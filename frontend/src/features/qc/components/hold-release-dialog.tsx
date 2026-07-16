"use client";

import { useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import api from "@/lib/api";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";

interface HoldReleaseDialogProps {
  isOpen: boolean;
  onClose: () => void;
  lotId: string;
  lotNo: string;
  mode: "hold" | "release" | "reject";
  onSuccess: () => void;
}

export function HoldReleaseDialog({ isOpen, onClose, lotId, lotNo, mode, onSuccess }: HoldReleaseDialogProps) {
  const [reasonCode, setReasonCode] = useState("");
  const [locationId, setLocationId] = useState("");
  const [loading, setLoading] = useState(false);

  const getTitle = () => {
    switch (mode) {
      case "hold":
        return `Khóa lô hàng ${lotNo}`;
      case "release":
        return `Giải phóng lô hàng ${lotNo}`;
      case "reject":
        return `Từ chối lô hàng ${lotNo}`;
    }
  };

  const getButtonText = () => {
    switch (mode) {
      case "hold":
        return "Khóa hàng";
      case "release":
        return "Giải phóng";
      case "reject":
        return "Từ chối";
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reasonCode.trim()) {
      showError("Vui lòng nhập lý do.");
      return;
    }

    setLoading(true);
    try {
      if (mode === "hold") {
        await api.post(`/qc/${lotId}/hold`, {
          locationId: locationId || undefined,
          reasonCode: reasonCode.trim()
        });
        showSuccess("Khóa lô hàng thành công.");
      } else if (mode === "release") {
        await api.post(`/qc/${lotId}/release`, {
          reasonCode: reasonCode.trim()
        });
        showSuccess("Giải phóng lô hàng thành công.");
      } else if (mode === "reject") {
        await api.post(`/qc/${lotId}/reject`, {
          reasonCode: reasonCode.trim()
        });
        showSuccess("Từ chối lô hàng thành công.");
      }
      onSuccess();
      onClose();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi xử lý yêu cầu."));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[425px] bg-zinc-900 border-zinc-800 text-white font-sans">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle className="text-lg font-semibold text-white">{getTitle()}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            {mode === "hold" && (
              <div className="grid gap-2">
                <Label htmlFor="locationId" className="text-xs text-zinc-400">Vị trí kệ cụ thể (Không bắt buộc)</Label>
                <Input
                  id="locationId"
                  placeholder="Nhập ID vị trí (nếu muốn khóa cụ thể)"
                  value={locationId}
                  onChange={(e) => setLocationId(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-white h-9 text-sm focus:ring-emerald-500"
                />
              </div>
            )}

            <div className="grid gap-2">
              <Label htmlFor="reasonCode" className="text-xs text-zinc-400">Lý do (Reason code)</Label>
              <Input
                id="reasonCode"
                placeholder={mode === "hold" ? "Ví dụ: QC_FAILED, DAMAGE" : "Nhập lý do thực hiện"}
                value={reasonCode}
                onChange={(e) => setReasonCode(e.target.value)}
                required
                className="bg-zinc-800 border-zinc-700 text-white h-9 text-sm focus:ring-emerald-500"
              />
            </div>
          </div>

          <DialogFooter className="gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={loading}
              className="border-zinc-700 text-zinc-300 hover:bg-zinc-800 hover:text-white h-9 text-xs"
            >
              Hủy
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
              {loading ? "Đang xử lý..." : getButtonText()}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
