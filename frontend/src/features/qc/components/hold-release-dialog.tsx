"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import api from "@/lib/api";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";

interface HoldReleaseDialogProps {
  isOpen: boolean;
  onClose: () => void;
  lotId: string;
  lotNo: string;
  mode: "hold" | "release" | "reject";
  onSuccess: () => void;
}

export function HoldReleaseDialog({ isOpen, onClose, lotId, lotNo, mode, onSuccess }: HoldReleaseDialogProps) {
  const t = useTranslations("Features.qc");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [reasonCode, setReasonCode] = useState("");
  const [locationId, setLocationId] = useState("");
  const [loading, setLoading] = useState(false);

  const getTitle = () => {
    switch (mode) {
      case "hold":
        return t("holdTitle", { lotNo });
      case "release":
        return t("releaseTitle", { lotNo });
      case "reject":
        return t("rejectTitle", { lotNo });
    }
  };

  const getButtonText = () => {
    switch (mode) {
      case "hold":
        return t("holdAction");
      case "release":
        return t("releaseAction");
      case "reject":
        return t("rejectAction");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reasonCode.trim()) {
      showApiErrorToast(t("errors.reasonRequired"), t("errors.reasonRequired"));
      return;
    }

    setLoading(true);
    try {
      if (mode === "hold") {
        await api.post(`/qc/${lotId}/hold`, {
          locationId: locationId || undefined,
          reasonCode: reasonCode.trim()
        });
        showSuccess(t("toastHoldSuccess"));
      } else if (mode === "release") {
        await api.post(`/qc/${lotId}/release`, {
          reasonCode: reasonCode.trim()
        });
        showSuccess(t("toastReleaseSuccess"));
      } else if (mode === "reject") {
        await api.post(`/qc/${lotId}/reject`, {
          reasonCode: reasonCode.trim()
        });
        showSuccess(t("toastRejectSuccess"));
      }
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.actionFailed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg bg-card border-border text-foreground font-sans">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle className="text-lg font-semibold text-foreground">{getTitle()}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            {mode === "hold" && (
              <div className="grid gap-2">
                <Label htmlFor="locationId" className="text-xs text-muted-foreground">{t("holdLocationOptional")}</Label>
                <Input
                  id="locationId"
                  placeholder={t("holdLocationPlaceholder")}
                  value={locationId}
                  onChange={(e) => setLocationId(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-foreground h-9 text-sm focus:ring-emerald-500"
                />
              </div>
            )}

            <div className="grid gap-2">
              <Label htmlFor="reasonCode" className="text-xs text-muted-foreground">{t("reasonCode")}</Label>
              <Input
                id="reasonCode"
                placeholder={mode === "hold" ? t("holdReasonPlaceholder") : t("actionReasonPlaceholder")}
                value={reasonCode}
                onChange={(e) => setReasonCode(e.target.value)}
                required
                className="bg-zinc-800 border-zinc-700 text-foreground h-9 text-sm focus:ring-emerald-500"
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
              className={`${
                mode === "release"
                  ? "bg-emerald-600 hover:bg-emerald-500"
                  : mode === "reject"
                  ? "bg-rose-600 hover:bg-rose-500"
                  : "bg-amber-600 hover:bg-amber-500"
              } text-white h-9 text-xs`}
            >
              {loading ? t("processing") : getButtonText()}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
