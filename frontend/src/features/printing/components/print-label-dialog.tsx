"use client";

import { useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { showError, showSuccess } from "@/lib/toast";
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

interface ApiError {
  response?: {
    data?: {
      message?: string;
      errorCode?: string;
    };
  };
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
  const [saving, setSaving] = useState(false);
  const [job, setJob] = useState<PrintJobDto | null>(null);
  const [reprintOpen, setReprintOpen] = useState(false);
  const printer = useLocalPrinter(printerCode, isOpen);
  const templateReady = Boolean(templateId);

  const handleSubmit = async () => {
    if (!templateId) {
      showError("Label template is required before printing.");
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
      showSuccess("Label print job created.");
    } catch (err) {
      const apiError = err as ApiError;
      showError(apiError.response?.data?.message ?? "Cannot create label print job.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Print package label</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-5 py-2">
            <Alert>
              <AlertTitle>Local printer</AlertTitle>
              <AlertDescription>
                {printer.error ?? `Printer ${printerCode}: ${printer.status?.status ?? printer.state}`}
              </AlertDescription>
            </Alert>

            {!templateReady ? (
              <Alert variant="destructive">
                <AlertTitle>Template required</AlertTitle>
                <AlertDescription>Configure a label template ID before creating a print job.</AlertDescription>
              </Alert>
            ) : null}

            <FieldGroup>
              <Field orientation="responsive">
                <FieldLabel>Shipment</FieldLabel>
                <div className="text-sm font-semibold">{shipmentNo}</div>
              </Field>
              <Field orientation="responsive">
                <FieldLabel>Package</FieldLabel>
                <div className="text-sm font-semibold">{packageNo}</div>
              </Field>
              <Field orientation="responsive">
                <FieldLabel htmlFor="printerCode">Printer</FieldLabel>
                <Input id="printerCode" value={printerCode} readOnly />
              </Field>
              <Field orientation="responsive">
                <FieldLabel htmlFor="labelWeight">Weight (kg)</FieldLabel>
                <Input id="labelWeight" value={weightKg.toFixed(3)} readOnly />
                <FieldDescription>Source: {weightSource}</FieldDescription>
              </Field>
            </FieldGroup>

            {job ? (
              <Alert>
                <AlertTitle className="flex items-center justify-between gap-3">
                  Print job
                  <PrintJobStatusBadge status={job.status} />
                </AlertTitle>
                <AlertDescription>Job {job.id} · Template {job.templateCode}</AlertDescription>
              </Alert>
            ) : null}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Skip printing</Button>
            {job ? (
              <Button type="button" variant="secondary" onClick={() => setReprintOpen(true)} disabled={saving}>Reprint</Button>
            ) : null}
            <Button type="button" onClick={handleSubmit} disabled={saving || !templateReady}>
              {saving ? <Spinner data-icon="inline-start" /> : null}
              Create print job
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
