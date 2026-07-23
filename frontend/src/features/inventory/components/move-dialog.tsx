"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";

interface LocationDto {
  id: string;
  code: string;
}

interface ReasonDto {
  id: string;
  code: string;
  name: string;
}

interface MoveInventoryDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  lotNo: string;
  itemId: string;
  itemName: string;
  fromLocationId: string;
  fromLocationCode: string;
  maxQty: number;
}

export function MoveInventoryDialog({
  isOpen,
  onClose,
  onSuccess,
  lotNo,
  itemId,
  itemName,
  fromLocationId,
  fromLocationCode,
  maxQty,
}: MoveInventoryDialogProps) {
  const t = useTranslations("Features.inventory");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [reasons, setReasons] = useState<ReasonDto[]>([]);
  const [toLocationId, setToLocationId] = useState("");
  const [qty, setQty] = useState<number>(maxQty);
  const [reasonCode, setReasonCode] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchLocations = useCallback(async () => {
    try {
      const res = await api.get<{ items: LocationDto[] }>("/master-data/storage-locations");
      const list = (res.data.items || []).filter(l => l.id !== fromLocationId);
      setLocations(list);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadLocationsFailed"));
    }
  }, [fromLocationId, t, tErrors]);

  const fetchReasons = useCallback(async () => {
    try {
      const res = await api.get<{ items: ReasonDto[] }>("/master-data/reasons");
      setReasons(res.data.items || []);
    } catch {
      setReasons([
        { id: "1", code: "ROUTINE_QC", name: "ROUTINE_QC" },
        { id: "2", code: "OPTIMIZE_SPACE", name: "OPTIMIZE_SPACE" },
        { id: "3", code: "DAMAGE_COMPROMISE", name: "DAMAGE_COMPROMISE" }
      ]);
    }
  }, []);

  useEffect(() => {
    if (isOpen) {
      queueMicrotask(() => {
        void fetchLocations();
        void fetchReasons();
        setQty(maxQty);
        setToLocationId("");
        setReasonCode("");
      });
    }
  }, [fetchLocations, fetchReasons, isOpen, maxQty]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!toLocationId) {
      showApiErrorToast(t("errors.toLocationRequired"), t("errors.toLocationRequired"));
      return;
    }
    if (qty <= 0) {
      showApiErrorToast(t("errors.qtyInvalid"), t("errors.qtyInvalid"));
      return;
    }
    if (qty > maxQty) {
      showApiErrorToast(
        t("errors.qtyExceeded", { max: maxQty }),
        t("errors.qtyExceeded", { max: maxQty })
      );
      return;
    }
    if (!reasonCode) {
      showApiErrorToast(t("errors.reasonRequired"), t("errors.reasonRequired"));
      return;
    }

    setSaving(true);
    try {
      await api.post("/inventory/move", {
        itemId,
        lotNo,
        fromLocationId,
        toLocationId,
        qty,
        reasonCode,
      });
      showSuccess(t("toastMoveSuccess"));
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.moveFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("moveTitle")}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">{t("item")}</Label>
            <div className="col-span-3 text-sm font-semibold">{itemName}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">{t("lotNo")}</Label>
            <div className="col-span-3 text-sm font-semibold">{lotNo}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">{t("fromLocation")}</Label>
            <div className="col-span-3 text-sm font-semibold text-amber-600">{fromLocationCode}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="toLocationId" className="text-right">{t("toLocation")}</Label>
            <select
              id="toLocationId"
              value={toLocationId}
              onChange={(e) => setToLocationId(e.target.value)}
              className="col-span-3 flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">{t("selectToLocation")}</option>
              {locations.map((l) => (
                <option key={l.id} value={l.id}>{l.code}</option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="qty" className="text-right">{t("quantity")}</Label>
            <Input
              id="qty"
              type="number"
              step="any"
              max={maxQty}
              min={0.0001}
              value={qty}
              onChange={(e) => setQty(parseFloat(e.target.value) || 0)}
              className="col-span-3"
            />
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="reasonCode" className="text-right">{t("reason")}</Label>
            <select
              id="reasonCode"
              value={reasonCode}
              onChange={(e) => setReasonCode(e.target.value)}
              className="col-span-3 flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">{t("selectReason")}</option>
              {reasons.map((r) => (
                <option key={r.id} value={r.code}>{r.name} ({r.code})</option>
              ))}
            </select>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>{tc("cancel")}</Button>
            <Button type="submit" disabled={saving}>{t("confirmMove")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
