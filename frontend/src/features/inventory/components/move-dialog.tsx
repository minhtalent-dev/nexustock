"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";

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
  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [reasons, setReasons] = useState<ReasonDto[]>([]);
  const [toLocationId, setToLocationId] = useState("");
  const [qty, setQty] = useState<number>(maxQty);
  const [reasonCode, setReasonCode] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchLocations = async () => {
    try {
      const res = await api.get<{ items: LocationDto[] }>("/master-data/storage-locations");
      // Filter out the source location
      const list = (res.data.items || []).filter(l => l.id !== fromLocationId);
      setLocations(list);
    } catch (err) {
      showError("Không thể tải danh sách vị trí kho.");
    }
  };

  const fetchReasons = async () => {
    try {
      const res = await api.get<{ items: ReasonDto[] }>("/master-data/reasons");
      setReasons(res.data.items || []);
    } catch (err) {
      // Fallback local reasons if API fails
      setReasons([
        { id: "1", code: "ROUTINE_QC", name: "Kiểm tra chất lượng định kỳ" },
        { id: "2", code: "OPTIMIZE_SPACE", name: "Tối ưu hóa không gian kệ" },
        { id: "3", code: "DAMAGE_COMPROMISE", name: "Nghi ngờ hư hại sản phẩm" }
      ]);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchLocations();
      fetchReasons();
      setQty(maxQty);
      setToLocationId("");
      setReasonCode("");
    }
  }, [isOpen, fromLocationId, maxQty]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!toLocationId) {
      showError("Vui lòng chọn vị trí đích.");
      return;
    }
    if (qty <= 0) {
      showError("Số lượng dịch chuyển phải lớn hơn 0.");
      return;
    }
    if (qty > maxQty) {
      showError(`Số lượng dịch chuyển không được vượt quá tồn khả dụng (${maxQty}).`);
      return;
    }
    if (!reasonCode) {
      showError("Vui lòng chọn lý do dịch chuyển.");
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
      showSuccess("Dịch chuyển tồn kho thành công.");
      onSuccess();
      onClose();
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể thực hiện dịch chuyển.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Dịch chuyển tồn kho</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">Vật tư</Label>
            <div className="col-span-3 text-sm font-semibold">{itemName}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">Số lô</Label>
            <div className="col-span-3 text-sm font-semibold">{lotNo}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label className="text-right">Vị trí nguồn</Label>
            <div className="col-span-3 text-sm font-semibold text-amber-600">{fromLocationCode}</div>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="toLocationId" className="text-right">Vị trí đích</Label>
            <select
              id="toLocationId"
              value={toLocationId}
              onChange={(e) => setToLocationId(e.target.value)}
              className="col-span-3 flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Chọn vị trí đích...</option>
              {locations.map((l) => (
                <option key={l.id} value={l.id}>{l.code}</option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="qty" className="text-right">Số lượng</Label>
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
            <Button type="submit" disabled={saving}>Xác nhận chuyển</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
