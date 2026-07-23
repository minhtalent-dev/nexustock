"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import MobileShell from "@/components/mobile/mobile-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import api from "@/lib/api";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { CheckSquare, Search } from "lucide-react";

interface LotDetails {
  id: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  qcStatus: string;
}

interface QcQueueItem {
  id: string;
  lotId: string;
  lotNo: string;
}

export default function MobileQcPage() {
  const t = useTranslations("Mobile.qc");
  const tErrors = useTranslations("Errors");
  const [enabled, setEnabled] = React.useState<boolean | null>(null);
  const [lotNo, setLotNo] = React.useState("");
  const [lot, setLot] = React.useState<LotDetails | null>(null);
  const [qcRequestId, setQcRequestId] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    api
      .get<{ enabled: boolean }>("/feature-flags/FF_MOBILE_QC")
      .then((res) => setEnabled(!!res.data.enabled))
      .catch(() => setEnabled(false));
  }, []);

  const showApiErr = (err: unknown, fallback: string) => {
    const { codeLabel, message } = resolveApiError(err, tErrors);
    showApiErrorToast(codeLabel, message || fallback);
  };

  const lookup = async () => {
    if (!lotNo.trim()) {
      showApiErrorToast("VALIDATION", t("toast.needLot"));
      return;
    }
    setBusy(true);
    setLot(null);
    setQcRequestId(null);
    try {
      const lots = await api.get<LotDetails[]>(`/lots/${lotNo.trim()}`);
      const found = lots.data[0];
      if (!found) {
        showApiErrorToast("QC_LOT_NOT_FOUND", t("toast.lookupFailed"));
        return;
      }
      setLot(found);
      const queue = await api.get<QcQueueItem[]>("/qc/queue", { params: { q: found.lotNo } });
      const match = queue.data.find((q) => q.lotId === found.id || q.lotNo === found.lotNo);
      setQcRequestId(match?.id ?? null);
    } catch (err) {
      showApiErr(err, t("toast.lookupFailed"));
    } finally {
      setBusy(false);
    }
  };

  const submitResult = async (isPassed: boolean) => {
    if (!lot) return;
    if (!qcRequestId) {
      showApiErrorToast("QC_REQUEST_NOT_PENDING", t("toast.needRequest"));
      return;
    }
    setBusy(true);
    try {
      await api.post(`/qc/${lot.id}/result`, {
        qcRequestId,
        isPassed,
      });
      showSuccess(t("toast.resultOk"));
      await lookup();
    } catch (err) {
      showApiErr(err, t("toast.lookupFailed"));
    } finally {
      setBusy(false);
    }
  };

  const submitHold = async () => {
    if (!lot) return;
    setBusy(true);
    try {
      await api.post(`/qc/${lot.id}/hold`, { reasonCode: "MOBILE_HOLD" });
      showSuccess(t("toast.holdOk"));
      await lookup();
    } catch (err) {
      showApiErr(err, t("toast.lookupFailed"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <PageShell className="gap-6">
      <MobileShell>
      <div className="flex flex-col gap-4 p-4 text-foreground">
        <div className="flex items-center gap-2">
          <CheckSquare className="h-5 w-5 text-emerald-500" />
          <div>
            <h1 className="text-lg font-semibold">{t("page.title")}</h1>
            <p className="text-xs text-muted-foreground">{t("page.subtitle")}</p>
          </div>
        </div>

        {enabled === false && (
          <Card className="bg-card border-border">
            <CardHeader>
              <CardTitle className="text-sm text-amber-400">{t("page.disabledTitle")}</CardTitle>
            </CardHeader>
            <CardContent className="text-xs text-muted-foreground">{t("page.disabledHint")}</CardContent>
          </Card>
        )}

        {enabled && (
          <>
            <div className="flex gap-2">
              <Input
                value={lotNo}
                onChange={(e) => setLotNo(e.target.value)}
                placeholder={t("labels.lotPlaceholder")}
                className="bg-card border-border text-foreground"
                onKeyDown={(e) => e.key === "Enter" && void lookup()}
              />
              <Button onClick={() => void lookup()} disabled={busy} className="bg-emerald-600 hover:bg-emerald-500">
                <Search className="h-4 w-4" />
              </Button>
            </div>

            {lot && (
              <Card className="bg-card border-border">
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">{lot.lotNo}</CardTitle>
                </CardHeader>
                <CardContent className="flex flex-col gap-3 text-xs">
                  <div className="text-muted-foreground">{lot.itemName} ({lot.itemCode})</div>
                  <div className="text-muted-foreground">
                    {t("labels.status")}: <span className="text-foreground">{lot.qcStatus}</span>
                  </div>
                  <div className="grid grid-cols-3 gap-2 pt-2">
                    <Button disabled={busy} onClick={() => void submitResult(true)} className="bg-emerald-600 hover:bg-emerald-500 h-9 text-xs">
                      {t("labels.pass")}
                    </Button>
                    <Button disabled={busy} onClick={() => void submitResult(false)} className="bg-rose-600 hover:bg-rose-500 h-9 text-xs">
                      {t("labels.fail")}
                    </Button>
                    <Button disabled={busy} onClick={() => void submitHold()} className="bg-amber-600 hover:bg-amber-500 h-9 text-xs">
                      {t("labels.hold")}
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}
          </>
        )}
      </div>
    </MobileShell>
    </PageShell>
  );
}
