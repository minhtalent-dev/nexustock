"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { RefreshCw, Layers, Plus, ArrowRight, ClipboardList, Settings, X, LogIn } from "lucide-react";

interface Lpn {
  id: string;
  lpnNo: string;
  locationId: string;
  status: string;
  createdAt: string;
}

interface LpnEvent {
  id: string;
  eventType: string;
  itemId: string | null;
  itemCode: string | null;
  itemName: string | null;
  lotNo: string | null;
  qty: number | null;
  fromLocationCode: string | null;
  toLocationCode: string | null;
  createdAt: string;
  createdBy: string;
}

interface Product {
  id: string;
  code: string;
  name: string;
}

interface StorageLocation {
  id: string;
  code: string;
}

interface InventoryBalance {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  lotNo: string;
  locationId: string;
  locationCode: string;
  qtyOnHand: number;
  qtyReserved: number;
  qtyAvailable: number;
  lpnId: string | null;
}

export default function LpnPage() {
  const t = useTranslations("Admin.lpn");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [lpns, setLpns] = useState<Lpn[]>([]);
  const [selectedLpn, setSelectedLpn] = useState<Lpn | null>(null);
  const [lpnEvents, setLpnEvents] = useState<LpnEvent[]>([]);
  const [lpnItems, setLpnItems] = useState<InventoryBalance[]>([]);

  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);

  const [loadingLpns, setLoadingLpns] = useState(false);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [submittingLpn, setSubmittingLpn] = useState(false);

  const [newLpn, setNewLpn] = useState({
    lpnNo: "",
    locationId: "",
  });

  const [attachForm, setAttachForm] = useState({
    itemId: "",
    lotNo: "",
    qty: 0,
  });

  const [moveLocationId, setMoveLocationId] = useState("");
  const [showAttachModal, setShowAttachModal] = useState(false);
  const [showMoveModal, setShowMoveModal] = useState(false);

  const fetchLpns = useCallback(async () => {
    setLoadingLpns(true);
    try {
      const res = await api.get<Lpn[]>("/lpns");
      setLpns(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoadingLpns(false);
    }
  }, [t, tErrors]);

  const fetchMetadata = useCallback(async () => {
    try {
      const prodRes = await api.get<{ items: Product[] }>("/master-data/products");
      setProducts(prodRes.data.items || []);
    } catch {
      // ignore
    }

    try {
      const locRes = await api.get<{ items: StorageLocation[] }>("/master-data/storage-locations");
      setLocations(locRes.data.items || []);
    } catch {
      // ignore
    }
  }, []);

  const fetchLpnDetails = async (lpn: Lpn) => {
    setLoadingDetails(true);
    try {
      const eventsRes = await api.get<LpnEvent[]>(`/lpns/${lpn.id}/events`);
      setLpnEvents(eventsRes.data || []);

      const balancesRes = await api.get<{ items: InventoryBalance[] }>("/inventory/balances", {
        params: { lpnId: lpn.id },
      });
      setLpnItems(balancesRes.data?.items || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadDetailFailed"));
    } finally {
      setLoadingDetails(false);
    }
  };

  useEffect(() => {
    queueMicrotask(() => {
      void fetchLpns();
      void fetchMetadata();
    });
  }, [fetchLpns, fetchMetadata]);

  const handleSelectLpn = (lpn: Lpn) => {
    setSelectedLpn(lpn);
    fetchLpnDetails(lpn);
  };

  const handleCreateLpn = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newLpn.lpnNo || !newLpn.locationId) {
      showApiErrorToast("", t("errors.fieldsRequired"));
      return;
    }

    setSubmittingLpn(true);
    try {
      const res = await api.post<Lpn>("/lpns", newLpn);
      showSuccess(t("toastCreateSuccess"));
      setNewLpn({ lpnNo: "", locationId: "" });
      fetchLpns();
      if (res.data) {
        handleSelectLpn(res.data);
      }
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setSubmittingLpn(false);
    }
  };

  const handleAttachItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedLpn) return;
    if (!attachForm.itemId || !attachForm.lotNo || attachForm.qty <= 0) {
      showApiErrorToast("", t("errors.attachFieldsRequired"));
      return;
    }

    try {
      await api.post(`/lpns/${selectedLpn.id}/attach`, attachForm);
      showSuccess(t("toastAttachSuccess"));
      setAttachForm({ itemId: "", lotNo: "", qty: 0 });
      setShowAttachModal(false);
      fetchLpnDetails(selectedLpn);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.attachFailed"));
    }
  };

  const handleDetachItem = async (item: InventoryBalance, qtyToDetach: number) => {
    if (!selectedLpn) return;
    if (qtyToDetach <= 0 || qtyToDetach > item.qtyOnHand) {
      showApiErrorToast("", t("errors.detachQtyInvalid"));
      return;
    }

    if (!confirm(t("confirmDetach", { qty: qtyToDetach }))) {
      return;
    }

    try {
      await api.post(`/lpns/${selectedLpn.id}/detach`, {
        itemId: item.itemId,
        lotNo: item.lotNo,
        qty: qtyToDetach,
      });
      showSuccess(t("toastDetachSuccess"));
      fetchLpnDetails(selectedLpn);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.detachFailed"));
    }
  };

  const handleMoveLpn = async () => {
    if (!selectedLpn || !moveLocationId) return;

    try {
      await api.post(`/lpns/${selectedLpn.id}/move`, {
        targetLocationId: moveLocationId,
      });
      showSuccess(t("toastMoveSuccess"));
      setShowMoveModal(false);
      setMoveLocationId("");

      const updatedLpn = { ...selectedLpn, locationId: moveLocationId };
      setSelectedLpn(updatedLpn);
      fetchLpns();
      fetchLpnDetails(updatedLpn);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.moveFailed"));
    }
  };

  const getLocationCode = (id: string) => {
    const loc = locations.find((l) => l.id === id);
    return loc ? loc.code : id.substring(0, 8);
  };

  const renderEventMessage = (evt: LpnEvent) => {
    switch (evt.eventType) {
      case "CREATE":
        return t("eventCreate");
      case "ATTACH":
        return t("eventAttach", { qty: evt.qty, itemCode: evt.itemCode, lotNo: evt.lotNo });
      case "DETACH":
        return t("eventDetach", { qty: evt.qty, itemCode: evt.itemCode, lotNo: evt.lotNo });
      case "MOVE":
        return t("eventMove", { from: evt.fromLocationCode, to: evt.toLocationCode });
      default:
        return evt.eventType;
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Layers className="h-6 w-6 text-emerald-500" />
            {t("title")}
          </h1>
          <p className="text-xs text-muted-foreground mt-1">{t("subtitle")}</p>
        </div>
        <div>
          <Button
            onClick={fetchLpns}
            variant="outline"
            className="border-border hover:bg-muted text-zinc-300 h-9 px-4 flex items-center gap-2"
          >
            <RefreshCw className={`h-4 w-4 ${loadingLpns ? "animate-spin" : ""}`} />
            {t("refresh")}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1 flex flex-col gap-6">
          <Card className="bg-card border-border text-white">
            <CardHeader>
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Plus className="h-4 w-4 text-emerald-500" />
                {t("createTitle")}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleCreateLpn} className="flex flex-col gap-4 text-xs">
                <div className="flex flex-col gap-1.5">
                  <label className="text-[10px] text-muted-foreground">{t("lpnNoLabel")}</label>
                  <input
                    type="text"
                    placeholder={t("lpnNoPlaceholder")}
                    value={newLpn.lpnNo}
                    onChange={(e) => setNewLpn({ ...newLpn, lpnNo: e.target.value.toUpperCase() })}
                    className="bg-muted border border-border text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-mono"
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-[10px] text-muted-foreground">{t("initialLocationLabel")}</label>
                  <select
                    value={newLpn.locationId}
                    onChange={(e) => setNewLpn({ ...newLpn, locationId: e.target.value })}
                    className="bg-muted border border-border text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                  >
                    <option value="">{t("selectLocation")}</option>
                    {locations.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code}
                      </option>
                    ))}
                  </select>
                </div>
                <Button type="submit" disabled={submittingLpn} className="bg-emerald-600 hover:bg-emerald-500 text-white w-full h-9 text-xs rounded">
                  {submittingLpn ? t("creating") : t("createLpn")}
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card className="bg-card border-border text-white">
            <CardHeader>
              <CardTitle className="text-sm font-semibold">{t("listTitle")}</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {loadingLpns && lpns.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground text-xs">{t("loadingList")}</div>
              ) : lpns.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground text-xs">{t("emptyList")}</div>
              ) : (
                <div className="flex flex-col max-h-[400px] overflow-y-auto">
                  {lpns.map((lpn) => (
                    <button
                      key={lpn.id}
                      onClick={() => handleSelectLpn(lpn)}
                      className={`text-left p-3.5 border-b border-border hover:bg-muted/40 transition-all flex items-center justify-between text-xs ${
                        selectedLpn?.id === lpn.id ? "bg-muted/80 border-l-4 border-l-emerald-500" : ""
                      }`}
                    >
                      <div className="flex flex-col gap-1">
                        <span className="font-bold text-zinc-200 font-mono">{lpn.lpnNo}</span>
                        <span className="text-[10px] text-muted-foreground">
                          {t("locationPrefix")} {getLocationCode(lpn.locationId)}
                        </span>
                      </div>
                      <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white text-[9px] scale-90">{lpn.status}</Badge>
                    </button>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-2 flex flex-col gap-6">
          {selectedLpn ? (
            <>
              <div className="flex items-center justify-between gap-4 bg-card p-4 border border-border rounded-lg">
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] text-muted-foreground uppercase tracking-wider font-semibold">{t("selectedLabel")}</span>
                  <h2 className="text-xl font-bold font-mono text-emerald-400">{selectedLpn.lpnNo}</h2>
                  <span className="text-xs text-zinc-300">
                    {t("locationPrefix")}{" "}
                    <span className="font-mono font-bold text-white">{getLocationCode(selectedLpn.locationId)}</span>
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <Button onClick={() => setShowAttachModal(true)} className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4 flex items-center gap-2">
                    <LogIn className="h-4 w-4" />
                    {t("attachBtn")}
                  </Button>
                  <Button onClick={() => setShowMoveModal(true)} variant="outline" className="border-border hover:bg-muted text-zinc-300 text-xs h-8 px-4 flex items-center gap-2">
                    <ArrowRight className="h-4 w-4 text-emerald-500" />
                    {t("moveBtn")}
                  </Button>
                </div>
              </div>

              <Card className="bg-card border-border text-white">
                <CardHeader>
                  <CardTitle className="text-sm font-semibold flex items-center gap-2">
                    <ClipboardList className="h-4 w-4 text-emerald-500" />
                    {t("itemsTitle")}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  {loadingDetails ? (
                    <div className="text-center py-8 text-muted-foreground text-xs">{t("loadingItems")}</div>
                  ) : lpnItems.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground text-xs">{t("emptyItems")}</div>
                  ) : (
                    <Table className="text-xs">
                      <TableHeader className="border-b border-border">
                        <TableRow className="border-b border-border hover:bg-muted/50">
                          <TableHead className="text-muted-foreground">{t("colProduct")}</TableHead>
                          <TableHead className="text-muted-foreground">{t("colLotNo")}</TableHead>
                          <TableHead className="text-muted-foreground text-right">{t("colOnHand")}</TableHead>
                          <TableHead className="text-muted-foreground text-right">{t("colReserved")}</TableHead>
                          <TableHead className="text-muted-foreground text-right">{t("colAvailable")}</TableHead>
                          <TableHead className="text-muted-foreground text-center">{t("colActions")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {lpnItems.map((item) => (
                          <TableRow key={item.id} className="border-b border-border/50 hover:bg-muted/30">
                            <TableCell className="font-semibold text-zinc-300">
                              {item.itemCode} - {item.itemName}
                            </TableCell>
                            <TableCell className="text-zinc-300 font-mono">{item.lotNo}</TableCell>
                            <TableCell className="text-right font-bold text-white">{item.qtyOnHand}</TableCell>
                            <TableCell className="text-right text-amber-500">{item.qtyReserved}</TableCell>
                            <TableCell className="text-right text-emerald-400 font-bold">{item.qtyAvailable}</TableCell>
                            <TableCell className="text-center">
                              <Button
                                onClick={() => handleDetachItem(item, item.qtyOnHand)}
                                variant="outline"
                                className="border-border hover:bg-muted text-rose-500 h-7 px-3 text-[10px] rounded"
                              >
                                {t("detachBtn")}
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>

              <Card className="bg-card border-border text-white">
                <CardHeader>
                  <CardTitle className="text-sm font-semibold flex items-center gap-2">
                    <Settings className="h-4 w-4 text-emerald-500" />
                    {t("eventsTitle")}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  {loadingDetails ? (
                    <div className="text-center py-8 text-muted-foreground text-xs">{t("loadingEvents")}</div>
                  ) : lpnEvents.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground text-xs">{t("emptyEvents")}</div>
                  ) : (
                    <div className="relative border-l border-border pl-4 space-y-4 text-xs ml-2">
                      {lpnEvents.map((evt) => (
                        <div key={evt.id} className="relative">
                          <div className="absolute -left-[21px] top-1 bg-card border border-border w-2.5 h-2.5 rounded-full flex items-center justify-center">
                            <div className="w-1 h-1 bg-emerald-500 rounded-full" />
                          </div>
                          <div className="flex flex-col gap-1">
                            <div className="flex items-center gap-2">
                              <Badge className="bg-muted text-zinc-300 text-[9px] hover:bg-muted scale-90 px-2 py-0.5">
                                {evt.eventType}
                              </Badge>
                              <span className="text-[10px] text-muted-foreground">
                                {t("eventBy", {
                                  at: new Date(evt.createdAt).toLocaleString(),
                                  by: evt.createdBy,
                                })}
                              </span>
                            </div>
                            <p className="text-zinc-300">{renderEventMessage(evt)}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            </>
          ) : (
            <div className="bg-card border border-border rounded-lg p-16 text-center text-muted-foreground text-xs flex flex-col items-center justify-center gap-2">
              <Layers className="h-10 w-10 text-zinc-700 animate-pulse" />
              {t("selectHint")}
            </div>
          )}
        </div>
      </div>

      {showAttachModal && selectedLpn && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-card border border-border rounded-lg max-w-sm w-full text-white shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-border">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <LogIn className="h-4 w-4 text-emerald-500" />
                {t("attachDialogTitle", { lpnNo: selectedLpn.lpnNo })}
              </h3>
              <button onClick={() => setShowAttachModal(false)} className="text-muted-foreground hover:text-white transition-all">
                <X className="h-4 w-4" />
              </button>
            </div>
            <form onSubmit={handleAttachItem} className="p-4 flex flex-col gap-4 text-xs">
              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-muted-foreground">{t("productLabel")}</label>
                <select
                  value={attachForm.itemId}
                  onChange={(e) => setAttachForm({ ...attachForm, itemId: e.target.value })}
                  className="bg-muted border border-border text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                >
                  <option value="">{t("selectProduct")}</option>
                  {products.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.code} - {p.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-muted-foreground">{t("lotNoLabel")}</label>
                <input
                  type="text"
                  placeholder={t("lotNoPlaceholder")}
                  value={attachForm.lotNo}
                  onChange={(e) => setAttachForm({ ...attachForm, lotNo: e.target.value })}
                  className="bg-muted border border-border text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-muted-foreground">{t("attachQtyLabel")}</label>
                <input
                  type="number"
                  value={attachForm.qty}
                  onChange={(e) => setAttachForm({ ...attachForm, qty: parseFloat(e.target.value) || 0 })}
                  className="bg-muted border border-border text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-bold"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <Button type="button" onClick={() => setShowAttachModal(false)} variant="outline" className="border-border hover:bg-muted text-zinc-300 text-xs h-8 px-4">
                  {tc("cancel")}
                </Button>
                <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4">
                  {t("attachBtn2")}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showMoveModal && selectedLpn && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-card border border-border rounded-lg max-w-sm w-full text-white shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-border">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <ArrowRight className="h-4 w-4 text-emerald-500" />
                {t("moveDialogTitle", { lpnNo: selectedLpn.lpnNo })}
              </h3>
              <button onClick={() => setShowMoveModal(false)} className="text-muted-foreground hover:text-white transition-all">
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="p-4 flex flex-col gap-4 text-xs">
              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-muted-foreground">{t("targetLocationLabel")}</label>
                <select
                  value={moveLocationId}
                  onChange={(e) => setMoveLocationId(e.target.value)}
                  className="bg-muted border border-border text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                >
                  <option value="">{t("selectTargetLocation")}</option>
                  {locations
                    .filter((l) => l.id !== selectedLpn.locationId)
                    .map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code}
                      </option>
                    ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <Button onClick={() => setShowMoveModal(false)} variant="outline" className="border-border hover:bg-muted text-zinc-300 text-xs h-8 px-4">
                  {tc("cancel")}
                </Button>
                <Button onClick={handleMoveLpn} className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4">
                  {tc("confirm")}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </PageShell>
  );
}
