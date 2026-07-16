"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";

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
        { id: "1", code: "MAINTENANCE", name: "Bảo trì ô kệ" },
        { id: "2", code: "CLEANING", name: "Dọn dẹp vệ sinh" },
        { id: "3", code: "SUSPENDED", name: "Tạm dừng sử dụng" }
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
      showError("Vui lòng chọn lý do khóa.");
      return;
    }

    setSaving(true);
    try {
      await api.post(`/inventory/locations/${locationId}/lock`, {
        lockType,
        reasonCode,
      });
      showSuccess(`Đã khóa vị trí ${locationCode} thành công.`);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể khóa vị trí."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Khóa vị trí kệ</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">Vị trí kệ</Label>
            <div className="col-span-3 text-sm font-semibold text-red-600">{locationCode}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="lockType" className="text-right">Kiểu khóa</Label>
            <select
              id="lockType"
              value={lockType}
              onChange={(e) => setLockType(e.target.value)}
              className="col-span-3 flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="ALL">Khóa toàn bộ (ALL)</option>
              <option value="INBOUND">Khóa chiều nhập (INBOUND)</option>
              <option value="OUTBOUND">Khóa chiều xuất (OUTBOUND)</option>
            </select>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="reasonCode" className="text-right">Lý do</Label>
            <select
              id="reasonCode"
              value={reasonCode}
              onChange={(e) => setReasonCode(e.target.value)}
              className="col-span-3 flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Chọn lý do...</option>
              {reasons.map((r) => (
                <option key={r.id} value={r.code}>{r.name} ({r.code})</option>
              ))}
            </select>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving}>Xác nhận khóa</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
