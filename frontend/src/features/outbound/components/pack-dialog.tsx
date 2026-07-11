"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";

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
  const [weight, setWeight] = useState<number>(0);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setPackageNo(`PKG-${shipmentNo}-${Date.now().toString().slice(-4)}`);
      setWeight(0);
    }
  }, [isOpen, shipmentNo]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!packageNo.trim()) {
      showError("Vui lòng nhập mã kiện hàng.");
      return;
    }
    if (weight <= 0) {
      showError("Cân nặng phải lớn hơn 0.");
      return;
    }

    setSaving(true);
    try {
      await api.post(`/outbound/packing/${shipmentId}/complete`, {
        packageNo: packageNo.trim(),
        weight
      });
      showSuccess("Hoàn thành đóng gói đơn xuất.");
      onSuccess();
      onClose();
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể hoàn thành đóng gói.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[400px]">
        <DialogHeader>
          <DialogTitle>Xác nhận đóng gói (Packing)</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-3 items-center gap-4">
            <Label className="text-right">Mã đơn xuất</Label>
            <div className="col-span-2 text-sm font-semibold">{shipmentNo}</div>
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label htmlFor="packageNo" className="text-right">Mã kiện hàng</Label>
            <Input
              id="packageNo"
              value={packageNo}
              onChange={(e) => setPackageNo(e.target.value)}
              className="col-span-2"
              autoFocus
            />
          </div>
          <div className="grid grid-cols-3 items-center gap-4">
            <Label htmlFor="weight" className="text-right">Cân nặng (kg)</Label>
            <Input
              id="weight"
              type="number"
              step="any"
              min={0.0001}
              value={weight}
              onChange={(e) => setWeight(parseFloat(e.target.value) || 0)}
              className="col-span-2"
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving}>Xác nhận đóng gói</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
