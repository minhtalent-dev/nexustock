"use client";

import { useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Spinner } from "@/components/ui/spinner";
import { showError, showSuccess } from "@/lib/toast";
import { reprintJob } from "../api";
import type { PrintJobDto } from "../types";
import { PrintJobStatusBadge } from "./print-job-status-badge";

const reasonOptions = [
  { value: "LABEL_DAMAGED", label: "Label damaged" },
  { value: "PRINTER_JAM", label: "Printer jam" },
  { value: "WRONG_LABEL_APPLIED", label: "Wrong label applied" },
  { value: "SUPERVISOR_APPROVED", label: "Supervisor approved" },
];

interface ReprintLabelDialogProps {
  isOpen: boolean;
  onClose: () => void;
  sourceJob: PrintJobDto;
  onReprinted?: (job: PrintJobDto) => void;
}

interface ApiError {
  response?: {
    data?: {
      message?: string;
      errorCode?: string;
    };
  };
}

export function ReprintLabelDialog({ isOpen, onClose, sourceJob, onReprinted }: ReprintLabelDialogProps) {
  const [reasonCode, setReasonCode] = useState("");
  const [saving, setSaving] = useState(false);
  const [reprintResult, setReprintResult] = useState<PrintJobDto | null>(null);

  const handleSubmit = async () => {
    if (!reasonCode) {
      showError("Reprint reason is required.");
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
      showSuccess("Label reprint job created.");
    } catch (err) {
      const apiError = err as ApiError;
      const fallback = apiError.response?.data?.errorCode === "REPRINT_LIMIT_EXCEEDED"
        ? "Reprint limit exceeded."
        : "Cannot create label reprint job.";
      showError(apiError.response?.data?.message ?? fallback);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>Reprint package label</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-5 py-2">
          <Alert>
            <AlertTitle className="flex items-center justify-between gap-3">
              Source print job
              <PrintJobStatusBadge status={sourceJob.status} />
            </AlertTitle>
            <AlertDescription>Job {sourceJob.id} · Printer {sourceJob.printerCode}</AlertDescription>
          </Alert>

          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="labelReprintReason">Reprint reason</FieldLabel>
              <NativeSelect
                id="labelReprintReason"
                className="w-full"
                value={reasonCode}
                onChange={(event) => setReasonCode(event.target.value)}
                aria-invalid={!reasonCode}
              >
                <NativeSelectOption value="">Select reason</NativeSelectOption>
                {reasonOptions.map((reason) => (
                  <NativeSelectOption key={reason.value} value={reason.value}>{reason.label}</NativeSelectOption>
                ))}
              </NativeSelect>
            </Field>
          </FieldGroup>

          {reprintResult ? (
            <Alert>
              <AlertTitle className="flex items-center justify-between gap-3">
                Reprint job
                <PrintJobStatusBadge status={reprintResult.status} />
              </AlertTitle>
              <AlertDescription>Job {reprintResult.id} · Reason {reprintResult.reasonCode}</AlertDescription>
            </Alert>
          ) : null}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Close</Button>
          <Button type="button" onClick={handleSubmit} disabled={saving || !reasonCode}>
            {saving ? <Spinner data-icon="inline-start" /> : null}
            Create reprint job
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
