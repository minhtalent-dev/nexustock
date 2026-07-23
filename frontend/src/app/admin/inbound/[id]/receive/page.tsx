"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { ArrowLeft, CheckCircle2, AlertTriangle, Plus, ShieldAlert } from "lucide-react";

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

interface LocationDto {
  id: string;
  name: string;
  code: string;
}

export default function ReceivePage() {
  const t = useTranslations("Admin.inbound");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const params = useParams();
  const orderId = params.id as string;

  const [order, setOrder] = useState<InboundOrderResponseDto | null>(null);
  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [loading, setLoading] = useState(true);

  const [isOpen, setIsOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState<InboundOrderItemResponseDto | null>(null);
  const [lotNo, setLotNo] = useState("");
  const [receivedQty, setReceivedQty] = useState(0);
  const [toLocationId, setToLocationId] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [productionDate, setProductionDate] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchOrderDetails = useCallback(async () => {
    try {
      const res = await api.get<InboundOrderResponseDto>(`/inbound/orders/${orderId}`);
      setOrder(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadDetailFailed"));
    }
  }, [orderId, t, tErrors]);

  const fetchLocations = useCallback(async () => {
    try {
      const res = await api.get<{ items: LocationDto[] }>("/master-data/storage-locations");
      setLocations(res.data.items || []);
    } catch {
      showApiErrorToast("", t("errors.loadLocationsFailed"));
    }
  }, [t]);

  useEffect(() => {
    queueMicrotask(() => {
      const init = async () => {
        setLoading(true);
        await Promise.all([fetchOrderDetails(), fetchLocations()]);
        setLoading(false);
      };
      void init();
    });
  }, [fetchOrderDetails, fetchLocations]);

  const openReceiveDialog = (item: InboundOrderItemResponseDto) => {
    setSelectedItem(item);
    setLotNo("");
    const remain = Math.max(0, item.expectedQty - item.receivedQty);
    setReceivedQty(remain);
    setToLocationId("");
    setExpiryDate("");
    setProductionDate("");
    setIsOpen(true);
  };

  const handleReceive = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    if (!lotNo.trim()) {
      showApiErrorToast("", t("errors.lotRequired"));
      return;
    }
    if (receivedQty <= 0) {
      showApiErrorToast("", t("errors.qtyRequired"));
      return;
    }
    if (!toLocationId) {
      showApiErrorToast("", t("errors.locationRequired"));
      return;
    }

    setSaving(true);
    try {
      await api.post(`/inbound/orders/${orderId}/receive`, {
        itemId: selectedItem.itemId,
        lotNo,
        receivedQty,
        toLocationId,
        expiryDate: expiryDate ? new Date(expiryDate).toISOString() : null,
        productionDate: productionDate ? new Date(productionDate).toISOString() : null,
      });

      showSuccess(t("toastReceiveSuccess", { itemName: selectedItem.itemName }));
      setIsOpen(false);
      await fetchOrderDetails();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.receiveFailed"));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4 text-muted-foreground">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-emerald-500 border-t-transparent" />
        <span className="text-sm">{t("loadingDetail")}</span>
      </div>
    );
  }

  if (!order) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4 text-muted-foreground">
        <AlertTriangle className="h-12 w-12 text-red-500" />
        <span className="text-sm">{t("notFound")}</span>
        <Link href="/admin/inbound">
          <Button className="bg-muted hover:bg-zinc-700 text-foreground gap-2">
            <ArrowLeft className="h-4 w-4" />
            {t("backToList")}
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <PageShell className="gap-6">
      <div className="flex items-center gap-4">
        <Link href="/admin/inbound">
          <Button variant="ghost" className="h-9 w-9 p-0 border border-border hover:bg-zinc-850 text-muted-foreground hover:text-foreground">
            <ArrowLeft className="h-4 w-4" />
          </Button>
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-foreground flex items-center gap-3">
            {t("receiveTitle", { orderNo: order.orderNo })}
          </h1>
          <p className="text-xs text-muted-foreground mt-1">
            {t("receiveSubtitle", { partner: order.partnerName, status: order.status })}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-3 gap-6">
        <Card className="bg-card border-border/80 col-span-3">
          <CardHeader className="py-4 border-b border-border/60">
            <CardTitle className="text-sm font-semibold text-foreground">{t("receiveLinesTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader className="bg-card/30 border-b border-border/60">
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-muted-foreground font-semibold h-11">{t("colItem")}</TableHead>
                  <TableHead className="text-muted-foreground font-semibold h-11">{t("colUom")}</TableHead>
                  <TableHead className="text-muted-foreground font-semibold h-11 text-right">{t("colExpectedQty")}</TableHead>
                  <TableHead className="text-muted-foreground font-semibold h-11 text-right">{t("colReceivedQty")}</TableHead>
                  <TableHead className="text-muted-foreground font-semibold h-11 text-right">{t("colTolerance")}</TableHead>
                  <TableHead className="text-muted-foreground font-semibold h-11 text-right">{t("colProgress")}</TableHead>
                  <TableHead className="text-muted-foreground font-semibold h-11 text-right w-32 pr-6">{t("colActions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {order.items.map((i) => {
                  const progressPercent = Math.min(100, Math.round((i.receivedQty / i.expectedQty) * 100)) || 0;
                  const isCompleted = i.receivedQty >= i.expectedQty;

                  return (
                    <TableRow key={i.id} className="border-b border-border/50 hover:bg-card/20">
                      <TableCell className="text-foreground font-medium">
                        <div>
                          <p>{i.itemName}</p>
                          <p className="text-[10px] text-muted-foreground font-normal">{i.itemCode}</p>
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{i.uomName}</TableCell>
                      <TableCell className="text-right text-muted-foreground font-mono">{i.expectedQty}</TableCell>
                      <TableCell className="text-right text-emerald-400 font-mono font-semibold">{i.receivedQty}</TableCell>
                      <TableCell className="text-right text-muted-foreground font-mono">{(i.tolerance * 100).toFixed(0)}%</TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-2">
                          <div className="w-16 bg-muted rounded-full h-1.5 overflow-hidden">
                            <div
                              className={`h-full rounded-full ${isCompleted ? "bg-emerald-500" : "bg-amber-500"}`}
                              style={{ width: `${progressPercent}%` }}
                            />
                          </div>
                          <span className="text-xs font-mono text-muted-foreground">{progressPercent}%</span>
                        </div>
                      </TableCell>
                      <TableCell className="text-right pr-6">
                        {order.status === "Completed" || order.status === "Cancelled" ? (
                          <span className="text-xs text-muted-foreground">{tc("notAvailable")}</span>
                        ) : (
                          <Button
                            onClick={() => openReceiveDialog(i)}
                            className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-3 py-1 gap-1.5"
                          >
                            <Plus className="h-3.5 w-3.5" />
                            {t("receiveBtn")}
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="bg-background border-border text-foreground sm:max-w-2xl max-h-[85vh] overflow-y-auto overflow-x-hidden">
          <DialogHeader>
            <DialogTitle className="text-foreground flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-emerald-500" />
              {t("receiveDialogTitle")}
            </DialogTitle>
          </DialogHeader>
          {selectedItem && (
            <form onSubmit={handleReceive} className="space-y-4">
              <div className="bg-card/60 p-3 rounded-lg border border-border">
                <p className="text-xs text-muted-foreground font-semibold uppercase">{t("itemToReceive")}</p>
                <p className="text-sm font-bold text-foreground mt-0.5">{selectedItem.itemName}</p>
                <p className="text-[10px] text-muted-foreground font-normal">{t("itemCode")}: {selectedItem.itemCode}</p>
                <div className="grid grid-cols-1 gap-2 mt-3 text-xs text-muted-foreground sm:grid-cols-3">
                  <div>
                    <p className="text-muted-foreground">{t("expected")}</p>
                    <p className="font-semibold text-foreground mt-0.5 font-mono">{selectedItem.expectedQty} {selectedItem.uomName}</p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">{t("received")}</p>
                    <p className="font-semibold text-emerald-400 mt-0.5 font-mono">{selectedItem.receivedQty} {selectedItem.uomName}</p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">{t("tolerance")}</p>
                    <p className="font-semibold text-amber-500 mt-0.5 font-mono">{(selectedItem.tolerance * 100).toFixed(0)}%</p>
                  </div>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="lotNo" className="text-muted-foreground text-xs">{t("lotNoLabel")}</Label>
                <Input
                  id="lotNo"
                  placeholder={t("lotNoPlaceholder")}
                  value={lotNo}
                  onChange={(e) => setLotNo(e.target.value)}
                  className="bg-card border-border text-foreground focus-visible:ring-emerald-500"
                />
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="qty" className="text-muted-foreground text-xs">{t("receiveQtyLabel")}</Label>
                  <Input
                    id="qty"
                    type="number"
                    min={0.01}
                    step="any"
                    value={receivedQty}
                    onChange={(e) => setReceivedQty(parseFloat(e.target.value) || 0)}
                    className="bg-card border-border text-foreground focus-visible:ring-emerald-500 font-mono"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="location" className="text-muted-foreground text-xs">{t("locationLabel")}</Label>
                  <select
                    id="location"
                    value={toLocationId}
                    onChange={(e) => setToLocationId(e.target.value)}
                    className="flex h-10 w-full rounded-md border border-border bg-card px-3 py-1 text-sm shadow-sm transition-colors text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                  >
                    <option value="">{t("locationPlaceholder")}</option>
                    {locations.map((loc) => (
                      <option key={loc.id} value={loc.id}>
                        {loc.name} ({loc.code})
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="prodDate" className="text-muted-foreground text-xs">{t("productionDateLabel")}</Label>
                  <Input
                    id="prodDate"
                    type="date"
                    value={productionDate}
                    onChange={(e) => setProductionDate(e.target.value)}
                    className="bg-card border-border text-foreground focus-visible:ring-emerald-500"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="expDate" className="text-muted-foreground text-xs">{t("expiryDateLabel")}</Label>
                  <Input
                    id="expDate"
                    type="date"
                    value={expiryDate}
                    onChange={(e) => setExpiryDate(e.target.value)}
                    className="bg-card border-border text-foreground focus-visible:ring-emerald-500"
                  />
                </div>
              </div>

              {receivedQty + selectedItem.receivedQty > selectedItem.expectedQty * (1 + selectedItem.tolerance) && (
                <div className="flex gap-2 p-3 bg-red-900/10 border border-red-500/20 rounded-lg text-xs text-red-400">
                  <ShieldAlert className="h-4 w-4 shrink-0" />
                  <p>{t("toleranceWarning")}</p>
                </div>
              )}

              <DialogFooter className="border-t border-border pt-4 flex gap-2">
                <Button type="button" variant="outline" onClick={() => setIsOpen(false)} className="text-foreground">
                  {tc("cancel")}
                </Button>
                <Button type="submit" disabled={saving} className="bg-emerald-600 hover:bg-emerald-500 text-white min-w-24">
                  {saving ? tc("processing") : tc("confirm")}
                </Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </PageShell>
  );
}
