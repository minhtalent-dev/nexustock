"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import {
  laborApi,
  LaborSessionDto,
  CurrentShiftResponse,
  StartLaborSessionRequest,
} from "@/lib/labor-api";
import { Button } from "@/components/ui/button";
import { PageShell } from "@/components/layout/page-shell";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { Play, Pause, CheckCircle2, XCircle, RefreshCw, Plus, Clock, ShieldAlert } from "lucide-react";

const STATUS_COLORS: Record<string, string> = {
  Running: "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400",
  Paused: "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400",
  Completed: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  Cancelled: "bg-rose-100 text-rose-800 dark:bg-rose-900/30 dark:text-rose-400",
};

const SESSION_STATUS_KEYS = ["ALL", "Running", "Paused", "Completed", "Cancelled"] as const;
const SOURCE_TASK_TYPE_KEYS = ["Manual", "MobileTask", "PickTask", "WavePickTask"] as const;
const OPERATION_TYPE_KEYS = ["Picking", "Putaway", "Replenishment", "Movement", "Packing", "Count", "Manual"] as const;

export default function LaborSessionsPage() {
  const t = useTranslations("Admin.labor");
  const tc = useTranslations("Admin.common");
  const tActions = useTranslations("Common.actions");
  const tErrors = useTranslations("Errors");

  const [sessions, setSessions] = React.useState<LaborSessionDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [page] = React.useState(1);
  const [statusFilter, setStatusFilter] = React.useState("ALL");
  const [shiftInfo, setShiftInfo] = React.useState<CurrentShiftResponse | null>(null);
  const [now, setNow] = React.useState<number>(0);

  const [startOpen, setStartOpen] = React.useState(false);
  const [sourceTaskType, setSourceTaskType] = React.useState("Manual");
  const [sourceTaskId, setSourceTaskId] = React.useState("");
  const [operationType, setOperationType] = React.useState("Picking");
  const [locationId, setLocationId] = React.useState("");
  const [starting, setStarting] = React.useState(false);

  const [cancelOpen, setCancelOpen] = React.useState(false);
  const [cancellingId, setCancellingId] = React.useState<string | null>(null);
  const [cancelReason, setCancelReason] = React.useState("");
  const [cancelling, setCancelling] = React.useState(false);

  const translateSessionStatus = (status: string) => t(`sessionStatus.${status}` as "sessionStatus.Running");
  const translateOperation = (operation: string) => t(`operations.${operation}` as "operations.Picking");
  const translateSourceTaskType = (type: string) => t(`sourceTaskTypes.${type}` as "sourceTaskTypes.Manual");

  const fetchShift = async () => {
    try {
      const res = await laborApi.getCurrentShift();
      setShiftInfo(res);
    } catch {
      // Ignored: Shift might not be running or initialized yet.
    }
  };

  const fetchSessions = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await laborApi.listSessions({
        status: statusFilter === "ALL" ? undefined : statusFilter,
        page,
        pageSize: 10,
      });
      setSessions(res.items ?? []);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadSessionsFailed"));
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, t, tErrors]);

  React.useEffect(() => {
    queueMicrotask(() => {
      void fetchShift();
      void fetchSessions();
    });
  }, [fetchSessions]);

  React.useEffect(() => {
    const timer = setInterval(() => {
      setNow(Date.now());
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const handleStartSession = async () => {
    if (sourceTaskType !== "Manual" && !sourceTaskId.trim()) {
      showApiErrorToast(t("errors.sourceTaskIdRequired"), t("errors.sourceTaskIdRequired"));
      return;
    }
    setStarting(true);
    try {
      const req: StartLaborSessionRequest = {
        sourceTaskType,
        sourceTaskId: sourceTaskId.trim() || undefined,
        operationType,
        locationId: locationId.trim() || undefined,
      };
      const res = await laborApi.startSession(req);
      showSuccess(t("toastSessionStarted", { status: translateSessionStatus(res.status) }));
      setStartOpen(false);
      setSourceTaskId("");
      setLocationId("");
      fetchSessions();
      fetchShift();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.startFailed"));
    } finally {
      setStarting(false);
    }
  };

  const handlePause = async (id: string) => {
    try {
      await laborApi.pauseSession(id);
      showSuccess(t("toastSessionPaused"));
      fetchSessions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.pauseFailed"));
    }
  };

  const handleResume = async (id: string) => {
    try {
      await laborApi.resumeSession(id);
      showSuccess(t("toastSessionResumed"));
      fetchSessions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.resumeFailed"));
    }
  };

  const handleComplete = async (id: string) => {
    try {
      await laborApi.completeSession(id);
      showSuccess(t("toastSessionCompleted"));
      fetchSessions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.completeFailed"));
    }
  };

  const openCancelDialog = (id: string) => {
    setCancellingId(id);
    setCancelReason("");
    setCancelOpen(true);
  };

  const handleCancel = async () => {
    if (!cancellingId) return;
    if (!cancelReason.trim()) {
      showApiErrorToast(t("errors.reasonRequired"), t("errors.reasonRequired"));
      return;
    }
    setCancelling(true);
    try {
      await laborApi.cancelSession(cancellingId, cancelReason.trim());
      showSuccess(t("toastSessionCancelled"));
      setCancelOpen(false);
      fetchSessions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.cancelFailed"));
    } finally {
      setCancelling(false);
    }
  };

  const renderLiveDuration = (session: LaborSessionDto) => {
    if (session.status !== "Running") {
      const totalSec = session.durationSeconds;
      const h = Math.floor(totalSec / 3600);
      const m = Math.floor((totalSec % 3600) / 60);
      const s = totalSec % 60;
      return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
    }

    const startMs = new Date(session.startedAt).getTime();
    const elapsedSec = Math.max(0, Math.floor((now - startMs) / 1000) - session.pausedSeconds);
    const h = Math.floor(elapsedSec / 3600);
    const m = Math.floor((elapsedSec % 3600) / 60);
    const s = elapsedSec % 60;

    return (
      <span className="font-mono text-emerald-500 font-bold animate-pulse flex items-center gap-1">
        <Clock className="h-3 w-3" />
        {`${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`}
      </span>
    );
  };

  return (
    <PageShell className="gap-6">
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground">{t("sessionsTitle")}</h1>
          <p className="text-muted-foreground text-sm">{t("sessionsSubtitle")}</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={fetchSessions} disabled={loading} className="gap-2">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            {tc("refresh")}
          </Button>
          <Button onClick={() => setStartOpen(true)} className="gap-2">
            <Plus className="h-4 w-4" /> {t("startNewSession")}
          </Button>
        </div>
      </div>

      {shiftInfo && (
        <Card className="bg-emerald-50/20 border-emerald-500/20 dark:bg-emerald-950/10">
          <CardContent className="flex items-center justify-between p-4 flex-wrap gap-2 text-sm">
            <div className="flex items-center gap-2">
              <div className="h-2.5 w-2.5 rounded-full bg-emerald-500 animate-ping" />
              <span className="font-medium text-muted-foreground">{t("activeShift")}</span>
              <strong className="text-foreground">{shiftInfo.shiftCode}</strong>
            </div>
            <div className="text-muted-foreground text-xs">
              {t("startedAt")} <strong>{new Date(shiftInfo.startedAt).toLocaleTimeString()}</strong>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="py-3 flex flex-row items-center justify-between border-b bg-muted/20">
          <CardTitle className="text-sm font-semibold">{t("logTitle")}</CardTitle>
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[150px] h-8">
              <SelectValue placeholder={t("filterStatus")} />
            </SelectTrigger>
            <SelectContent>
              {SESSION_STATUS_KEYS.map((key) => (
                <SelectItem key={key} value={key}>
                  {t(`sessionStatus.${key}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("colOperation")}</TableHead>
                <TableHead>{t("colSourceTask")}</TableHead>
                <TableHead>{t("colUserId")}</TableHead>
                <TableHead>{t("colStatus")}</TableHead>
                <TableHead>{t("colDuration")}</TableHead>
                <TableHead>{t("colStartedAt")}</TableHead>
                <TableHead className="text-right">{tc("actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading && sessions.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                    {t("loadingSessions")}
                  </TableCell>
                </TableRow>
              ) : sessions.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                    {t("emptySessions")}
                  </TableCell>
                </TableRow>
              ) : (
                sessions.map((session) => (
                  <TableRow key={session.id}>
                    <TableCell className="font-semibold">{translateOperation(session.operationType)}</TableCell>
                    <TableCell>
                      <span className="text-xs text-muted-foreground block">
                        {translateSourceTaskType(session.sourceTaskType)}
                      </span>
                      <span className="font-mono text-xs text-foreground">{session.sourceTaskId || tc("notAvailable")}</span>
                    </TableCell>
                    <TableCell className="text-xs">{session.userId}</TableCell>
                    <TableCell>
                      <Badge className={STATUS_COLORS[session.status] || "bg-gray-100"}>
                        {translateSessionStatus(session.status)}
                      </Badge>
                    </TableCell>
                    <TableCell>{renderLiveDuration(session)}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {new Date(session.startedAt).toLocaleString()}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1.5">
                        {session.status === "Running" && (
                          <>
                            <Button variant="outline" size="xs" onClick={() => handlePause(session.id)} className="h-7 text-xs gap-1 border-amber-500/30 text-amber-600 hover:bg-amber-500/10">
                              <Pause className="h-3.5 w-3.5" /> {t("pause")}
                            </Button>
                            <Button variant="outline" size="xs" onClick={() => handleComplete(session.id)} className="h-7 text-xs gap-1 border-emerald-500/30 text-emerald-600 hover:bg-emerald-500/10">
                              <CheckCircle2 className="h-3.5 w-3.5" /> {t("complete")}
                            </Button>
                          </>
                        )}
                        {session.status === "Paused" && (
                          <>
                            <Button variant="outline" size="xs" onClick={() => handleResume(session.id)} className="h-7 text-xs gap-1 border-green-500/30 text-green-600 hover:bg-green-500/10">
                              <Play className="h-3.5 w-3.5" /> {t("resume")}
                            </Button>
                            <Button variant="outline" size="xs" onClick={() => handleComplete(session.id)} className="h-7 text-xs gap-1 border-emerald-500/30 text-emerald-600 hover:bg-emerald-500/10">
                              <CheckCircle2 className="h-3.5 w-3.5" /> {t("complete")}
                            </Button>
                          </>
                        )}
                        {(session.status === "Running" || session.status === "Paused") && (
                          <Button variant="outline" size="xs" onClick={() => openCancelDialog(session.id)} className="h-7 text-xs gap-1 border-rose-500/30 text-rose-600 hover:bg-rose-500/10">
                            <XCircle className="h-3.5 w-3.5" /> {t("cancel")}
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={startOpen} onOpenChange={setStartOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("startDialogTitle")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <label className="text-sm font-semibold">{t("sourceTaskType")}</label>
              <Select value={sourceTaskType} onValueChange={setSourceTaskType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SOURCE_TASK_TYPE_KEYS.map((key) => (
                    <SelectItem key={key} value={key}>
                      {t(`sourceTaskTypes.${key}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-semibold">
                {t("sourceTaskId")} {sourceTaskType !== "Manual" && <span className="text-rose-500">*</span>}
              </label>
              <Input
                placeholder={sourceTaskType === "Manual" ? t("optionalGuidPlaceholder") : t("requiredTaskGuidPlaceholder")}
                value={sourceTaskId}
                onChange={(e) => setSourceTaskId(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-semibold">{t("operationType")}</label>
              <Select value={operationType} onValueChange={setOperationType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {OPERATION_TYPE_KEYS.map((key) => (
                    <SelectItem key={key} value={key}>
                      {t(`operations.${key}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-semibold">{t("locationIdOptional")}</label>
              <Input
                placeholder={t("enterLocationGuidPlaceholder")}
                value={locationId}
                onChange={(e) => setLocationId(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStartOpen(false)}>{tc("cancel")}</Button>
            <Button onClick={handleStartSession} disabled={starting}>
              {starting ? t("starting") : t("startTimer")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-rose-600">
              <ShieldAlert className="h-5 w-5" /> {t("cancelDialogTitle")}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <p className="text-sm text-muted-foreground">{t("cancelDialogDescription")}</p>
            <div className="space-y-2">
              <label className="text-sm font-semibold">{t("reason")}</label>
              <Input
                placeholder={t("cancelReasonPlaceholder")}
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>{tActions("back")}</Button>
            <Button variant="destructive" onClick={handleCancel} disabled={cancelling}>
              {cancelling ? t("cancelling") : t("confirmAbort")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PageShell>
  );
}
