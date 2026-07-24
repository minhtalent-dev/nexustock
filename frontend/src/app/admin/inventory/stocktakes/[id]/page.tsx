"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState, use } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { AlertCircle, ArrowLeft, Check, Lock, Play, Send } from "lucide-react";
import { EntityAttachmentsPanel } from "@/features/files/entity-attachments-panel";

interface StocktakeItem {
  id: string;
  locationId: string;
  locationCode: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  lotNo: string;
  systemQty: number;
  countedQty: number | null;
  varianceQty: number | null;
  status: string;
}

interface Stocktake {
  id: string;
  stocktakeNo: string;
  status: string;
  zoneId: string | null;
  totalVarianceAmount: number;
  currentApprovalLevel: number;
  startedAt: string | null;
  startedBy: string | null;
  completedAt: string | null;
  completedBy: string | null;
  createdAt: string;
  createdBy: string;
}

interface DetailsResponse {
  stocktake: Stocktake;
  items: StocktakeItem[];
}

export default function StocktakeDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const t = useTranslations("Admin.stocktakes");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [stocktake, setStocktake] = useState<Stocktake | null>(null);
  const [items, setItems] = useState<StocktakeItem[]>([]);
  const [loading, setLoading] = useState(true);

  const [selectedItem, setSelectedItem] = useState<StocktakeItem | null>(null);
  const [countedQty, setCountedQty] = useState("");
  const [countingModalOpen, setCountingModalOpen] = useState(false);

  const [reasonCode, setReasonCode] = useState("ADJ-COUNT");
  const [remarks, setRemarks] = useState("");
  const [approveModalOpen, setApproveModalOpen] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  const fetchDetails = useCallback(async () => {
    try {
      const res = await api.get<DetailsResponse>(`/stocktakes/${id}`);
      if (res.data) {
        setStocktake(res.data.stocktake);
        setItems(res.data.items || []);
      }
    } catch {
      showApiErrorToast("", t("errors.loadDetailFailed"));
    } finally {
      setLoading(false);
    }
  }, [id, t]);

  useEffect(() => {
    queueMicrotask(() => void fetchDetails());
  }, [fetchDetails]);

  const handleStart = async () => {
    setActionLoading(true);
    try {
      await api.post(`/stocktakes/${id}/start`);
      showSuccess(t("toastStartSuccess"));
      fetchDetails();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.startFailed"));
    } finally {
      setActionLoading(false);
    }
  };

  const handleOpenCountModal = (item: StocktakeItem) => {
    setSelectedItem(item);
    setCountedQty(item.countedQty !== null ? item.countedQty.toString() : "");
    setCountingModalOpen(true);
  };

  const handleSaveCount = async () => {
    if (!selectedItem) return;
    const qty = parseFloat(countedQty);
    if (isNaN(qty) || qty < 0) {
      showApiErrorToast("", t("errors.countQtyInvalid"));
      return;
    }

    setActionLoading(true);
    try {
      await api.post(`/stocktakes/${id}/count`, {
        locationId: selectedItem.locationId,
        itemId: selectedItem.itemId,
        lotNo: selectedItem.lotNo,
        countedQty: qty,
      });
      showSuccess(t("toastCountSuccess"));
      setCountingModalOpen(false);
      fetchDetails();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.countFailed"));
    } finally {
      setActionLoading(false);
    }
  };

  const handleSubmitApprove = async () => {
    setActionLoading(true);
    try {
      const res = await api.post(`/stocktakes/${id}/approve`, {
        reasonCode,
        remarks,
      });
      showSuccess(res.data.message || t("toastActionSuccess"));
      setApproveModalOpen(false);
      fetchDetails();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.approveFailed"));
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case "Draft":
        return t("statusDetailDraft");
      case "Counting":
        return t("statusDetailCounting");
      case "Pending_L1_Approve":
        return t("statusDetailPendingL1");
      case "Pending_L2_Approve":
        return t("statusDetailPendingL2");
      case "Pending_L3_Approve":
        return t("statusDetailPendingL3");
      case "Approved":
        return t("statusDetailApproved");
      case "Cancelled":
        return t("statusDetailCancelled");
      default:
        return status;
    }
  };

  if (loading) return <div className="p-6 text-center text-muted-foreground">{t("loadingDetail")}</div>;
  if (!stocktake) return <div className="p-6 text-center text-red-500">{t("notFound")}</div>;

  return (
    <PageShell className="gap-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon"
            render={<Link href="/admin/inventory/stocktakes" />}
            nativeButton={false}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <h1 className="text-2xl font-bold">{t("detailTitle", { stocktakeNo: stocktake.stocktakeNo })}</h1>
        </div>
        <div className="flex gap-2">
          {stocktake.status === "Draft" && (
            <Button onClick={handleStart} disabled={actionLoading} className="gap-2">
              <Play className="h-4 w-4" />
              {t("startCount")}
            </Button>
          )}

          {stocktake.status === "Counting" && (
            <Button onClick={() => setApproveModalOpen(true)} disabled={actionLoading} className="gap-2">
              <Send className="h-4 w-4" />
              {t("submitVariance")}
            </Button>
          )}

          {stocktake.status.startsWith("Pending_") && (
            <Button onClick={() => setApproveModalOpen(true)} disabled={actionLoading} className="gap-2 bg-green-600 hover:bg-green-700 text-white">
              <Check className="h-4 w-4" />
              {t("approveAdjustment")}
            </Button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="md:col-span-1">
          <CardHeader>
            <CardTitle>{t("generalInfoTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <Label className="text-muted-foreground">{t("statusLabel")}</Label>
              <div className="text-lg font-semibold">{getStatusText(stocktake.status)}</div>
            </div>
            <div>
              <Label className="text-muted-foreground">{t("estimatedVarianceLabel")}</Label>
              <div className="text-lg font-mono font-bold text-red-600">
                {stocktake.totalVarianceAmount.toLocaleString()} {t("currencySuffix")}
              </div>
            </div>
            <div>
              <Label className="text-muted-foreground">{t("approvalLevelLabel")}</Label>
              <div className="text-lg font-semibold">
                {stocktake.currentApprovalLevel > 0
                  ? t("approvalLevel", { level: stocktake.currentApprovalLevel })
                  : t("approvalLevelUnknown")}
              </div>
            </div>
            <div>
              <Label className="text-muted-foreground">{t("createdByLabel")}</Label>
              <div className="font-medium">{stocktake.createdBy}</div>
            </div>
            <div>
              <Label className="text-muted-foreground">{t("startedAtLabel")}</Label>
              <div className="font-medium">
                {stocktake.startedAt ? new Date(stocktake.startedAt).toLocaleString() : t("notStarted")}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="md:col-span-2">
          <CardHeader>
            <CardTitle>{t("itemsTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            {stocktake.status === "Draft" ? (
              <div className="py-8 text-center text-muted-foreground flex flex-col items-center gap-2">
                <Lock className="h-8 w-8 text-muted-foreground" />
                <span>{t("draftHint")}</span>
              </div>
            ) : items.length === 0 ? (
              <div className="py-8 text-center text-muted-foreground">{t("itemsEmpty")}</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("colLocation")}</TableHead>
                    <TableHead>{t("colItem")}</TableHead>
                    <TableHead>{t("colLot")}</TableHead>
                    <TableHead className="text-right">{t("colSystemQty")}</TableHead>
                    <TableHead className="text-right">{t("colCountedQty")}</TableHead>
                    <TableHead className="text-right">{t("colVariance")}</TableHead>
                    <TableHead className="text-right">{t("colActions")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((item) => {
                    const variance = item.varianceQty;
                    let varianceColor = "text-gray-900";
                    if (variance !== null) {
                      if (variance > 0) varianceColor = "text-green-600 font-bold";
                      if (variance < 0) varianceColor = "text-red-600 font-bold";
                    }

                    return (
                      <TableRow key={item.id}>
                        <TableCell className="font-mono font-semibold">{item.locationCode}</TableCell>
                        <TableCell>
                          <div>{item.itemName}</div>
                          <div className="text-xs text-muted-foreground">{item.itemCode}</div>
                        </TableCell>
                        <TableCell className="font-mono">{item.lotNo}</TableCell>
                        <TableCell className="text-right font-mono">{item.systemQty.toLocaleString()}</TableCell>
                        <TableCell className="text-right font-mono font-semibold">
                          {item.countedQty !== null ? item.countedQty.toLocaleString() : "—"}
                        </TableCell>
                        <TableCell className={`text-right font-mono ${varianceColor}`}>
                          {variance !== null ? (variance > 0 ? `+${variance.toLocaleString()}` : variance.toLocaleString()) : "—"}
                        </TableCell>
                        <TableCell className="text-right">
                          {stocktake.status === "Counting" && (
                            <Button size="sm" variant="outline" onClick={() => handleOpenCountModal(item)}>
                              {t("countBtn")}
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="mt-6">
        <EntityAttachmentsPanel entityType="STOCKTAKE" entityId={id} />
      </div>

      <Dialog open={countingModalOpen} onOpenChange={setCountingModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("countDialogTitle")}</DialogTitle>
          </DialogHeader>
          {selectedItem && (
            <div className="space-y-4 py-4">
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div>
                  <span className="text-muted-foreground">{t("locationField")}</span>{" "}
                  <span className="font-semibold">{selectedItem.locationCode}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">{t("lotField")}</span>{" "}
                  <span className="font-semibold">{selectedItem.lotNo}</span>
                </div>
                <div className="col-span-2">
                  <span className="text-muted-foreground">{t("itemField")}</span>{" "}
                  <span className="font-semibold">
                    {selectedItem.itemName} ({selectedItem.itemCode})
                  </span>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="countedQty">{t("countedQtyLabel")}</Label>
                <Input
                  id="countedQty"
                  type="number"
                  step="any"
                  value={countedQty}
                  onChange={(e) => setCountedQty(e.target.value)}
                  placeholder={t("countedQtyPlaceholder")}
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCountingModalOpen(false)}>
              {tc("cancel")}
            </Button>
            <Button onClick={handleSaveCount} disabled={actionLoading}>
              {t("saveCount")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={approveModalOpen} onOpenChange={setApproveModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {stocktake.status === "Counting" ? t("approveDialogSubmitTitle") : t("approveDialogApproveTitle")}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            {stocktake.status === "Counting" ? (
              <div className="flex gap-2 text-sm text-amber-600 bg-amber-50 p-3 rounded">
                <AlertCircle className="h-5 w-5 shrink-0" />
                <span>{t("submitVarianceHint")}</span>
              </div>
            ) : (
              <div className="space-y-3">
                <div className="text-sm font-semibold">
                  {t("varianceToApprove", {
                    amount: `${stocktake.totalVarianceAmount.toLocaleString()} ${t("currencySuffix")}`,
                    level: stocktake.currentApprovalLevel,
                  })}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="reason">{t("reasonCodeLabel")}</Label>
                  <Select onValueChange={(val) => setReasonCode(val)} defaultValue="ADJ-COUNT">
                    <SelectTrigger>
                      <SelectValue placeholder={t("reasonCodePlaceholder")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ADJ-COUNT">{t("reasonAdjCount")}</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="remarks">{t("remarksLabel")}</Label>
              <Input
                id="remarks"
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                placeholder={t("remarksPlaceholder")}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveModalOpen(false)}>
              {tc("cancel")}
            </Button>
            <Button onClick={handleSubmitApprove} disabled={actionLoading} className="bg-green-600 hover:bg-green-700 text-white">
              {stocktake.status === "Counting" ? t("submitForApproval") : t("approveAndApply")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PageShell>
  );
}
