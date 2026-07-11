"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";

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
  const [pickedQty, setPickedQty] = useState<number>(allocatedQty);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setPickedQty(allocatedQty);
    }
  }, [isOpen, allocatedQty]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (pickedQty <= 0) {
      showError("Số lượng pick phải lớn hơn 0.");
      return;
    }
    if (pickedQty > allocatedQty) {
      showError(`Số lượng pick thực tế không được vượt quá số lượng phân bổ (${allocatedQty}).`);
      return;
    }

    setSaving(true);
    try {
      await api.post(`/outbound/picks/${pickTaskId}/complete`, {
        pickedQty
      });
      showSuccess("Hoàn thành nhiệm vụ lấy hàng.");
      onSuccess();
      onClose();
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể hoàn tất nhiệm vụ lấy hàng.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[400px]">
        <DialogHeader>
          <DialogTitle>Xác nhận lấy hàng (Picking)</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">Vật tư</Label>
            <div className="col-span-2 text-sm font-semibold">{itemName}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">Số lô</Label>
            <div className="col-span-2 text-sm font-mono font-semibold">{lotNo}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">Vị trí kệ</Label>
            <div className="col-span-2 text-sm font-bold text-amber-600">{locationCode}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">Yêu cầu pick</Label>
            <div className="col-span-2 text-sm font-semibold">{allocatedQty}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label htmlFor="pickedQty" className="text-right">Thực tế pick</Label>
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
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving}>Xác nhận pick</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
