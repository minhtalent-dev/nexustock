"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { QcResultDialog } from "@/features/qc/components/qc-result-dialog";
import { HoldReleaseDialog } from "@/features/qc/components/hold-release-dialog";
import {
  CheckSquare, Search, Unlock, Lock, Ban,
  RefreshCw, ClipboardCheck, AlertOctagon, History
} from "lucide-react";

interface QcQueueItem {
  id: string;
  lotId: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  expectedQty: number;
  receivedQty: number;
  createdAt: string;
  agingHours?: number;
  agingBucket?: string;
}

interface QcHistoryItem {
  id: string;
  eventType: string;
  lotId: string;
  lotNo: string;
  inspector?: string;
  isPassed?: boolean;
  reasonCode?: string;
  metrics?: string;
  createdAt: string;
}

interface LotDetails {
  id: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  expiryDate: string;
  productionDate: string;
  qcStatus: string;
}

export default function QcPage() {
  const t = useTranslations("Admin.qc");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [tab, setTab] = useState<"queue" | "history">("queue");
  const [queue, setQueue] = useState<QcQueueItem[]>([]);
  const [history, setHistory] = useState<QcHistoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [agingHours, setAgingHours] = useState("");

  const [lookupLotNo, setLookupLotNo] = useState("");
  const [lookupResult, setLookupResult] = useState<LotDetails[] | null>(null);
  const [lookupLoading, setLookupLoading] = useState(false);

  const [activeLot, setActiveLot] = useState<{ id: string; lotNo: string; qcRequestId?: string } | null>(null);
  const [dialogMode, setDialogMode] = useState<"result" | "hold" | "release" | "reject" | null>(null);

  const fetchQueue = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (searchQuery.trim()) params.set("q", searchQuery.trim());
      if (fromDate) params.set("from", new Date(fromDate).toISOString());
      if (toDate) params.set("to", new Date(toDate).toISOString());
      if (agingHours.trim()) params.set("agingHours", agingHours.trim());
      const qs = params.toString();
      const res = await api.get<QcQueueItem[]>(`/qc/queue${qs ? `?${qs}` : ""}`);
      setQueue(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadQueueFailed"));
    } finally {
      setLoading(false);
    }
  }, [agingHours, fromDate, searchQuery, t, tErrors, toDate]);

  const fetchHistory = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (searchQuery.trim()) params.set("lotNo", searchQuery.trim());
      if (fromDate) params.set("from", new Date(fromDate).toISOString());
      if (toDate) params.set("to", new Date(toDate).toISOString());
      const qs = params.toString();
      const res = await api.get<QcHistoryItem[]>(`/qc/history${qs ? `?${qs}` : ""}`);
      setHistory(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadHistoryFailed"));
    } finally {
      setLoading(false);
    }
  }, [fromDate, searchQuery, t, tErrors, toDate]);

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lookupLotNo.trim()) return;

    setLookupLoading(true);
    setLookupResult(null);
    try {
      const res = await api.get<LotDetails[]>(`/lots/${lookupLotNo.trim()}`);
      setLookupResult(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.lotNotFound"));
    } finally {
      setLookupLoading(false);
    }
  };

  useEffect(() => {
    queueMicrotask(() => {
      if (tab === "queue") void fetchQueue();
      else void fetchHistory();
    });
  }, [tab, fetchQueue, fetchHistory]);

  const openQcDialog = (item: QcQueueItem) => {
    setActiveLot({ id: item.lotId, lotNo: item.lotNo, qcRequestId: item.id });
    setDialogMode("result");
  };

  const openActionDialog = (lotId: string, lotNo: string, mode: "hold" | "release" | "reject") => {
    setActiveLot({ id: lotId, lotNo });
    setDialogMode(mode);
  };

  const handleSuccess = () => {
    if (tab === "queue") fetchQueue();
    else fetchHistory();
    if (lookupLotNo) {
      api.get<LotDetails[]>(`/lots/${lookupLotNo.trim()}`).then((res) => {
        setLookupResult(res.data);
      }).catch(() => {});
    }
  };

  const getQcStatusBadge = (status: string) => {
    switch (status.toUpperCase()) {
      case "RELEASE":
        return <Badge className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">{t("statusRelease")}</Badge>;
      case "HOLD":
        return <Badge className="bg-amber-500/10 text-amber-500 border-amber-500/20">{t("statusHold")}</Badge>;
      case "REJECT":
        return <Badge className="bg-rose-500/10 text-rose-500 border-rose-500/20">{t("statusReject")}</Badge>;
      case "UNSPEC":
        return <Badge className="bg-zinc-500/10 text-zinc-400 border-zinc-500/20">{t("statusUnspec")}</Badge>;
      default:
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">{status}</Badge>;
    }
  };

  const agingBadge = (bucket?: string, hours?: number) => {
    if (bucket === "critical72") {
      return <Badge className="bg-rose-500/10 text-rose-400 border-rose-500/20">{t("agingCritical")} {hours ?? 0}h</Badge>;
    }
    if (bucket === "warn24") {
      return <Badge className="bg-amber-500/10 text-amber-400 border-amber-500/20">{t("agingWarn")} {hours ?? 0}h</Badge>;
    }
    return <Badge className="bg-zinc-500/10 text-zinc-400 border-zinc-500/20">{t("agingFresh")}</Badge>;
  };

  const refresh = () => {
    if (tab === "queue") void fetchQueue();
    else void fetchHistory();
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-3">
          <CheckSquare className="h-6 w-6 text-emerald-500" />
          {t("title")}
        </h1>
        <p className="text-xs text-zinc-400 mt-1">{t("subtitle")}</p>
      </div>

      <div className="flex gap-2">
        <Button
          variant={tab === "queue" ? "default" : "ghost"}
          className={`h-8 text-xs ${tab === "queue" ? "bg-emerald-600 hover:bg-emerald-500" : "text-zinc-400"}`}
          onClick={() => setTab("queue")}
        >
          <ClipboardCheck className="h-3.5 w-3.5 mr-1.5" />
          {t("tabQueue")}
        </Button>
        <Button
          variant={tab === "history" ? "default" : "ghost"}
          className={`h-8 text-xs ${tab === "history" ? "bg-emerald-600 hover:bg-emerald-500" : "text-zinc-400"}`}
          onClick={() => setTab("history")}
        >
          <History className="h-3.5 w-3.5 mr-1.5" />
          {t("tabHistory")}
        </Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 border-b border-zinc-800">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                {tab === "queue" ? (
                  <>
                    <ClipboardCheck className="h-4 w-4 text-emerald-500" />
                    {t("queueTitle", { count: queue.length })}
                  </>
                ) : (
                  <>
                    <History className="h-4 w-4 text-emerald-500" />
                    {t("historyTitle")}
                  </>
                )}
              </CardTitle>
              <Button variant="ghost" size="icon" onClick={refresh} className="h-8 w-8 text-zinc-400 hover:text-white">
                <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
              </Button>
            </CardHeader>
            <CardContent className="pt-4">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-2 mb-4">
                <div className="relative md:col-span-2">
                  <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-zinc-500" />
                  <Input
                    placeholder={t("searchPlaceholder")}
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && refresh()}
                    className="bg-zinc-800 border-zinc-700 text-white pl-9 h-9 text-xs"
                  />
                </div>
                <Input
                  type="date"
                  aria-label={t("filterFrom")}
                  value={fromDate}
                  onChange={(e) => setFromDate(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-white h-9 text-xs"
                />
                <Input
                  type="date"
                  aria-label={t("filterTo")}
                  value={toDate}
                  onChange={(e) => setToDate(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-white h-9 text-xs"
                />
                {tab === "queue" && (
                  <Input
                    type="number"
                    min={0}
                    placeholder={t("filterAging")}
                    value={agingHours}
                    onChange={(e) => setAgingHours(e.target.value)}
                    className="bg-zinc-800 border-zinc-700 text-white h-9 text-xs md:col-span-2"
                  />
                )}
                <Button onClick={refresh} className="bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 h-9 text-xs md:col-span-2">
                  {tc("filter")}
                </Button>
              </div>

              {loading && (tab === "queue" ? queue.length === 0 : history.length === 0) ? (
                <div className="text-center py-8 text-zinc-500 text-xs">{t("loading")}</div>
              ) : tab === "queue" ? (
                queue.length === 0 ? (
                  <div className="text-center py-8 text-zinc-500 text-xs">{t("queueEmpty")}</div>
                ) : (
                  <div className="overflow-x-auto">
                    <Table className="text-xs">
                      <TableHeader className="border-b border-zinc-800">
                        <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                          <TableHead className="text-zinc-400">{t("colLotNo")}</TableHead>
                          <TableHead className="text-zinc-400">{t("colItem")}</TableHead>
                          <TableHead className="text-zinc-400">{t("colAging")}</TableHead>
                          <TableHead className="text-zinc-400 text-right">{t("colExpectedQty")}</TableHead>
                          <TableHead className="text-zinc-400 text-right">{t("colReceivedQty")}</TableHead>
                          <TableHead className="text-zinc-400">{t("colRequestDate")}</TableHead>
                          <TableHead className="text-zinc-400 text-center">{t("colActions")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {queue.map((item) => (
                          <TableRow key={item.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                            <TableCell className="font-semibold text-zinc-200">{item.lotNo}</TableCell>
                            <TableCell>
                              <div className="font-medium text-zinc-300">{item.itemName}</div>
                              <div className="text-[10px] text-zinc-500 font-mono">{item.itemCode}</div>
                            </TableCell>
                            <TableCell>{agingBadge(item.agingBucket, item.agingHours)}</TableCell>
                            <TableCell className="text-right text-zinc-300">{item.expectedQty.toLocaleString()}</TableCell>
                            <TableCell className="text-right text-zinc-200 font-medium">{item.receivedQty.toLocaleString()}</TableCell>
                            <TableCell className="text-zinc-400">{new Date(item.createdAt).toLocaleString()}</TableCell>
                            <TableCell className="text-center">
                              <Button
                                onClick={() => openQcDialog(item)}
                                className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-3 text-[11px] rounded"
                              >
                                {t("inspect")}
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )
              ) : history.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">{t("historyEmpty")}</div>
              ) : (
                <div className="overflow-x-auto">
                  <Table className="text-xs">
                    <TableHeader className="border-b border-zinc-800">
                      <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                        <TableHead className="text-zinc-400">{t("colLotNo")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colEvent")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colInspector")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colRequestDate")}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {history.map((item) => (
                        <TableRow key={`${item.eventType}-${item.id}`} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                          <TableCell className="font-semibold text-zinc-200">{item.lotNo}</TableCell>
                          <TableCell className="text-zinc-300">
                            {item.eventType}
                            {item.isPassed != null ? ` · ${item.isPassed ? "Pass" : "Fail"}` : ""}
                            {item.reasonCode ? ` · ${item.reasonCode}` : ""}
                          </TableCell>
                          <TableCell className="text-zinc-400">{item.inspector || "—"}</TableCell>
                          <TableCell className="text-zinc-400">{new Date(item.createdAt).toLocaleString()}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-2">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <AlertOctagon className="h-4 w-4 text-amber-500" />
                {t("holdReleaseTitle")}
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4 flex flex-col gap-4">
              <form onSubmit={handleLookup} className="flex gap-2">
                <Input
                  placeholder={t("lookupPlaceholder")}
                  value={lookupLotNo}
                  onChange={(e) => setLookupLotNo(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-white h-9 text-xs flex-1"
                />
                <Button type="submit" disabled={lookupLoading} className="bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 text-white h-9 px-3 text-xs">
                  <Search className="h-4 w-4" />
                </Button>
              </form>

              {lookupLoading && (
                <div className="text-center py-4 text-zinc-500 text-xs">{t("lookupLoading")}</div>
              )}

              {lookupResult && lookupResult.map((lot, idx) => (
                <div key={idx} className="bg-zinc-800/50 p-4 rounded-lg border border-zinc-800 flex flex-col gap-3 text-xs">
                  <div className="flex justify-between items-start border-b border-zinc-800 pb-2">
                    <div>
                      <div className="font-semibold text-zinc-200 text-sm">{lot.lotNo}</div>
                      <span className="text-[10px] text-zinc-500 font-mono">ID: {lot.id}</span>
                    </div>
                    {getQcStatusBadge(lot.qcStatus)}
                  </div>

                  <div className="grid grid-cols-2 gap-y-2 text-[11px]">
                    <span className="text-zinc-500">{t("itemLabel")}:</span>
                    <span className="text-zinc-300 text-right truncate">{lot.itemName} ({lot.itemCode})</span>
                    <span className="text-zinc-500">{t("expiryLabel")}:</span>
                    <span className="text-zinc-300 text-right">{lot.expiryDate ? new Date(lot.expiryDate).toLocaleDateString() : tc("notAvailable")}</span>
                  </div>

                  <div className="flex gap-2 mt-2 border-t border-zinc-800 pt-3">
                    <Button
                      onClick={() => openActionDialog(lot.id, lot.lotNo, "hold")}
                      disabled={lot.qcStatus.toUpperCase() === "HOLD"}
                      className="bg-amber-600/10 text-amber-500 border border-amber-600/20 hover:bg-amber-600 hover:text-white h-8 px-2 flex-1 text-[11px] gap-1"
                    >
                      <Lock className="h-3.5 w-3.5" />
                      {t("hold")}
                    </Button>
                    <Button
                      onClick={() => openActionDialog(lot.id, lot.lotNo, "release")}
                      disabled={lot.qcStatus.toUpperCase() === "RELEASE"}
                      className="bg-emerald-600/10 text-emerald-500 border border-emerald-600/20 hover:bg-emerald-600 hover:text-white h-8 px-2 flex-1 text-[11px] gap-1"
                    >
                      <Unlock className="h-3.5 w-3.5" />
                      {t("release")}
                    </Button>
                    <Button
                      onClick={() => openActionDialog(lot.id, lot.lotNo, "reject")}
                      disabled={lot.qcStatus.toUpperCase() === "REJECT"}
                      className="bg-rose-600/10 text-rose-500 border border-rose-600/20 hover:bg-rose-600 hover:text-white h-8 px-2 flex-1 text-[11px] gap-1"
                    >
                      <Ban className="h-3.5 w-3.5" />
                      {t("reject")}
                    </Button>
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>

      {dialogMode === "result" && activeLot && (
        <QcResultDialog
          isOpen={true}
          onClose={() => {
            setActiveLot(null);
            setDialogMode(null);
          }}
          lotId={activeLot.id}
          lotNo={activeLot.lotNo}
          qcRequestId={activeLot.qcRequestId || ""}
          onSuccess={handleSuccess}
        />
      )}

      {(dialogMode === "hold" || dialogMode === "release" || dialogMode === "reject") && activeLot && (
        <HoldReleaseDialog
          isOpen={true}
          onClose={() => {
            setActiveLot(null);
            setDialogMode(null);
          }}
          lotId={activeLot.id}
          lotNo={activeLot.lotNo}
          mode={dialogMode}
          onSuccess={handleSuccess}
        />
      )}
    </div>
  );
}
