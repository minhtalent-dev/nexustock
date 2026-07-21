"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Spinner } from "@/components/ui/spinner";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { reprintJob } from "../api";
import type { PrintJobDto } from "../types";
import { PrintJobStatusBadge } from "./print-job-status-badge";

const REASON_CODES = ["LABEL_DAMAGED", "PRINTER_JAM", "WRONG_LABEL_APPLIED", "SUPERVISOR_APPROVED"] as const;

interface ReprintLabelDialogProps {
  isOpen: boolean;
  onClose: () => void;
  sourceJob: PrintJobDto;
  onReprinted?: (job: PrintJobDto) => void;
}

export function ReprintLabelDialog({ isOpen, onClose, sourceJob, onReprinted }: ReprintLabelDialogProps) {
  const t = useTranslations("Features.printing");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [reasonCode, setReasonCode] = useState("");
  const [saving, setSaving] = useState(false);
  const [reprintResult, setReprintResult] = useState<PrintJobDto | null>(null);

  const handleSubmit = async () => {
    if (!reasonCode) {
      showApiErrorToast(t("errors.reprintReasonRequired"), t("errors.reprintReasonRequired"));
      return;
    }

    setSaving(true);
    try {
      const job = await reprintJob(sourceJob.id, {
        reasonCode,
        idempotencyKey: `reprint_${sourceJob.id}_${reasonCode}_${Date.now()}`,
      });
      setReprintResult(job);
      onReprinted?.(job);
      showSuccess(t("toastReprintSuccess"));
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      const payload = err as { response?: { data?: { errorCode?: string } } };
      const fallback = payload.response?.data?.errorCode === "REPRINT_LIMIT_EXCEEDED"
        ? t("errors.reprintLimitExceeded")
        : t("errors.reprintFailed");
      showApiErrorToast(codeLabel, message || fallback);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>{t("reprintPackageLabel")}</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-5 py-2">
          <Alert>
            <AlertTitle className="flex items-center justify-between gap-3">
              {t("sourcePrintJob")}
              <PrintJobStatusBadge status={sourceJob.status} />
            </AlertTitle>
            <AlertDescription>
              {t("sourceJobSummary", { id: sourceJob.id, printer: sourceJob.printerCode })}
            </AlertDescription>
          </Alert>

          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="labelReprintReason">{t("reprintReason")}</FieldLabel>
              <NativeSelect
                id="labelReprintReason"
                className="w-full"
                value={reasonCode}
                onChange={(event) => setReasonCode(event.target.value)}
                aria-invalid={!reasonCode}
              >
                <NativeSelectOption value="">{t("selectReason")}</NativeSelectOption>
                {REASON_CODES.map((code) => (
                  <NativeSelectOption key={code} value={code}>{t(`reasons.${code}`)}</NativeSelectOption>
                ))}
              </NativeSelect>
            </Field>
          </FieldGroup>

          {reprintResult ? (
            <Alert>
              <AlertTitle className="flex items-center justify-between gap-3">
                {t("reprintJob")}
                <PrintJobStatusBadge status={reprintResult.status} />
              </AlertTitle>
              <AlertDescription>
                {t("reprintJobSummary", { id: reprintResult.id, reason: reprintResult.reasonCode })}
              </AlertDescription>
            </Alert>
          ) : null}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose} disabled={saving}>{tc("close")}</Button>
          <Button type="button" onClick={handleSubmit} disabled={saving || !reasonCode}>
            {saving ? <Spinner data-icon="inline-start" /> : null}
            {t("createReprintJob")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
