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
  const t = useTranslations("Features.outbound");
  const tc = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

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
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadMasterDataFailed"));
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
      copy[index] = { ...copy[index], [field]: value } as SelectedItem;
      return copy;
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!shipmentNo.trim()) {
      showApiErrorToast(t("errors.shipmentNoRequired"), t("errors.shipmentNoRequired"));
      return;
    }
    if (!partnerId) {
      showApiErrorToast(t("errors.partnerRequired"), t("errors.partnerRequired"));
      return;
    }
    if (selectedItems.some(i => !i.itemId || !i.uomId || i.requestedQty <= 0)) {
      showApiErrorToast(t("errors.itemsIncomplete"), t("errors.itemsIncomplete"));
      return;
    }

    setSaving(true);
    try {
      await api.post("/outbound/shipments", {
        shipmentNo: shipmentNo.trim(),
        partnerId,
        items: selectedItems
      });
      showSuccess(t("toastCreateSuccess"));
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-2xl max-h-[85vh] flex flex-col overflow-x-hidden">
        <DialogHeader>
          <DialogTitle>{t("createTitle")}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-4 flex-1 overflow-y-auto pr-2">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="shipmentNo">{t("shipmentNo")}</Label>
              <Input
                id="shipmentNo"
                value={shipmentNo}
                onChange={(e) => setShipmentNo(e.target.value)}
                placeholder={t("shipmentNoPlaceholder")}
              />
            </div>
            <div>
              <Label htmlFor="partnerId">{t("partner")}</Label>
              <select
                id="partnerId"
                value={partnerId}
                onChange={(e) => setPartnerId(e.target.value)}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">{t("selectPartner")}</option>
                {partners.map(p => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="space-y-2 border-t pt-4">
            <div className="flex items-center justify-between">
              <Label className="text-sm font-semibold">{t("itemList")}</Label>
              <Button type="button" size="sm" variant="outline" onClick={addItemRow} className="gap-1 text-xs">
                <Plus className="h-3 w-3" />
                {t("addRow")}
              </Button>
            </div>

            {selectedItems.map((item, idx) => (
              <div key={idx} className="flex flex-col gap-3 border-b pb-2 sm:flex-row sm:items-end">
                <div className="min-w-0 flex-1">
                  {idx === 0 && <Label className="text-xs">{t("item")}</Label>}
                  <select
                    value={item.itemId}
                    onChange={(e) => updateItemRow(idx, "itemId", e.target.value)}
                    className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none"
                  >
                    <option value="">{t("selectItem")}</option>
                    {products.map(p => (
                      <option key={p.id} value={p.id}>{p.name} ({p.code})</option>
                    ))}
                  </select>
                </div>

                <div className="w-full sm:min-w-[9rem] sm:w-40">
                  {idx === 0 && <Label className="text-xs">{t("uom")}</Label>}
                  <select
                    value={item.uomId}
                    onChange={(e) => updateItemRow(idx, "uomId", e.target.value)}
                    className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none"
                  >
                    <option value="">{t("uomPlaceholder")}</option>
                    {uoms.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>

                <div className="w-full sm:min-w-[7rem] sm:w-28">
                  {idx === 0 && <Label className="text-xs">{t("quantity")}</Label>}
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
                  className="text-red-500 hover:text-red-700 hover:bg-red-50 shrink-0"
                  disabled={selectedItems.length === 1}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </div>

          <DialogFooter className="border-t pt-4">
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>{tc("cancel")}</Button>
            <Button type="submit" disabled={saving}>{t("createShipment")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
