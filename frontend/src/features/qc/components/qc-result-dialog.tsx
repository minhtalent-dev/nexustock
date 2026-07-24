"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import api from "@/lib/api";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { EntityAttachmentsPanel } from "@/features/files/entity-attachments-panel";
import { bindAttachment, type UploadResult } from "@/features/files/api";

interface QcResultDialogProps {
  isOpen: boolean;
  onClose: () => void;
  lotId: string;
  lotNo: string;
  qcRequestId: string;
  onSuccess: () => void;
}

export function QcResultDialog({ isOpen, onClose, lotId, lotNo, qcRequestId, onSuccess }: QcResultDialogProps) {
  const t = useTranslations("Features.qc");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [isPassed, setIsPassed] = useState(true);
  const [metrics, setMetrics] = useState("");
  const [pendingUploads, setPendingUploads] = useState<UploadResult[]>([]);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      const res = await api.post<{ id: string; message: string }>(`/qc/${lotId}/result`, {
        qcRequestId,
        isPassed,
        metrics: metrics.trim() || undefined,
        attachmentRefs: undefined // Không gửi snapshot url thô từ client nữa
      });

      const resultId = res.data.id;
      if (resultId && pendingUploads.length > 0) {
        await Promise.all(
          pendingUploads.map(uploaded =>
            bindAttachment({
              uploadId: uploaded.uploadId,
              entityType: "QC_RESULT",
              entityId: resultId,
            })
          )
        );
      }

      showSuccess(t("toastResultSuccess"));
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.resultFailed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-xl bg-card border-border text-foreground font-sans">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle className="text-lg font-semibold text-foreground">{t("resultDialogTitle", { lotNo })}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-5 py-4">
            <div className="flex items-center justify-between bg-zinc-800/50 p-3 rounded-lg border border-zinc-850">
              <div>
                <Label className="text-sm font-medium text-foreground block">{t("inspectionStatus")}</Label>
                <span className="text-xs text-muted-foreground">
                  {isPassed ? t("passedHint") : t("failedHint")}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className={`text-xs font-semibold ${isPassed ? "text-emerald-500" : "text-rose-500"}`}>
                  {isPassed ? t("passed") : t("failed")}
                </span>
                <Switch
                  checked={isPassed}
                  onCheckedChange={setIsPassed}
                  className="data-[state=checked]:bg-emerald-600 data-[state=unchecked]:bg-rose-600"
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="metrics" className="text-xs text-muted-foreground">{t("metrics")}</Label>
              <Textarea
                id="metrics"
                placeholder={t("metricsPlaceholder")}
                value={metrics}
                onChange={(e) => setMetrics(e.target.value)}
                rows={3}
                className="bg-zinc-800 border-zinc-700 text-foreground text-sm focus:ring-emerald-500"
              />
            </div>

            <div className="grid gap-2">
              <Label className="text-xs text-muted-foreground">{t("attachments")}</Label>
              <EntityAttachmentsPanel
                entityType="QC_RESULT"
                entityId={null}
                pendingUploads={pendingUploads}
                onPendingChange={setPendingUploads}
              />
            </div>
          </div>

          <DialogFooter className="gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={loading}
              className="border-border text-foreground hover:bg-muted h-9 text-xs"
            >
              {tc("cancel")}
            </Button>
            <Button
              type="submit"
              disabled={loading}
              className="bg-emerald-600 hover:bg-emerald-500 text-white h-9 text-xs"
            >
              {loading ? t("submitting") : t("submitResult")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
