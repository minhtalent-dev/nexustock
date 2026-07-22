"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { ArrowLeft, Save } from "lucide-react";

interface Zone {
  id: string;
  name: string;
  code: string;
}

export default function NewStocktakePage() {
  const t = useTranslations("Admin.stocktakes");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const router = useRouter();
  const [stocktakeNo, setStocktakeNo] = useState(() => {
    const now = new Date();
    return `SC-${now.getFullYear()}${(now.getMonth() + 1).toString().padStart(2, "0")}${now.getDate().toString().padStart(2, "0")}-${now.getHours().toString().padStart(2, "0")}${now.getMinutes().toString().padStart(2, "0")}`;
  });
  const [zoneId, setZoneId] = useState<string | null>(null);
  const [zones, setZones] = useState<Zone[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const fetchZones = useCallback(async () => {
    try {
      const res = await api.get<Zone[]>("/storage-zones");
      setZones(res.data || []);
    } catch {
      showApiErrorToast("", t("errors.loadZonesFailed"));
    }
  }, [t]);

  useEffect(() => {
    queueMicrotask(() => void fetchZones());
  }, [fetchZones]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!stocktakeNo) {
      showApiErrorToast("", t("errors.stocktakeNoRequired"));
      return;
    }

    setSubmitting(true);
    try {
      const res = await api.post("/stocktakes", {
        stocktakeNo,
        zoneId: zoneId === "ALL" ? null : zoneId,
      });
      showSuccess(t("toastCreateSuccess"));
      router.push(`/admin/inventory/stocktakes/${res.data.id}`);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-2xl mx-auto">
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          render={<Link href="/admin/inventory/stocktakes" />}
          nativeButton={false}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <h1 className="text-2xl font-bold">{t("newTitle")}</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("newFormTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="space-y-2">
              <Label htmlFor="stocktakeNo">{t("stocktakeNoLabel")}</Label>
              <Input
                id="stocktakeNo"
                value={stocktakeNo}
                onChange={(e) => setStocktakeNo(e.target.value)}
                placeholder={t("stocktakeNoPlaceholder")}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="zone">{t("zoneLabel")}</Label>
              <Select
                onValueChange={(val) => {
                  if (val === "ALL") {
                    setZoneId(null);
                  } else {
                    setZoneId(val);
                  }
                }}
                defaultValue="ALL"
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("zonePlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ALL">{t("zoneAll")}</SelectItem>
                  {zones.map((z) => (
                    <SelectItem key={z.id} value={z.id}>
                      {z.name} ({z.code})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex justify-end gap-2 pt-4">
              <Button
                variant="outline"
                type="button"
                render={<Link href="/admin/inventory/stocktakes" />}
                nativeButton={false}
              >
                {tc("cancel")}
              </Button>
              <Button type="submit" disabled={submitting} className="gap-2">
                <Save className="h-4 w-4" />
                {submitting ? tc("saving") : t("saveDraft")}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
