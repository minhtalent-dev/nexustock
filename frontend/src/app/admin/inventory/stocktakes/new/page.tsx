"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, Save } from "lucide-react";
import Link from "next/link";

interface Zone {
  id: string;
  name: string;
  code: string;
}

export default function NewStocktakePage() {
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
      showError("Không thể tải danh sách khu vực kho.");
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => void fetchZones());
  }, [fetchZones]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!stocktakeNo) {
      showError("Vui lòng nhập mã đợt kiểm kê");
      return;
    }

    setSubmitting(true);
    try {
      const res = await api.post("/stocktakes", {
        stocktakeNo,
        zoneId: zoneId === "ALL" ? null : zoneId
      });
      showSuccess("Tạo đợt kiểm kê nháp thành công!");
      router.push(`/admin/inventory/stocktakes/${res.data.id}`);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Tạo đợt kiểm kê thất bại"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-2xl mx-auto">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/inventory/stocktakes">
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold">Tạo đợt kiểm kê mới</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Thông tin đợt kiểm kê</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="space-y-2">
              <Label htmlFor="stocktakeNo">Mã đợt kiểm kê</Label>
              <Input
                id="stocktakeNo"
                value={stocktakeNo}
                onChange={(e) => setStocktakeNo(e.target.value)}
                placeholder="Ví dụ: SC-20260711-001"
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="zone">Phạm vi khu vực (Storage Zone)</Label>
              <Select onValueChange={(val) => {
                if (val === "ALL") {
                  setZoneId(null);
                } else {
                  setZoneId(val);
                }
              }} defaultValue="ALL">
                <SelectTrigger>
                  <SelectValue placeholder="Chọn khu vực kiểm kê" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ALL">Toàn bộ kho</SelectItem>
                  {zones.map((z) => (
                    <SelectItem key={z.id} value={z.id}>
                      {z.name} ({z.code})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex justify-end gap-2 pt-4">
              <Button variant="outline" type="button" asChild>
                <Link href="/admin/inventory/stocktakes">Hủy</Link>
              </Button>
              <Button type="submit" disabled={submitting} className="gap-2">
                <Save className="h-4 w-4" />
                {submitting ? "Đang lưu..." : "Lưu đợt nháp"}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
