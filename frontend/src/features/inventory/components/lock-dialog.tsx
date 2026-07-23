"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";

interface ReasonDto {
  id: string;
  code: string;
  name: string;
}

interface LockLocationDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  locationId: string;
  locationCode: string;
}

export function LockLocationDialog({
  isOpen,
  onClose,
  onSuccess,
  locationId,
  locationCode,
}: LockLocationDialogProps) {
  const t = useTranslations("Features.inventory");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [reasons, setReasons] = useState<ReasonDto[]>([]);
  const [lockType, setLockType] = useState("ALL");
  const [reasonCode, setReasonCode] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchReasons = async () => {
    try {
      const res = await api.get<{ items: ReasonDto[] }>("/master-data/reasons");
      setReasons(res.data.items || []);
    } catch {
      setReasons([
        { id: "1", code: "MAINTENANCE", name: "MAINTENANCE" },
        { id: "2", code: "CLEANING", name: "CLEANING" },
        { id: "3", code: "SUSPENDED", name: "SUSPENDED" }
      ]);
    }
  };

  useEffect(() => {
    if (isOpen) {
      queueMicrotask(() => {
        void fetchReasons();
        setLockType("ALL");
        setReasonCode("");
      });
    }
  }, [isOpen]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reasonCode) {
      showApiErrorToast(t("errors.lockReasonRequired"), t("errors.lockReasonRequired"));
      return;
    }

    setSaving(true);
    try {
      await api.post(`/inventory/locations/${locationId}/lock`, {
        lockType,
        reasonCode,
      });
      showSuccess(t("toastLockSuccess", { code: locationCode }));
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.lockFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("lockTitle")}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">{t("location")}</Label>
            <div className="col-span-3 text-sm font-semibold text-red-600">{locationCode}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="lockType" className="text-right">{t("lockType")}</Label>
            <select
              id="lockType"
              value={lockType}
              onChange={(e) => setLockType(e.target.value)}
              className="col-span-3 flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="ALL">{t("lockTypeAll")}</option>
              <option value="INBOUND">{t("lockTypeInbound")}</option>
              <option value="OUTBOUND">{t("lockTypeOutbound")}</option>
            </select>
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
            <Button type="submit" disabled={saving}>{t("confirmLock")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
