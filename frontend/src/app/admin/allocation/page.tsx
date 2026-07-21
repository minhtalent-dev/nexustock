"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { RefreshCw, Play, Trash2, Layers, ClipboardList, CheckCircle } from "lucide-react";

interface Shipment {
  id: string;
  shipmentNo: string;
  partnerId: string;
  partnerName: string;
  status: string;
  createdAt: string;
  createdBy: string;
}

interface ShipmentLine {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  uomName: string;
  requestedQty: number;
  allocatedQty: number;
  pickedQty: number;
  status: string;
}

export default function AllocationPage() {
  const t = useTranslations("Admin.allocation");
  const tErrors = useTranslations("Errors");

  const [shipments, setShipments] = useState<Shipment[]>([]);
  const [loadingShipments, setLoadingShipments] = useState(false);
  const [activeShipment, setActiveShipment] = useState<Shipment | null>(null);
  const [shipmentLines, setShipmentLines] = useState<ShipmentLine[]>([]);
  const [loadingLines, setLoadingLines] = useState(false);

  const [strategy, setStrategy] = useState("FEFO");
  const [allowPartial, setAllowPartial] = useState(true);
  const [ttlMinutes, setTtlMinutes] = useState(1440);
  const [submitting, setSubmitting] = useState(false);

  const fetchShipments = useCallback(async () => {
    setLoadingShipments(true);
    try {
      const res = await api.get<Shipment[]>("/outbound/shipments");
      setShipments(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadShipmentsFailed"));
    } finally {
      setLoadingShipments(false);
    }
  }, [t, tErrors]);

  const fetchShipmentLines = useCallback(async (shipment: Shipment) => {
    setActiveShipment(shipment);
    setLoadingLines(true);
    try {
      const res = await api.get<{ shipment: Shipment; items: ShipmentLine[] }>(`/outbound/shipments/${shipment.id}`);
      setShipmentLines(res.data.items || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadDetailFailed"));
    } finally {
      setLoadingLines(false);
    }
  }, [t, tErrors]);

  const handleRunAllocation = async () => {
    if (!activeShipment) return;
    setSubmitting(true);
    try {
      const payload = {
        shipmentId: activeShipment.id,
        strategy,
        allowPartial,
        reservationTtlMinutes: ttlMinutes,
      };
      const res = await api.post("/allocation/reserve", payload);
      showSuccess(res.data.message || t("toastAllocateSuccess"));

      fetchShipmentLines(activeShipment);
      fetchShipments();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.allocateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleReleaseAllocation = async () => {
    if (!activeShipment) return;
    if (!confirm(t("confirmRelease"))) return;

    setSubmitting(true);
    try {
      const res = await api.post("/allocation/release", { shipmentId: activeShipment.id });
      showSuccess(res.data.message || t("toastReleaseSuccess"));

      fetchShipmentLines(activeShipment);
      fetchShipments();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.releaseFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  useEffect(() => {
    queueMicrotask(() => void fetchShipments());
  }, [fetchShipments]);

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Allocated":
        return <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white">{t("statusAllocated")}</Badge>;
      case "PartiallyAllocated":
        return <Badge className="bg-amber-600 hover:bg-amber-500 text-white">{t("statusPartiallyAllocated")}</Badge>;
      case "Unallocated":
      case "Open":
        return <Badge className="bg-zinc-800 hover:bg-zinc-700 text-zinc-300">{t("statusOpen")}</Badge>;
      default:
        return <Badge className="bg-zinc-700 text-white">{status}</Badge>;
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-3">
          <Layers className="h-6 w-6 text-emerald-500" />
          {t("title")}
        </h1>
        <p className="text-xs text-zinc-400 mt-1">{t("subtitle")}</p>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-5 gap-6">
        <div className="xl:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 border-b border-zinc-800">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <ClipboardList className="h-4 w-4 text-emerald-500" />
                {t("shipmentListTitle", { count: shipments.length })}
              </CardTitle>
              <Button variant="ghost" size="icon" onClick={fetchShipments} className="h-8 w-8 text-zinc-400 hover:text-white">
                <RefreshCw className={`h-4 w-4 ${loadingShipments ? "animate-spin" : ""}`} />
              </Button>
            </CardHeader>
            <CardContent className="pt-4">
              {loadingShipments && shipments.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">{t("loadingShipments")}</div>
              ) : shipments.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">{t("emptyShipments")}</div>
              ) : (
                <div className="overflow-x-auto max-h-[500px]">
                  <Table className="text-xs">
                    <TableHeader className="border-b border-zinc-800">
                      <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                        <TableHead className="text-zinc-400">{t("colShipmentNo")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colCustomer")}</TableHead>
                        <TableHead className="text-zinc-400 text-center">{t("colStatus")}</TableHead>
                        <TableHead className="text-zinc-400 text-center">{t("colActions")}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {shipments.map((s) => (
                        <TableRow
                          key={s.id}
                          className={`border-b border-zinc-800/50 hover:bg-zinc-800/30 ${activeShipment?.id === s.id ? "bg-zinc-800/50" : ""}`}
                        >
                          <TableCell className="font-semibold text-zinc-200">{s.shipmentNo}</TableCell>
                          <TableCell className="text-zinc-300 truncate max-w-[120px]">{s.partnerName}</TableCell>
                          <TableCell className="text-center">{getStatusBadge(s.status)}</TableCell>
                          <TableCell className="text-center">
                            <Button
                              onClick={() => fetchShipmentLines(s)}
                              className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-3 text-[11px] rounded"
                            >
                              {t("detailsBtn")}
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="xl:col-span-3 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white min-h-[400px]">
            <CardHeader className="border-b border-zinc-800 pb-2">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <CheckCircle className="h-4 w-4 text-emerald-500" />
                {t("allocationDetailTitle")}
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4">
              {!activeShipment ? (
                <div className="flex flex-col items-center justify-center py-20 text-zinc-500 text-xs gap-2">
                  <ClipboardList className="h-8 w-8 text-zinc-700 animate-bounce" />
                  {t("selectHint")}
                </div>
              ) : loadingLines ? (
                <div className="text-center py-20 text-zinc-500 text-xs">{t("loadingDetail")}</div>
              ) : (
                <div className="flex flex-col gap-6 text-xs">
                  <div className="bg-zinc-950/40 p-4 rounded-lg border border-zinc-800 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                    <div className="flex flex-wrap gap-4">
                      <div className="flex flex-col gap-1.5">
                        <label className="text-[10px] text-zinc-500">{t("strategyLabel")}</label>
                        <select
                          value={strategy}
                          onChange={(e) => setStrategy(e.target.value)}
                          className="bg-zinc-800 border border-zinc-700 text-white rounded p-1.5 text-xs focus:outline-none h-8 w-28"
                        >
                          <option value="FEFO">{t("strategyFefo")}</option>
                          <option value="FIFO">{t("strategyFifo")}</option>
                        </select>
                      </div>

                      <div className="flex flex-col gap-1.5">
                        <label className="text-[10px] text-zinc-500">{t("ttlLabel")}</label>
                        <input
                          type="number"
                          value={ttlMinutes}
                          onChange={(e) => setTtlMinutes(parseInt(e.target.value) || 1440)}
                          className="bg-zinc-800 border border-zinc-700 text-white rounded p-1.5 text-xs focus:outline-none h-8 w-24"
                        />
                      </div>

                      <div className="flex items-center gap-2 mt-4">
                        <input
                          type="checkbox"
                          id="chk_allow_partial"
                          checked={allowPartial}
                          onChange={(e) => setAllowPartial(e.target.checked)}
                          className="accent-emerald-500 h-4 w-4 cursor-pointer"
                        />
                        <label htmlFor="chk_allow_partial" className="text-[10px] text-zinc-400 cursor-pointer select-none">
                          {t("allowPartialLabel")}
                        </label>
                      </div>
                    </div>

                    <div className="flex gap-2">
                      <Button
                        onClick={handleRunAllocation}
                        disabled={submitting || activeShipment.status === "Allocated"}
                        className="bg-emerald-600 hover:bg-emerald-500 text-white h-9 px-4 text-xs font-semibold flex items-center gap-1.5"
                      >
                        <Play className="h-3.5 w-3.5" />
                        {t("runAllocation")}
                      </Button>

                      <Button
                        onClick={handleReleaseAllocation}
                        disabled={submitting || activeShipment.status === "Open" || activeShipment.status === "Unallocated"}
                        variant="outline"
                        className="border-rose-900 bg-rose-950/10 hover:bg-rose-900 text-rose-400 hover:text-white h-9 px-4 text-xs font-semibold flex items-center gap-1.5"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                        {t("releaseReservation")}
                      </Button>
                    </div>
                  </div>

                  <div className="bg-zinc-800/30 p-3 rounded-lg border border-zinc-800 grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div>
                      <span className="text-[10px] text-zinc-500">{t("summaryShipmentNo")}</span>
                      <div className="font-semibold text-zinc-200">{activeShipment.shipmentNo}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-zinc-500">{t("summaryCustomer")}</span>
                      <div className="font-semibold text-zinc-200 truncate">{activeShipment.partnerName}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-zinc-500">{t("summaryCreatedAt")}</span>
                      <div className="font-semibold text-zinc-200">{new Date(activeShipment.createdAt).toLocaleDateString()}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-zinc-500">{t("summaryCreatedBy")}</span>
                      <div className="font-semibold text-zinc-200">{activeShipment.createdBy}</div>
                    </div>
                  </div>

                  <div className="flex flex-col gap-2">
                    <span className="text-zinc-400 font-semibold">{t("linesTitle")}</span>
                    <div className="overflow-x-auto border border-zinc-800 rounded-lg">
                      <Table className="text-xs">
                        <TableHeader className="border-b border-zinc-800 bg-zinc-950/40">
                          <TableRow className="border-b border-zinc-800">
                            <TableHead className="text-zinc-400">{t("colItemCode")}</TableHead>
                            <TableHead className="text-zinc-400">{t("colItemName")}</TableHead>
                            <TableHead className="text-zinc-400 text-right">{t("colRequestedQty")}</TableHead>
                            <TableHead className="text-zinc-400 text-right">{t("colAllocatedQty")}</TableHead>
                            <TableHead className="text-zinc-400 text-center">{t("colUom")}</TableHead>
                            <TableHead className="text-zinc-400 text-center">{t("colStatus")}</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {shipmentLines.map((line) => (
                            <TableRow key={line.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/20">
                              <TableCell className="font-mono text-zinc-300">{line.itemCode}</TableCell>
                              <TableCell className="font-medium text-zinc-200">{line.itemName}</TableCell>
                              <TableCell className="text-right text-zinc-200">{(line.requestedQty ?? 0).toLocaleString()}</TableCell>
                              <TableCell className="text-right text-emerald-400 font-bold">{(line.allocatedQty ?? 0).toLocaleString()}</TableCell>
                              <TableCell className="text-center text-zinc-400">{line.uomName}</TableCell>
                              <TableCell className="text-center">
                                {line.allocatedQty === 0 ? (
                                  <Badge className="bg-zinc-800 text-zinc-400">{t("lineStatusUnallocated")}</Badge>
                                ) : line.allocatedQty < line.requestedQty ? (
                                  <Badge className="bg-amber-600/20 text-amber-500 border border-amber-800">{t("lineStatusPartial")}</Badge>
                                ) : (
                                  <Badge className="bg-emerald-600/20 text-emerald-400 border border-emerald-800">{t("lineStatusAllocated")}</Badge>
                                )}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
