"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { resolveApiError } from "@/lib/api-error-i18n";
import { getHttpErrorPayload } from "@/lib/http-error";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { MapPin, Search, RefreshCw, Layers, CheckCircle2, XCircle } from "lucide-react";

interface InventoryBalance {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  lotNo: string;
  locationId: string;
  locationCode: string;
  qtyOnHand: number;
  qtyReserved: number;
  qtyAvailable: number;
}

interface PutawayProposal {
  lotId: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  lotNo: string;
  qty: number;
  proposals: PutawayCandidate[];
  zoneLocations: ZoneLocation[];
}

interface PutawayCandidate {
  proposalId: string;
  locationId: string;
  locationCode: string;
  zoneCode: string;
  score: number;
  reason: string;
}

interface ZoneLocation {
  locationId: string;
  locationCode: string;
  x: number;
  y: number;
  z: number;
  status: "PROPOSED" | "OCCUPIED" | "FREE";
}

interface LotInfo {
  id: string;
  itemId: string;
}

export default function PutawayPage() {
  const t = useTranslations("Admin.putaway");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [balances, setBalances] = useState<InventoryBalance[]>([]);
  const [loadingBalances, setLoadingBalances] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  const [activeItem, setActiveItem] = useState<InventoryBalance | null>(null);
  const [proposalsData, setProposalsData] = useState<PutawayProposal | null>(null);
  const [loadingProposals, setLoadingProposals] = useState(false);

  const [selectedCandidate, setSelectedCandidate] = useState<PutawayCandidate | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [rejectingProposal, setRejectingProposal] = useState<PutawayCandidate | null>(null);
  const [rejectReasonCode, setRejectReasonCode] = useState("LOC_FULL");
  const [rejectNote, setRejectNote] = useState("");

  const fetchBalances = useCallback(async () => {
    setLoadingBalances(true);
    try {
      const res = await api.get<{ items: InventoryBalance[] }>("/inventory/balances?pageSize=100");
      setBalances(res.data.items || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadBalancesFailed"));
    } finally {
      setLoadingBalances(false);
    }
  }, [t, tErrors]);

  const handleFetchProposals = async (item: InventoryBalance) => {
    setActiveItem(item);
    setProposalsData(null);
    setSelectedCandidate(null);
    setLoadingProposals(true);
    try {
      const lotRes = await api.get<LotInfo[]>(`/lots/${item.lotNo}`);
      const matchingLot = lotRes.data.find((l) => l.itemId === item.itemId);
      if (!matchingLot) {
        showApiErrorToast("", t("errors.lotNotFound"));
        setLoadingProposals(false);
        return;
      }

      const res = await api.get<PutawayProposal>(`/putaway/proposals?lotId=${matchingLot.id}&qty=${item.qtyAvailable}`);
      setProposalsData(res.data);
      if (res.data.proposals && res.data.proposals.length > 0) {
        setSelectedCandidate(res.data.proposals[0]);
      }
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadProposalsFailed"));
    } finally {
      setLoadingProposals(false);
    }
  };

  const handleConfirmPutaway = async () => {
    if (!activeItem || !proposalsData || !selectedCandidate) return;

    setSubmitting(true);
    try {
      const payload = {
        proposalId: selectedCandidate.proposalId,
        lotId: proposalsData.lotId,
        fromLocationId: activeItem.locationId,
        selectedLocationId: selectedCandidate.locationId,
        qty: proposalsData.qty
      };

      const res = await api.post("/putaway/confirm", payload);
      showSuccess(res.data.message || t("toastConfirmSuccess"));
      
      setProposalsData(null);
      setActiveItem(null);
      setSelectedCandidate(null);
      fetchBalances();
    } catch (err: unknown) {
      if (getHttpErrorPayload(err).status === 409) {
        showApiErrorToast("", t("errors.locationChanged"));
      } else {
        const { codeLabel, message } = resolveApiError(err, tErrors);
        showApiErrorToast(codeLabel, message || t("errors.confirmFailed"));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleRejectPutaway = async () => {
    if (!rejectingProposal) return;
    try {
      const payload = {
        proposalId: rejectingProposal.proposalId,
        reasonCode: rejectReasonCode,
        note: rejectNote
      };

      await api.post("/putaway/reject", payload);
      showSuccess(t("toastRejectSuccess"));
      
      if (proposalsData) {
        const updatedProposals = proposalsData.proposals.filter(p => p.proposalId !== rejectingProposal.proposalId);
        setProposalsData({
          ...proposalsData,
          proposals: updatedProposals
        });
        if (selectedCandidate?.proposalId === rejectingProposal.proposalId) {
          setSelectedCandidate(updatedProposals[0] || null);
        }
      }
      setRejectingProposal(null);
      setRejectNote("");
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.rejectFailed"));
    }
  };

  useEffect(() => {
    queueMicrotask(() => void fetchBalances());
  }, [fetchBalances]);

  const filteredBalances = balances.filter(b => 
    b.lotNo.toLowerCase().includes(searchQuery.toLowerCase()) ||
    b.itemName.toLowerCase().includes(searchQuery.toLowerCase()) ||
    b.itemCode.toLowerCase().includes(searchQuery.toLowerCase()) ||
    b.locationCode.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const render2DGridMap = () => {
    if (!proposalsData || !proposalsData.zoneLocations || proposalsData.zoneLocations.length === 0) return null;

    const xs = proposalsData.zoneLocations.map(l => l.x);
    const ys = proposalsData.zoneLocations.map(l => l.y);
    const minX = Math.min(...xs, 1);
    const maxX = Math.max(...xs, 1);
    const minY = Math.min(...ys, 1);
    const maxY = Math.max(...ys, 1);

    const columnsCount = maxX - minX + 1;
    const rowsCount = maxY - minY + 1;

    return (
      <div className="flex flex-col gap-2 mt-4">
        <div className="flex justify-between items-center text-xs text-muted-foreground">
          <span>{t("gridTitle")}</span>
          <div className="flex gap-4">
            <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-emerald-500/20 border border-emerald-500 inline-block animate-pulse"></span> {t("legendProposed")}</span>
            <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-muted border border-border inline-block"></span> {t("legendFree")}</span>
            <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-rose-950/20 border border-rose-900 inline-block"></span> {t("legendOccupied")}</span>
          </div>
        </div>

        <div 
          className="grid gap-2 border border-border p-4 rounded-lg bg-background overflow-auto max-h-[300px] select-none"
          style={{
            gridTemplateColumns: `repeat(${columnsCount}, minmax(80px, 1fr))`,
            gridTemplateRows: `repeat(${rowsCount}, minmax(50px, 1fr))`
          }}
        >
          {Array.from({ length: rowsCount }).map((_, rIdx) => {
            const y = maxY - rIdx;
            return Array.from({ length: columnsCount }).map((_, cIdx) => {
              const x = minX + cIdx;
              const loc = proposalsData.zoneLocations.find(l => l.x === x && l.y === y);

              if (!loc) {
                return <div key={`empty-${x}-${y}`} className="opacity-0"></div>;
              }

              const isSelected = selectedCandidate?.locationId === loc.locationId;
              const proposal = proposalsData.proposals.find(p => p.locationId === loc.locationId);

              let cellStyle = "border-border bg-card/40 text-muted-foreground";
              if (loc.status === "PROPOSED") {
                cellStyle = isSelected 
                  ? "border-emerald-500 bg-emerald-500/20 text-emerald-400 font-bold shadow-md shadow-emerald-500/10 cursor-pointer animate-pulse"
                  : "border-emerald-600/50 bg-emerald-600/10 text-emerald-500 cursor-pointer hover:border-emerald-500 hover:bg-emerald-500/10 transition-colors";
              } else if (loc.status === "OCCUPIED") {
                cellStyle = "border-rose-950 bg-rose-950/10 text-rose-500/70 cursor-not-allowed";
              } else if (loc.status === "FREE") {
                cellStyle = isSelected
                  ? "border-emerald-500 bg-emerald-500/20 text-emerald-400 font-bold cursor-pointer"
                  : "border-border bg-muted/40 text-zinc-300 cursor-pointer hover:border-zinc-500 transition-colors";
              }

              const handleCellClick = () => {
                if (loc.status === "PROPOSED" && proposal) {
                  setSelectedCandidate(proposal);
                } else if (loc.status === "FREE") {
                  setSelectedCandidate({
                    proposalId: "00000000-0000-0000-0000-000000000000",
                    locationId: loc.locationId,
                    locationCode: loc.locationCode,
                    zoneCode: "NORMAL",
                    score: 10,
                    reason: t("manualSelectReason")
                  });
                }
              };

              return (
                <div
                  key={loc.locationId}
                  onClick={handleCellClick}
                  className={`border rounded flex flex-col items-center justify-center p-1 text-[10px] relative transition-all ${cellStyle}`}
                >
                  <span className="font-semibold">{loc.locationCode}</span>
                  {proposal && <span className="text-[9px] opacity-80">({proposal.score}{t("scoreUnit")})</span>}
                  {loc.status === "OCCUPIED" && <span className="text-[8px] opacity-50">{t("cellFull")}</span>}
                </div>
              );
            });
          })}
        </div>
      </div>
    );
  };

  return (
    <PageShell className="gap-6">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-3">
          <MapPin className="h-6 w-6 text-emerald-500" />
          {t("title")}
        </h1>
        <p className="text-xs text-muted-foreground mt-1">{t("subtitle")}</p>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-5 gap-6">
        <div className="xl:col-span-2 flex flex-col gap-4">
          <Card className="bg-card border-border text-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 border-b border-border">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Layers className="h-4 w-4 text-emerald-500" />
                {t("queueTitle", { count: filteredBalances.length })}
              </CardTitle>
              <Button variant="ghost" size="icon" onClick={fetchBalances} className="h-8 w-8 text-muted-foreground hover:text-white">
                <RefreshCw className={`h-4 w-4 ${loadingBalances ? "animate-spin" : ""}`} />
              </Button>
            </CardHeader>
            <CardContent className="pt-4">
              <div className="relative mb-4">
                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder={t("searchPlaceholder")}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="bg-muted border-border text-white pl-9 h-9 text-xs"
                />
              </div>

              {loadingBalances && balances.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground text-xs">{t("loading")}</div>
              ) : filteredBalances.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground text-xs">{t("queueEmpty")}</div>
              ) : (
                <div className="overflow-x-auto max-h-[500px]">
                  <Table className="text-xs">
                    <TableHeader className="border-b border-border">
                      <TableRow className="border-b border-border hover:bg-muted/50">
                        <TableHead className="text-muted-foreground">{t("colLotLocation")}</TableHead>
                        <TableHead className="text-muted-foreground">{t("colItem")}</TableHead>
                        <TableHead className="text-muted-foreground text-right">{t("colAvailableQty")}</TableHead>
                        <TableHead className="text-muted-foreground text-center">{t("colActions")}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filteredBalances.map((item) => (
                        <TableRow 
                          key={item.id} 
                          className={`border-b border-border/50 hover:bg-muted/30 ${activeItem?.id === item.id ? "bg-muted/50" : ""}`}
                        >
                          <TableCell>
                            <div className="font-semibold text-zinc-200">{item.lotNo}</div>
                            <div className="text-[10px] text-muted-foreground font-mono">{t("atLocation")}: {item.locationCode}</div>
                          </TableCell>
                          <TableCell>
                            <div className="font-medium text-zinc-300 truncate max-w-[120px]">{item.itemName}</div>
                            <div className="text-[10px] text-muted-foreground font-mono">{item.itemCode}</div>
                          </TableCell>
                          <TableCell className="text-right text-zinc-200 font-medium">
                            {item.qtyAvailable.toLocaleString()}
                          </TableCell>
                          <TableCell className="text-center">
                            <Button
                              onClick={() => handleFetchProposals(item)}
                              className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-2.5 text-[11px] rounded"
                            >
                              {t("proposeBtn")}
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
          <Card className="bg-card border-border text-white min-h-[400px]">
            <CardHeader className="border-b border-border pb-2">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <MapPin className="h-4 w-4 text-emerald-500" />
                {t("proposalTitle")}
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4">
              {!activeItem ? (
                <div className="flex flex-col items-center justify-center py-20 text-muted-foreground text-xs gap-2">
                  <MapPin className="h-8 w-8 text-zinc-700 animate-bounce" />
                  {t("selectItemHint")}
                </div>
              ) : loadingProposals ? (
                <div className="text-center py-20 text-muted-foreground text-xs">{t("loadingProposals")}</div>
              ) : proposalsData ? (
                <div className="flex flex-col gap-4 text-xs">
                  <div className="bg-muted/30 p-3 rounded-lg border border-border grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div>
                      <span className="text-[10px] text-muted-foreground">{t("lotNo")}</span>
                      <div className="font-semibold text-zinc-200">{proposalsData.lotNo}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-muted-foreground">{t("colItem")}</span>
                      <div className="font-semibold text-zinc-200 truncate">{proposalsData.itemName}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-muted-foreground">{t("putawayQty")}</span>
                      <div className="font-semibold text-zinc-200">{proposalsData.qty.toLocaleString()}</div>
                    </div>
                    <div>
                      <span className="text-[10px] text-muted-foreground">{t("currentLocation")}</span>
                      <div className="font-semibold text-zinc-200">{activeItem.locationCode}</div>
                    </div>
                  </div>

                  {render2DGridMap()}

                  <div className="flex flex-col gap-2 mt-2">
                    <span className="text-muted-foreground font-semibold">{t("candidatesTitle")}</span>
                    <div className="overflow-x-auto border border-border rounded-lg">
                      <Table className="text-xs">
                        <TableHeader className="border-b border-border bg-background/40">
                          <TableRow className="border-b border-border">
                            <TableHead className="text-muted-foreground w-12 text-center">{t("colSelect")}</TableHead>
                            <TableHead className="text-muted-foreground">{t("colLocation")}</TableHead>
                            <TableHead className="text-muted-foreground">{t("colZone")}</TableHead>
                            <TableHead className="text-muted-foreground text-right">{t("colScore")}</TableHead>
                            <TableHead className="text-muted-foreground">{t("colReason")}</TableHead>
                            <TableHead className="text-muted-foreground text-center">{t("colActions")}</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {proposalsData.proposals.map((candidate) => {
                            const isSelected = selectedCandidate?.locationId === candidate.locationId;
                            return (
                              <TableRow 
                                key={candidate.locationId}
                                className={`border-b border-border/50 hover:bg-muted/20 ${isSelected ? "bg-emerald-500/5 border-l-2 border-l-emerald-500" : ""}`}
                              >
                                <TableCell className="text-center">
                                  <input 
                                    type="radio" 
                                    name="selected_proposal" 
                                    checked={isSelected}
                                    onChange={() => setSelectedCandidate(candidate)}
                                    className="accent-emerald-500 cursor-pointer"
                                  />
                                </TableCell>
                                <TableCell className="font-semibold text-zinc-200">{candidate.locationCode}</TableCell>
                                <TableCell className="text-muted-foreground">{candidate.zoneCode}</TableCell>
                                <TableCell className="text-right text-emerald-400 font-bold">{candidate.score} {t("scoreUnit")}</TableCell>
                                <TableCell className="text-zinc-300 max-w-[200px] truncate" title={candidate.reason}>
                                  {candidate.reason}
                                </TableCell>
                                <TableCell className="text-center">
                                  {candidate.proposalId !== "00000000-0000-0000-0000-000000000000" && (
                                    <Button
                                      onClick={() => setRejectingProposal(candidate)}
                                      className="bg-rose-950/20 hover:bg-rose-950 text-rose-500 hover:text-rose-400 h-6 px-2 text-[10px] border border-rose-950 rounded"
                                    >
                                      {t("rejectBtn")}
                                    </Button>
                                  )}
                                </TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    </div>
                  </div>

                  {selectedCandidate && (
                    <div className="flex justify-between items-center bg-zinc-850 p-4 border border-border rounded-lg mt-4 bg-background/20">
                      <div className="flex items-center gap-2 text-zinc-300">
                        <CheckCircle2 className="h-5 w-5 text-emerald-500" />
                        <span>{t("confirmPutaway", { location: selectedCandidate.locationCode, score: selectedCandidate.score })}</span>
                      </div>
                      <Button
                        onClick={handleConfirmPutaway}
                        disabled={submitting}
                        className="bg-emerald-600 hover:bg-emerald-500 text-white px-6 py-2 text-xs rounded font-semibold"
                      >
                        {submitting ? tc("processing") : t("confirmBtn")}
                      </Button>
                    </div>
                  )}
                </div>
              ) : (
                <div className="text-center py-20 text-muted-foreground text-xs">{t("noSuitableSlots")}</div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {rejectingProposal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-card border border-border rounded-lg w-full max-w-sm p-6 text-white text-xs flex flex-col gap-4 shadow-xl">
            <div>
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <XCircle className="h-5 w-5 text-rose-500" />
                {t("rejectDialogTitle")}
              </h3>
              <p className="text-[10px] text-muted-foreground mt-1">{t("rejectDialogHint", { location: rejectingProposal.locationCode })}</p>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-muted-foreground">{t("rejectReasonLabel")}</label>
              <select
                value={rejectReasonCode}
                onChange={(e) => setRejectReasonCode(e.target.value)}
                className="bg-muted border border-border text-white rounded p-2 h-9 focus:outline-none"
              >
                <option value="LOC_FULL">{t("reasonLocFull")}</option>
                <option value="LOC_DIRTY">{t("reasonLocDirty")}</option>
                <option value="LOC_WRONG_ZONE">{t("reasonWrongZone")}</option>
                <option value="LOC_BLOCKED">{t("reasonBlocked")}</option>
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-muted-foreground">{t("rejectNoteLabel")}</label>
              <Input
                placeholder={t("rejectNotePlaceholder")}
                value={rejectNote}
                onChange={(e) => setRejectNote(e.target.value)}
                className="bg-muted border-border text-white h-9 text-xs"
              />
            </div>

            <div className="flex gap-2 justify-end border-t border-border pt-4 mt-2">
              <Button
                variant="ghost"
                onClick={() => setRejectingProposal(null)}
                className="text-muted-foreground hover:text-white text-xs"
              >
                {tc("cancel")}
              </Button>
              <Button
                onClick={handleRejectPutaway}
                className="bg-rose-600 hover:bg-rose-500 text-white text-xs"
              >
                {t("rejectConfirmBtn")}
              </Button>
            </div>
          </div>
        </div>
      )}
    </PageShell>
  );
}
