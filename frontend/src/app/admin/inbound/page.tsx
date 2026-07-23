"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { Plus, ClipboardList, Eye } from "lucide-react";

interface InboundOrderResponseDto {
  id: string;
  orderNo: string;
  partnerId: string;
  partnerName: string;
  status: string;
  createdAt: string;
  createdBy: string;
  items: InboundOrderItemResponseDto[];
}

interface InboundOrderItemResponseDto {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  uomId: string;
  uomName: string;
  expectedQty: number;
  receivedQty: number;
  tolerance: number;
}

interface PartnerDto {
  id: string;
  name: string;
  code: string;
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

interface OrderItemInput {
  itemId: string;
  uomId: string;
  expectedQty: number;
  tolerance: number;
}

export default function InboundPage() {
  const t = useTranslations("Admin.inbound");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [orders, setOrders] = useState<InboundOrderResponseDto[]>([]);
  const [partners, setPartners] = useState<PartnerDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [uoms, setUoms] = useState<UomDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState("");

  const [isOpen, setIsOpen] = useState(false);
  const [partnerId, setPartnerId] = useState("");
  const [orderNo, setOrderNo] = useState("");
  const [items, setItems] = useState<OrderItemInput[]>([]);
  const [saving, setSaving] = useState(false);

  const fetchOrders = useCallback(async () => {
    setLoading(true);
    try {
      const url = statusFilter ? `/inbound/orders?status=${statusFilter}` : "/inbound/orders";
      const res = await api.get<InboundOrderResponseDto[]>(url);
      setOrders(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadOrdersFailed"));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, t, tErrors]);

  const fetchMetadata = useCallback(async () => {
    try {
      const [partnersRes, productsRes, uomsRes] = await Promise.all([
        api.get<{ items: PartnerDto[] }>("/master-data/partners"),
        api.get<{ items: ProductDto[] }>("/master-data/products"),
        api.get<{ items: UomDto[] }>("/master-data/uoms"),
      ]);
      setPartners(partnersRes.data.items || []);
      setProducts(productsRes.data.items || []);
      setUoms(uomsRes.data.items || []);
    } catch {
      showApiErrorToast("", t("errors.loadMetadataFailed"));
    }
  }, [t]);

  useEffect(() => {
    queueMicrotask(() => void fetchOrders());
  }, [fetchOrders]);

  useEffect(() => {
    queueMicrotask(() => void fetchMetadata());
  }, [fetchMetadata]);

  const openCreate = () => {
    setPartnerId("");
    setOrderNo("");
    setItems([{ itemId: "", uomId: "", expectedQty: 1, tolerance: 0 }]);
    setIsOpen(true);
  };

  const addItemRow = () => {
    setItems((prev) => [...prev, { itemId: "", uomId: "", expectedQty: 1, tolerance: 0 }]);
  };

  const removeItemRow = (index: number) => {
    setItems((prev) => prev.filter((_, i) => i !== index));
  };

  const updateItemRow = <K extends keyof OrderItemInput>(index: number, field: K, value: OrderItemInput[K]) => {
    setItems((prev) =>
      prev.map((item, i) => (i === index ? { ...item, [field]: value } : item))
    );
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!partnerId) {
      showApiErrorToast("", t("errors.partnerRequired"));
      return;
    }
    if (items.length === 0) {
      showApiErrorToast("", t("errors.itemsRequired"));
      return;
    }
    const invalidItem = items.some((item) => !item.itemId || !item.uomId || item.expectedQty <= 0);
    if (invalidItem) {
      showApiErrorToast("", t("errors.itemFieldsRequired"));
      return;
    }

    setSaving(true);
    try {
      await api.post("/inbound/orders", {
        orderNo: orderNo || undefined,
        partnerId,
        items,
      });
      showSuccess(t("toastCreateSuccess"));
      setIsOpen(false);
      fetchOrders();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setSaving(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status.toUpperCase()) {
      case "COMPLETED":
        return <Badge className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">{t("statusCompleted")}</Badge>;
      case "RECEIVING":
        return <Badge className="bg-amber-500/10 text-amber-500 border-amber-500/20">{t("statusReceiving")}</Badge>;
      case "OPEN":
        return <Badge className="bg-blue-500/10 text-blue-500 border-blue-500/20">{t("statusOpen")}</Badge>;
      case "CANCELLED":
        return <Badge className="bg-zinc-500/10 text-muted-foreground border-zinc-500/20">{t("statusCancelled")}</Badge>;
      default:
        return <Badge className="bg-zinc-500/10 text-muted-foreground border-zinc-500/20">{status}</Badge>;
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground flex items-center gap-3">
            <ClipboardList className="h-6 w-6 text-emerald-500" />
            {t("title")}
          </h1>
          <p className="text-xs text-muted-foreground mt-1">{t("subtitle")}</p>
        </div>
        <Button onClick={openCreate} className="bg-emerald-600 hover:bg-emerald-500 text-foreground gap-2 h-9 text-sm">
          <Plus className="h-4 w-4" />
          {t("createOrder")}
        </Button>
      </div>

      <div className="flex gap-4">
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="flex h-9 w-48 rounded-md border border-border bg-background px-3 py-1 text-sm shadow-sm transition-colors text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
        >
          <option value="">{t("filterAllStatuses")}</option>
          <option value="Open">{t("statusOpen")}</option>
          <option value="Receiving">{t("statusReceiving")}</option>
          <option value="Completed">{t("statusCompleted")}</option>
          <option value="Cancelled">{t("statusCancelled")}</option>
        </select>
      </div>

      <Card className="bg-card border-border/80">
        <CardHeader className="py-4 border-b border-border/60 flex flex-row items-center justify-between">
          <CardTitle className="text-sm font-semibold text-foreground">{t("listTitle")}</CardTitle>
          {loading && <div className="h-4 w-4 animate-spin rounded-full border-2 border-emerald-500 border-t-transparent" />}
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader className="bg-card/30 border-b border-border/60">
              <TableRow className="hover:bg-transparent">
                <TableHead className="text-muted-foreground font-semibold h-11">{t("colOrderNo")}</TableHead>
                <TableHead className="text-muted-foreground font-semibold h-11">{t("colPartner")}</TableHead>
                <TableHead className="text-muted-foreground font-semibold h-11">{t("colCreatedAt")}</TableHead>
                <TableHead className="text-muted-foreground font-semibold h-11">{t("colCreatedBy")}</TableHead>
                <TableHead className="text-muted-foreground font-semibold h-11 text-center w-36">{t("colStatus")}</TableHead>
                <TableHead className="text-muted-foreground font-semibold h-11 text-right w-24 pr-6">{t("colActions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orders.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-muted-foreground py-12">
                    {t("empty")}
                  </TableCell>
                </TableRow>
              ) : (
                orders.map((o) => (
                  <TableRow key={o.id} className="border-b border-border/50 hover:bg-card/20">
                    <TableCell className="text-foreground font-medium">{o.orderNo}</TableCell>
                    <TableCell className="text-muted-foreground">{o.partnerName}</TableCell>
                    <TableCell className="text-muted-foreground">{new Date(o.createdAt).toLocaleString("vi-VN")}</TableCell>
                    <TableCell className="text-muted-foreground">{o.createdBy || tc("system")}</TableCell>
                    <TableCell className="text-center">{getStatusBadge(o.status)}</TableCell>
                    <TableCell className="text-right pr-6">
                      <Link href={`/admin/inbound/${o.id}/receive`}>
                        <Button variant="ghost" className="h-8 w-8 p-0 text-muted-foreground hover:text-emerald-500 hover:bg-muted/50">
                          <Eye className="h-4 w-4" />
                        </Button>
                      </Link>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="bg-background border-border text-foreground max-w-3xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="text-foreground flex items-center gap-2">
              <Plus className="h-5 w-5 text-emerald-500" />
              {t("createDialogTitle")}
            </DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSave} className="space-y-6">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="orderNo" className="text-muted-foreground text-xs">{t("orderNoLabel")}</Label>
                <Input
                  id="orderNo"
                  placeholder={t("orderNoPlaceholder")}
                  value={orderNo}
                  onChange={(e) => setOrderNo(e.target.value)}
                  className="bg-card border-border text-foreground focus-visible:ring-emerald-500"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="partner" className="text-muted-foreground text-xs">{t("partnerLabel")}</Label>
                <select
                  id="partner"
                  value={partnerId}
                  onChange={(e) => setPartnerId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-border bg-card px-3 py-1 text-sm shadow-sm transition-colors text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                >
                  <option value="">{t("partnerPlaceholder")}</option>
                  {partners.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.code})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="space-y-4">
              <div className="flex items-center justify-between border-b border-border pb-2">
                <Label className="text-foreground text-sm font-semibold">{t("lineItemsTitle")}</Label>
                <Button type="button" onClick={addItemRow} size="sm" className="bg-muted hover:bg-zinc-700 text-foreground text-xs gap-1.5 h-8">
                  <Plus className="h-3.5 w-3.5" />
                  {t("addLine")}
                </Button>
              </div>

              {items.map((item, index) => (
                <div key={index} className="grid grid-cols-12 gap-3 items-end bg-card/30 p-3 rounded-lg border border-zinc-850">
                  <div className="col-span-4 space-y-1">
                    <Label className="text-muted-foreground text-[10px]">{t("itemLabel")}</Label>
                    <select
                      value={item.itemId}
                      onChange={(e) => updateItemRow(index, "itemId", e.target.value)}
                      className="flex h-9 w-full rounded-md border border-border bg-card px-2 py-1 text-xs text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                    >
                      <option value="">{t("itemPlaceholder")}</option>
                      {products.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.code})
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-span-3 space-y-1">
                    <Label className="text-muted-foreground text-[10px]">{t("uomLabel")}</Label>
                    <select
                      value={item.uomId}
                      onChange={(e) => updateItemRow(index, "uomId", e.target.value)}
                      className="flex h-9 w-full rounded-md border border-border bg-card px-2 py-1 text-xs text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                    >
                      <option value="">{t("uomPlaceholder")}</option>
                      {uoms.map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-span-2 space-y-1">
                    <Label className="text-muted-foreground text-[10px]">{t("expectedQtyLabel")}</Label>
                    <Input
                      type="number"
                      min={0.01}
                      step="any"
                      value={item.expectedQty}
                      onChange={(e) => updateItemRow(index, "expectedQty", parseFloat(e.target.value) || 0)}
                      className="h-9 bg-card border-border text-xs text-foreground focus-visible:ring-emerald-500"
                    />
                  </div>
                  <div className="col-span-2 space-y-1">
                    <Label className="text-muted-foreground text-[10px]">{t("toleranceLabel")}</Label>
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      step={1}
                      value={item.tolerance * 100}
                      onChange={(e) => updateItemRow(index, "tolerance", (parseFloat(e.target.value) || 0) / 100)}
                      className="h-9 bg-card border-border text-xs text-foreground focus-visible:ring-emerald-500"
                    />
                  </div>
                  <div className="col-span-1 text-right">
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => removeItemRow(index)}
                      className="h-9 w-9 p-0 text-muted-foreground hover:text-red-400 hover:bg-muted"
                    >
                      X
                    </Button>
                  </div>
                </div>
              ))}
            </div>

            <DialogFooter className="border-t border-border pt-4 flex gap-2">
              <Button type="button" variant="ghost" onClick={() => setIsOpen(false)} className="text-muted-foreground hover:text-foreground">
                {tc("cancel")}
              </Button>
              <Button type="submit" disabled={saving} className="bg-emerald-600 hover:bg-emerald-500 text-foreground min-w-24">
                {saving ? tc("saving") : tc("confirm")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </PageShell>
  );
}
