"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { useLocalPrinter } from "../hooks/use-local-printer";
import { createPrintJob } from "../api";
import type { PrintJobDto } from "../types";
import { PrintJobStatusBadge } from "./print-job-status-badge";
import { ReprintLabelDialog } from "./reprint-label-dialog";

interface PrintLabelDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onPrinted?: (job: PrintJobDto) => void;
  shipmentId: string;
  shipmentNo: string;
  packageNo: string;
  weightKg: number;
  weightSource: string;
  templateId?: string;
  printerCode?: string;
}

export function PrintLabelDialog({
  isOpen,
  onClose,
  onPrinted,
  shipmentId,
  shipmentNo,
  packageNo,
  weightKg,
  weightSource,
  templateId,
  printerCode = "PRINTER-01",
}: PrintLabelDialogProps) {
  const t = useTranslations("Features.printing");
  const tErrors = useTranslations("Errors");

  const [saving, setSaving] = useState(false);
  const [job, setJob] = useState<PrintJobDto | null>(null);
  const [reprintOpen, setReprintOpen] = useState(false);
  const printer = useLocalPrinter(printerCode, isOpen);
  const templateReady = Boolean(templateId);

  const handleSubmit = async () => {
    if (!templateId) {
      showApiErrorToast(t("errors.templateRequired"), t("errors.templateRequired"));
      return;
    }

    setSaving(true);
    try {
      const created = await createPrintJob({
        templateId,
        printerCode,
        idempotencyKey: `packing_label_${shipmentId}_${packageNo}`,
        payload: {
          shipmentNo,
          packageNo,
          weightKg: weightKg.toFixed(3),
          weightSource,
        },
      });
      setJob(created);
      onPrinted?.(created);
      showSuccess(t("toastPrintSuccess"));
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.printFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{t("printPackageLabel")}</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-5 py-2">
            <Alert>
              <AlertTitle>{t("localPrinter")}</AlertTitle>
              <AlertDescription>
                {printer.error ?? t("printerStatus", {
                  code: printerCode,
                  status: printer.status?.status ?? printer.state,
                })}
              </AlertDescription>
            </Alert>

            {!templateReady ? (
              <Alert variant="destructive">
                <AlertTitle>{t("templateRequired")}</AlertTitle>
                <AlertDescription>{t("templateRequiredDesc")}</AlertDescription>
              </Alert>
            ) : null}

            <FieldGroup>
              <Field orientation="responsive">
                <FieldLabel>{t("shipment")}</FieldLabel>
                <div className="text-sm font-semibold">{shipmentNo}</div>
              </Field>
              <Field orientation="responsive">
                <FieldLabel>{t("package")}</FieldLabel>
                <div className="text-sm font-semibold">{packageNo}</div>
              </Field>
              <Field orientation="responsive">
                <FieldLabel htmlFor="printerCode">{t("printer")}</FieldLabel>
                <Input id="printerCode" value={printerCode} readOnly />
              </Field>
              <Field orientation="responsive">
                <FieldLabel htmlFor="labelWeight">{t("weight")}</FieldLabel>
                <Input id="labelWeight" value={weightKg.toFixed(3)} readOnly />
                <FieldDescription>{t("source")}: {weightSource}</FieldDescription>
              </Field>
            </FieldGroup>

            {job ? (
              <Alert>
                <AlertTitle className="flex items-center justify-between gap-3">
                  {t("printJob")}
                  <PrintJobStatusBadge status={job.status} />
                </AlertTitle>
                <AlertDescription>
                  {t("jobSummary", { id: job.id, template: job.templateCode })}
                </AlertDescription>
              </Alert>
            ) : null}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>{t("skipPrinting")}</Button>
            {job ? (
              <Button type="button" variant="secondary" onClick={() => setReprintOpen(true)} disabled={saving}>{t("reprint")}</Button>
            ) : null}
            <Button type="button" onClick={handleSubmit} disabled={saving || !templateReady}>
              {saving ? <Spinner data-icon="inline-start" /> : null}
              {t("createPrintJob")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {job ? (
        <ReprintLabelDialog
          isOpen={reprintOpen}
          onClose={() => setReprintOpen(false)}
          sourceJob={job}
          onReprinted={setJob}
        />
      ) : null}
    </>
  );
}
