"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { Plus, Trash2 } from "lucide-react";

interface PartnerDto {
  id: string;
  name: string;
}

interface ProductDto {
  id: string;
  name: string;
  code: string;
}

interface UomDto {
  id: string;
  name: string;
}

interface CreateShipmentDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

interface SelectedItem {
  itemId: string;
  uomId: string;
  requestedQty: number;
}

export function CreateShipmentDialog({ isOpen, onClose, onSuccess }: CreateShipmentDialogProps) {
  const [partners, setPartners] = useState<PartnerDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [uoms, setUoms] = useState<UomDto[]>([]);

  const [shipmentNo, setShipmentNo] = useState("");
  const [partnerId, setPartnerId] = useState("");
  const [selectedItems, setSelectedItems] = useState<SelectedItem[]>([
    { itemId: "", uomId: "", requestedQty: 1 }
  ]);
  const [saving, setSaving] = useState(false);

  const fetchMasterData = async () => {
    try {
      const partnerRes = await api.get<{ items: PartnerDto[] }>("/master-data/partners");
      setPartners(partnerRes.data.items || []);

      const prodRes = await api.get<{ items: ProductDto[] }>("/master-data/products");
      setProducts(prodRes.data.items || []);

      const uomRes = await api.get<{ items: UomDto[] }>("/master-data/uoms");
      setUoms(uomRes.data.items || []);
    } catch {
      showError("Không thể tải dữ liệu danh mục.");
    }
  };

  useEffect(() => {
    if (isOpen) {
      queueMicrotask(() => {
        void fetchMasterData();
        setShipmentNo(`SO-${Date.now()}`);
        setPartnerId("");
        setSelectedItems([{ itemId: "", uomId: "", requestedQty: 1 }]);
      });
    }
  }, [isOpen]);

  const addItemRow = () => {
    setSelectedItems(prev => [...prev, { itemId: "", uomId: "", requestedQty: 1 }]);
  };

  const removeItemRow = (index: number) => {
    if (selectedItems.length === 1) return;
    setSelectedItems(prev => prev.filter((_, i) => i !== index));
  };

  const updateItemRow = (index: number, field: keyof SelectedItem, value: string | number) => {
    setSelectedItems(prev => {
      const copy = [...prev];
      // @ts-expect-error type override
      copy[index] = { ...copy[index], [field]: value };
      return copy;
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!shipmentNo.trim()) {
      showError("Vui lòng nhập mã đơn xuất.");
      return;
    }
    if (!partnerId) {
      showError("Vui lòng chọn đối tác.");
      return;
    }
    if (selectedItems.some(i => !i.itemId || !i.uomId || i.requestedQty <= 0)) {
      showError("Vui lòng nhập đầy đủ thông tin hàng hóa và số lượng lớn hơn 0.");
      return;
    }

    setSaving(true);
    try {
      await api.post("/outbound/shipments", {
        shipmentNo: shipmentNo.trim(),
        partnerId,
        items: selectedItems
      });
      showSuccess("Tạo đơn xuất kho thành công.");
      onSuccess();
      onClose();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tạo đơn xuất."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[85vh] flex flex-col">
        <DialogHeader>
          <DialogTitle>Tạo đơn xuất kho mới</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4 flex-1 overflow-y-auto pr-2">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <Label htmlFor="shipmentNo">Mã đơn xuất</Label>
              <Input
                id="shipmentNo"
                value={shipmentNo}
                onChange={(e) => setShipmentNo(e.target.value)}
                placeholder="Nhập mã đơn..."
              />
            </div>
            <div>
              <Label htmlFor="partnerId">Khách hàng / Đối tác</Label>
              <select
                id="partnerId"
                value={partnerId}
                onChange={(e) => setPartnerId(e.target.value)}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Chọn đối tác...</option>
                {partners.map(p => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="space-y-2 border-t pt-4">
            <div className="flex items-center justify-between">
              <Label className="text-sm font-semibold">Danh sách hàng hóa</Label>
              <Button type="button" size="sm" variant="outline" onClick={addItemRow} className="gap-1 text-xs">
                <Plus className="h-3 w-3" />
                Thêm dòng
              </Button>
            </div>

            {selectedItems.map((item, idx) => (
              <div key={idx} className="flex gap-2 items-end border-b pb-2">
                <div className="flex-1">
                  {idx === 0 && <Label className="text-xs">Vật tư</Label>}
                  <select
                    value={item.itemId}
                    onChange={(e) => updateItemRow(idx, "itemId", e.target.value)}
                    className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none"
                  >
                    <option value="">Chọn vật tư...</option>
                    {products.map(p => (
                      <option key={p.id} value={p.id}>{p.name} ({p.code})</option>
                    ))}
                  </select>
                </div>

                <div className="w-24">
                  {idx === 0 && <Label className="text-xs">ĐVT</Label>}
                  <select
                    value={item.uomId}
                    onChange={(e) => updateItemRow(idx, "uomId", e.target.value)}
                    className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none"
                  >
                    <option value="">ĐVT...</option>
                    {uoms.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>

                <div className="w-24">
                  {idx === 0 && <Label className="text-xs">Số lượng</Label>}
                  <Input
                    type="number"
                    min={0.0001}
                    step="any"
                    value={item.requestedQty}
                    onChange={(e) => updateItemRow(idx, "requestedQty", parseFloat(e.target.value) || 0)}
                  />
                </div>

                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  onClick={() => removeItemRow(idx)}
                  className="text-red-500 hover:text-red-700 hover:bg-red-50"
                  disabled={selectedItems.length === 1}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </div>

          <DialogFooter className="border-t pt-4">
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving}>Tạo đơn</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
