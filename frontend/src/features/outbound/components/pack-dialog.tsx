"use client";

import { useEffect, useMemo, useState } from "react";
import api from "@/lib/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { PrintLabelDialog } from "@/features/printing/components/print-label-dialog";
import { showError, showSuccess } from "@/lib/toast";
import { useLocalScale } from "@/features/outbound/hooks/use-local-scale";

const DEFAULT_LABEL_TEMPLATE_ID = "00000000-0000-0000-0000-000000002201";
const DEFAULT_PRINTER_CODE = "PRINTER-01";

interface ApiError {
  response?: {
    data?: {
      message?: string;
    };
  };
}

interface ManualOverrideResponse {
  manualOverrideId: string;
  manualWeight: number;
}

interface CompletedPackingContext {
  packageNo: string;
  weight: number;
  weightSource: string;
}

interface CompletePackingDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  shipmentId: string;
  shipmentNo: string;
}

export function CompletePackingDialog({
  isOpen,
  onClose,
  onSuccess,
  shipmentId,
  shipmentNo,
}: CompletePackingDialogProps) {
  const [packageNo, setPackageNo] = useState("");
  const [manualMode, setManualMode] = useState(false);
  const [manualWeight, setManualWeight] = useState("");
  const [manualReason, setManualReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [printDialogOpen, setPrintDialogOpen] = useState(false);
  const [completedPacking, setCompletedPacking] = useState<CompletedPackingContext | null>(null);
  const { status, reading, error, reconnect } = useLocalScale(isOpen);

  const weight = reading?.weightKg ?? 0;
  const parsedManualWeight = Number(manualWeight);
  const canSubmitScale = status === "connected" && Boolean(reading?.stable) && weight > 0;
  const canSubmitManual = parsedManualWeight > 0 && manualReason.trim().length > 0;
  const canSubmit = !saving && Boolean(packageNo.trim()) && (manualMode ? canSubmitManual : canSubmitScale);

  const statusLabel = useMemo(() => {
    if (status === "connected" && reading?.stable) return "Stable";
    if (status === "connected") return "Live";
    if (status === "connecting") return "Connecting";
    if (status === "unavailable") return "Unavailable";
    if (status === "error") return "Error";
    return "Idle";
  }, [reading?.stable, status]);

  useEffect(() => {
    if (isOpen) {
      queueMicrotask(() => {
        setPackageNo(`PKG-${shipmentNo}-${Date.now().toString().slice(-4)}`);
        setManualMode(false);
        setManualWeight("");
        setManualReason("");
      });
    }
  }, [isOpen, shipmentNo]);

  const completeWithScale = async () => {
    await api.post(`/outbound/packing/${shipmentId}/complete`, {
      packageNo: packageNo.trim(),
      weight,
      weightSource: "scale",
      scaleStable: true
    });

    return {
      packageNo: packageNo.trim(),
      weight,
      weightSource: "scale",
    } satisfies CompletedPackingContext;
  };

  const completeWithManualOverride = async () => {
    const overrideRes = await api.post<ManualOverrideResponse>("/outbound/packing/weight/manual", {
      shipmentId,
      packageNo: packageNo.trim(),
      manualWeight: parsedManualWeight,
      reason: manualReason.trim(),
    });

    await api.post(`/outbound/packing/${shipmentId}/complete`, {
      packageNo: packageNo.trim(),
      weight: overrideRes.data.manualWeight,
      weightSource: "manual_override",
      scaleStable: false,
      manualOverrideId: overrideRes.data.manualOverrideId,
    });

    return {
      packageNo: packageNo.trim(),
      weight: overrideRes.data.manualWeight,
      weightSource: "manual_override",
    } satisfies CompletedPackingContext;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!packageNo.trim()) {
      showError("Package number is required.");
      return;
    }
    if (!manualMode && !canSubmitScale) {
      showError("Stable scale weight is required before packing.");
      return;
    }
    if (manualMode && !canSubmitManual) {
      showError("Manual weight and override reason are required.");
      return;
    }

    setSaving(true);
    try {
      const result = manualMode
        ? await completeWithManualOverride()
        : await completeWithScale();

      setCompletedPacking(result);
      setPrintDialogOpen(true);
      showSuccess("Packing completed.");
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const message = (err as ApiError).response?.data?.message || "Cannot complete packing.";
      showError(message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>Confirm packing</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="flex flex-col gap-5 py-2">
          <Alert>
            <AlertTitle className="flex items-center justify-between gap-3">
              Local scale
              <Badge variant={reading?.stable ? "default" : "secondary"}>{statusLabel}</Badge>
            </AlertTitle>
            <AlertDescription>
              {error || "Packing prefers stable Local Agent scale readings. Manual override requires approval reason."}
            </AlertDescription>
          </Alert>

          <FieldGroup>
            <Field orientation="responsive">
              <FieldLabel>Shipment</FieldLabel>
              <div className="text-sm font-semibold">{shipmentNo}</div>
            </Field>
            <Field orientation="responsive">
              <FieldLabel htmlFor="packageNo">Package number</FieldLabel>
              <Input
                id="packageNo"
                value={packageNo}
                onChange={(e) => setPackageNo(e.target.value)}
                autoFocus
              />
            </Field>
            <Field orientation="responsive">
              <FieldLabel htmlFor="weight">Scale weight (kg)</FieldLabel>
              <Input
                id="weight"
                type="number"
                value={weight.toFixed(3)}
                readOnly
                aria-invalid={!reading?.stable && !manualMode}
              />
              <FieldDescription>
                Source: {reading?.deviceId ?? "Local Agent"} · {reading?.connectionState ?? status}
              </FieldDescription>
            </Field>
          </FieldGroup>

          {manualMode ? (
            <FieldGroup>
              <Field orientation="responsive">
                <FieldLabel htmlFor="manualWeight">Manual weight (kg)</FieldLabel>
                <Input
                  id="manualWeight"
                  type="number"
                  min="0.001"
                  step="0.001"
                  value={manualWeight}
                  onChange={(e) => setManualWeight(e.target.value)}
                  aria-invalid={!canSubmitManual}
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="manualReason">Override reason</FieldLabel>
                <Textarea
                  id="manualReason"
                  value={manualReason}
                  onChange={(e) => setManualReason(e.target.value)}
                  placeholder="Example: Scale unavailable, supervisor approved manual weight."
                  aria-invalid={!manualReason.trim()}
                />
              </Field>
            </FieldGroup>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
            <Button type="button" variant="secondary" onClick={reconnect} disabled={saving || status === "connecting"}>Reconnect scale</Button>
            <Button type="button" variant="secondary" onClick={() => setManualMode((value) => !value)} disabled={saving}>
              {manualMode ? "Use scale" : "Manual override"}
            </Button>
            <Button type="submit" disabled={!canSubmit}>Complete packing</Button>
          </DialogFooter>
        </form>
        </DialogContent>
      </Dialog>

      {completedPacking ? (
        <PrintLabelDialog
          isOpen={printDialogOpen}
          onClose={() => setPrintDialogOpen(false)}
          shipmentId={shipmentId}
          shipmentNo={shipmentNo}
          packageNo={completedPacking.packageNo}
          weightKg={completedPacking.weight}
          weightSource={completedPacking.weightSource}
          templateId={DEFAULT_LABEL_TEMPLATE_ID}
          printerCode={DEFAULT_PRINTER_CODE}
        />
      ) : null}
    </>
  );
}
