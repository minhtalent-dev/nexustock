"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
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
import { AlertCircle, UserCheck, ShieldAlert, CheckCircle2, History, Loader2 } from "lucide-react";

interface ExceptionDto {
  id: string;
  code: string;
  type: string;
  severity: string;
  status: string;
  referenceType: string;
  referenceId: string;
  locationId?: string;
  lotNo?: string;
  qty: number;
  reasonCode: string;
  note?: string;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

interface ExceptionEventDto {
  id: string;
  transition: string;
  actor: string;
  note?: string;
  createdAt: string;
}

export default function ExceptionsPage() {
  const t = useTranslations("Admin.exceptions");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [exceptions, setExceptions] = useState<ExceptionDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page] = useState(1);
  const [pageSize] = useState(20);

  const [severityFilter, setSeverityFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");

  const [selectedException, setSelectedException] = useState<ExceptionDto | null>(null);
  const [events, setEvents] = useState<ExceptionEventDto[]>([]);
  const [loadingEvents, setLoadingEvents] = useState(false);

  const [isAssignOpen, setIsAssignOpen] = useState(false);
  const [owner, setOwner] = useState("");
  const [slaHours, setSlaHours] = useState(4);
  const [assigning, setAssigning] = useState(false);

  const [isResolveOpen, setIsResolveOpen] = useState(false);
  const [resolveAction, setResolveAction] = useState("CORRECTIVE_TRANSACTION");
  const [resolveReason, setResolveReason] = useState("");
  const [resolveNote, setResolveNote] = useState("");
  const [resolving, setResolving] = useState(false);

  const fetchExceptions = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<{ items: ExceptionDto[]; totalCount: number }>("/exceptions/open", {
        params: {
          severity: severityFilter || undefined,
          type: typeFilter || undefined,
          page,
          pageSize,
        },
      });
      setExceptions(res.data.items);
      setTotalCount(res.data.totalCount);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, severityFilter, typeFilter, t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchExceptions());
  }, [fetchExceptions]);

  const viewDetails = async (exc: ExceptionDto) => {
    setSelectedException(exc);
    setLoadingEvents(true);
    try {
      const res = await api.get<ExceptionEventDto[]>(`/exceptions/${exc.id}/events`);
      setEvents(res.data);
    } catch {
      showApiErrorToast("", t("errors.loadEventsFailed"));
    } finally {
      setLoadingEvents(false);
    }
  };

  const handleAssign = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedException) return;
    setAssigning(true);

    try {
      await api.post(`/exceptions/${selectedException.id}/assign`, { owner, slaHours });
      showSuccess(t("toastAssignSuccess"));
      setIsAssignOpen(false);
      viewDetails(selectedException);
      fetchExceptions();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.assignFailed"));
    } finally {
      setAssigning(false);
    }
  };

  const handleResolve = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedException) return;
    setResolving(true);

    try {
      await api.post(`/exceptions/${selectedException.id}/resolve`, {
        action: resolveAction,
        reasonCode: resolveReason || selectedException.reasonCode,
        note: resolveNote,
      });
      showSuccess(t("toastResolveSuccess"));
      setIsResolveOpen(false);
      setSelectedException(null);
      fetchExceptions();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.resolveFailed"));
    } finally {
      setResolving(false);
    }
  };

  const getSeverityBadge = (sev: string) => {
    switch (sev) {
      case "CRITICAL":
        return <Badge variant="destructive">{t("severityCritical")}</Badge>;
      case "HIGH":
        return <Badge className="bg-orange-500 hover:bg-orange-600 text-foreground">{t("severityHigh")}</Badge>;
      case "MEDIUM":
        return <Badge className="bg-yellow-500 hover:bg-yellow-600 text-foreground">{t("severityMedium")}</Badge>;
      default:
        return <Badge variant="secondary">{t("severityLow")}</Badge>;
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Resolved":
        return <Badge className="bg-green-600 text-white">{t("statusResolved")}</Badge>;
      case "In_Progress":
        return <Badge className="bg-blue-600 text-white">{t("statusInProgress")}</Badge>;
      case "Cancelled":
        return <Badge variant="outline">{t("statusCancelled")}</Badge>;
      default:
        return <Badge variant="secondary">{t("statusPending")}</Badge>;
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{t("title")}</h1>
          <p className="text-muted-foreground text-sm">{t("subtitle")}</p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">{t("pendingCardTitle")}</CardTitle>
            <AlertCircle className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalCount}</div>
          </CardContent>
        </Card>
      </div>

      <div className="flex items-center gap-4 bg-card p-4 rounded-lg border">
        <div className="space-y-1 w-48">
          <Label className="text-xs">{t("severityFilterLabel")}</Label>
          <select
            className="w-full bg-background border rounded px-2 py-1 text-sm h-9"
            value={severityFilter}
            onChange={(e) => setSeverityFilter(e.target.value)}
          >
            <option value="">{t("filterAll")}</option>
            <option value="CRITICAL">{t("severityCritical")}</option>
            <option value="HIGH">{t("severityHigh")}</option>
            <option value="MEDIUM">{t("severityMedium")}</option>
            <option value="LOW">{t("severityLow")}</option>
          </select>
        </div>
        <div className="space-y-1 w-48">
          <Label className="text-xs">{t("typeFilterLabel")}</Label>
          <Input
            placeholder={t("typeFilterPlaceholder")}
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="h-9"
          />
        </div>
        <Button variant="secondary" onClick={() => { setSeverityFilter(""); setTypeFilter(""); }} className="mt-5 h-9">
          {t("resetFilters")}
        </Button>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        <div className="md:col-span-2 bg-card border rounded-lg overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("colCode")}</TableHead>
                <TableHead>{t("colType")}</TableHead>
                <TableHead>{t("colSeverity")}</TableHead>
                <TableHead>{t("colStatus")}</TableHead>
                <TableHead>{t("colCreatedAt")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center h-24">
                    <Loader2 className="h-6 w-6 animate-spin mx-auto text-muted-foreground" />
                  </TableCell>
                </TableRow>
              ) : exceptions.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center h-24 text-muted-foreground">
                    {t("empty")}
                  </TableCell>
                </TableRow>
              ) : (
                exceptions.map((exc) => (
                  <TableRow
                    key={exc.id}
                    className={`cursor-pointer hover:bg-muted ${selectedException?.id === exc.id ? "bg-muted" : ""}`}
                    onClick={() => viewDetails(exc)}
                  >
                    <TableCell className="font-semibold">{exc.code}</TableCell>
                    <TableCell>{exc.type}</TableCell>
                    <TableCell>{getSeverityBadge(exc.severity)}</TableCell>
                    <TableCell>{getStatusBadge(exc.status)}</TableCell>
                    <TableCell>{new Date(exc.createdAt).toLocaleString("vi-VN")}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>

        <div className="bg-card border rounded-lg p-6 space-y-6">
          {selectedException ? (
            <>
              <div className="flex items-start justify-between border-b pb-4">
                <div>
                  <h2 className="text-lg font-bold">{selectedException.code}</h2>
                  <p className="text-xs text-muted-foreground">ID: {selectedException.id}</p>
                </div>
                <div className="flex flex-col gap-1 items-end">
                  {getStatusBadge(selectedException.status)}
                  {getSeverityBadge(selectedException.severity)}
                </div>
              </div>

              <div className="space-y-4 text-sm border-b pb-4">
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">{t("typeLabel")}</span>
                  <span className="font-medium text-right">{selectedException.type}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">{t("lotLabel")}</span>
                  <span className="font-medium text-right">{selectedException.lotNo || "-"}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">{t("qtyLabel")}</span>
                  <span className="font-medium text-right">{selectedException.qty}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">{t("locationLabel")}</span>
                  <span className="font-medium text-right">{selectedException.locationId || "-"}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">{t("referenceLabel")}</span>
                  <span className="font-medium text-right text-xs truncate max-w-[150px]" title={selectedException.referenceId}>
                    {selectedException.referenceId}
                  </span>
                </div>
                <div className="space-y-1">
                  <span className="text-muted-foreground">{t("noteLabel")}</span>
                  <p className="bg-muted p-2 rounded text-xs italic">{selectedException.note || t("noNote")}</p>
                </div>
              </div>

              {selectedException.status !== "Resolved" && selectedException.status !== "Cancelled" && (
                <div className="flex items-center gap-2 border-b pb-4">
                  <Button variant="outline" size="sm" onClick={() => setIsAssignOpen(true)} className="flex-1 gap-1">
                    <UserCheck className="h-4 w-4" /> {t("assignBtn")}
                  </Button>
                  <Button size="sm" onClick={() => setIsResolveOpen(true)} className="flex-1 gap-1">
                    <CheckCircle2 className="h-4 w-4" /> {t("resolveBtn")}
                  </Button>
                </div>
              )}

              <div className="space-y-3">
                <h3 className="text-sm font-semibold flex items-center gap-1.5">
                  <History className="h-4 w-4 text-muted-foreground" /> {t("timelineTitle")}
                </h3>
                {loadingEvents ? (
                  <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                ) : (
                  <div className="relative border-l pl-4 ml-2 space-y-4">
                    {events.map((e) => (
                      <div key={e.id} className="relative">
                        <div className="absolute -left-[21px] top-1.5 h-2.5 w-2.5 rounded-full border bg-background" />
                        <div className="space-y-0.5">
                          <span className="text-xs font-semibold block">{e.transition}</span>
                          <span className="text-[10px] text-muted-foreground block">
                            {t("timelineBy", {
                              actor: e.actor,
                              at: new Date(e.createdAt).toLocaleString("vi-VN"),
                            })}
                          </span>
                          {e.note && <p className="text-[11px] italic text-muted-foreground bg-muted/50 p-1 rounded mt-0.5">{e.note}</p>}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          ) : (
            <div className="h-full flex flex-col items-center justify-center text-center text-muted-foreground py-12">
              <ShieldAlert className="h-8 w-8 mb-2" />
              <p className="text-sm">{t("selectHint")}</p>
            </div>
          )}
        </div>
      </div>

      <Dialog open={isAssignOpen} onOpenChange={setIsAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("assignDialogTitle")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleAssign} className="space-y-4">
            <div className="space-y-1">
              <Label>{t("ownerLabel")}</Label>
              <Input
                placeholder={t("ownerPlaceholder")}
                value={owner}
                onChange={(e) => setOwner(e.target.value)}
                required
              />
            </div>
            <div className="space-y-1">
              <Label>{t("slaHoursLabel")}</Label>
              <Input
                type="number"
                value={slaHours}
                onChange={(e) => setSlaHours(parseInt(e.target.value))}
                min={1}
                required
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setIsAssignOpen(false)}>
                {tc("cancel")}
              </Button>
              <Button type="submit" disabled={assigning}>
                {assigning && <Loader2 className="h-4 w-4 animate-spin mr-1" />} {t("confirmAssign")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={isResolveOpen} onOpenChange={setIsResolveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("resolveDialogTitle")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleResolve} className="space-y-4">
            <div className="space-y-1">
              <Label>{t("resolveActionLabel")}</Label>
              <select
                className="w-full bg-background border rounded px-2 py-1.5 text-sm h-10"
                value={resolveAction}
                onChange={(e) => setResolveAction(e.target.value)}
              >
                <option value="CORRECTIVE_TRANSACTION">{t("resolveActionCorrective")}</option>
                <option value="CANCEL">{t("resolveActionCancel")}</option>
              </select>
            </div>
            <div className="space-y-1">
              <Label>{t("resolveReasonLabel")}</Label>
              <Input
                placeholder={t("resolveReasonPlaceholder")}
                value={resolveReason}
                onChange={(e) => setResolveReason(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>{t("resolveNoteLabel")}</Label>
              <textarea
                className="w-full bg-background border rounded p-2 text-sm h-20"
                placeholder={t("resolveNotePlaceholder")}
                value={resolveNote}
                onChange={(e) => setResolveNote(e.target.value)}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setIsResolveOpen(false)}>
                {tc("cancel")}
              </Button>
              <Button type="submit" disabled={resolving}>
                {resolving && <Loader2 className="h-4 w-4 animate-spin mr-1" />} {t("confirmResolve")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
    </PageShell>
  );
}
