"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";

interface CompletePickDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  pickTaskId: string;
  itemName: string;
  lotNo: string;
  locationCode: string;
  allocatedQty: number;
}

export function CompletePickDialog({
  isOpen,
  onClose,
  onSuccess,
  pickTaskId,
  itemName,
  lotNo,
  locationCode,
  allocatedQty,
}: CompletePickDialogProps) {
  const t = useTranslations("Features.outbound");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [pickedQty, setPickedQty] = useState<number>(allocatedQty);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (isOpen) {
      queueMicrotask(() => setPickedQty(allocatedQty));
    }
  }, [isOpen, allocatedQty]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (pickedQty <= 0) {
      showApiErrorToast(t("errors.pickQtyInvalid"), t("errors.pickQtyInvalid"));
      return;
    }
    if (pickedQty > allocatedQty) {
      showApiErrorToast(
        t("errors.pickQtyExceeded", { allocated: allocatedQty }),
        t("errors.pickQtyExceeded", { allocated: allocatedQty })
      );
      return;
    }

    setSaving(true);
    try {
      await api.post(`/outbound/picks/${pickTaskId}/complete`, {
        pickedQty
      });
      showSuccess(t("toastPickSuccess"));
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.pickFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[400px]">
        <DialogHeader>
          <DialogTitle>{t("pickConfirmTitle")}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">{t("item")}</Label>
            <div className="col-span-2 text-sm font-semibold">{itemName}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">{t("lotNo")}</Label>
            <div className="col-span-2 text-sm font-mono font-semibold">{lotNo}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">{t("location")}</Label>
            <div className="col-span-2 text-sm font-bold text-amber-600">{locationCode}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">{t("requestedPick")}</Label>
            <div className="col-span-2 text-sm font-semibold">{allocatedQty}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label htmlFor="pickedQty" className="text-right">{t("actualPick")}</Label>
            <Input
              id="pickedQty"
              type="number"
              step="any"
              max={allocatedQty}
              min={0.0001}
              value={pickedQty}
              onChange={(e) => setPickedQty(parseFloat(e.target.value) || 0)}
              className="col-span-2"
              autoFocus
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>{tc("cancel")}</Button>
            <Button type="submit" disabled={saving}>{t("confirmPick")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
