"use client";

import { useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { PrintLabelDialog } from "@/features/printing/components/print-label-dialog";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { useLocalScale } from "@/features/outbound/hooks/use-local-scale";

const DEFAULT_LABEL_TEMPLATE_ID = "00000000-0000-0000-0000-000000002201";
const DEFAULT_PRINTER_CODE = "PRINTER-01";

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
  const t = useTranslations("Features.outbound");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

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
    if (status === "connected" && reading?.stable) return t("scaleStatus.stable");
    if (status === "connected") return t("scaleStatus.live");
    if (status === "connecting") return t("scaleStatus.connecting");
    if (status === "unavailable") return t("scaleStatus.unavailable");
    if (status === "error") return t("scaleStatus.error");
    return t("scaleStatus.idle");
  }, [reading?.stable, status, t]);

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
      showApiErrorToast(t("errors.packageNoRequired"), t("errors.packageNoRequired"));
      return;
    }
    if (!manualMode && !canSubmitScale) {
      showApiErrorToast(t("errors.stableWeightRequired"), t("errors.stableWeightRequired"));
      return;
    }
    if (manualMode && !canSubmitManual) {
      showApiErrorToast(t("errors.manualOverrideRequired"), t("errors.manualOverrideRequired"));
      return;
    }

    setSaving(true);
    try {
      const result = manualMode
        ? await completeWithManualOverride()
        : await completeWithScale();

      setCompletedPacking(result);
      setPrintDialogOpen(true);
      showSuccess(t("toastPackSuccess"));
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.packFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Dialog open={isOpen} onOpenChange={onClose}>
        <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>{t("packTitle")}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="flex flex-col gap-5 py-2">
          <Alert>
            <AlertTitle className="flex items-center justify-between gap-3">
              {t("localScale")}
              <Badge variant={reading?.stable ? "default" : "secondary"}>{statusLabel}</Badge>
            </AlertTitle>
            <AlertDescription>
              {error || t("scaleHint")}
            </AlertDescription>
          </Alert>

          <FieldGroup>
            <Field orientation="responsive">
              <FieldLabel>{t("shipment")}</FieldLabel>
              <div className="text-sm font-semibold">{shipmentNo}</div>
            </Field>
            <Field orientation="responsive">
              <FieldLabel htmlFor="packageNo">{t("packageNo")}</FieldLabel>
              <Input
                id="packageNo"
                value={packageNo}
                onChange={(e) => setPackageNo(e.target.value)}
                autoFocus
              />
            </Field>
            <Field orientation="responsive">
              <FieldLabel htmlFor="weight">{t("scaleWeight")}</FieldLabel>
              <Input
                id="weight"
                type="number"
                value={weight.toFixed(3)}
                readOnly
                aria-invalid={!reading?.stable && !manualMode}
              />
              <FieldDescription>
                {t("source")}: {reading?.deviceId ?? "Local Agent"} · {reading?.connectionState ?? status}
              </FieldDescription>
            </Field>
          </FieldGroup>

          {manualMode ? (
            <FieldGroup>
              <Field orientation="responsive">
                <FieldLabel htmlFor="manualWeight">{t("manualWeight")}</FieldLabel>
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
                <FieldLabel htmlFor="manualReason">{t("overrideReason")}</FieldLabel>
                <Textarea
                  id="manualReason"
                  value={manualReason}
                  onChange={(e) => setManualReason(e.target.value)}
                  placeholder={t("overrideReasonPlaceholder")}
                  aria-invalid={!manualReason.trim()}
                />
              </Field>
            </FieldGroup>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>{tc("cancel")}</Button>
            <Button type="button" variant="secondary" onClick={reconnect} disabled={saving || status === "connecting"}>{t("reconnectScale")}</Button>
            <Button type="button" variant="secondary" onClick={() => setManualMode((value) => !value)} disabled={saving}>
              {manualMode ? t("useScale") : t("manualOverride")}
            </Button>
            <Button type="submit" disabled={!canSubmit}>{t("completePacking")}</Button>
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
