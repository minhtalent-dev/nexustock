"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { getAlerts, ackAlert, resolveAlert } from "@/features/observability/api";
import { OperationalAlert } from "@/features/observability/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { toast } from "sonner";
import { AlertCircle, CheckCircle2, ShieldAlert, User } from "lucide-react";

export default function AlertCenterPage() {
  const t = useTranslations("Admin.alerts");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [alerts, setAlerts] = useState<OperationalAlert[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [status, setStatus] = useState<string>("all");
  const [severity, setSeverity] = useState<string>("all");
  const [loading, setLoading] = useState(false);

  const [selectedAlert, setSelectedAlert] = useState<OperationalAlert | null>(null);
  const [actionType, setActionType] = useState<"ack" | "resolve" | null>(null);
  const [actionNote, setActionNote] = useState("");
  const [actionLoading, setActionLoading] = useState(false);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      try {
        const data = await getAlerts({
          status: status === "all" ? undefined : status,
          severity: severity === "all" ? undefined : severity,
          page,
          pageSize
        });
        if (active) {
          setAlerts(data.items);
          setTotal(data.total);
        }
      } catch (err) {
        const { codeLabel, message } = resolveApiError(err, tErrors);
        showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [status, severity, page, pageSize, refreshTrigger, t, tErrors]);

  const handleActionSubmit = async () => {
    if (!selectedAlert || !actionType) return;
    setActionLoading(true);
    try {
      if (actionType === "ack") {
        await ackAlert(selectedAlert.id, actionNote);
        toast.success(t("toastAckSuccess"));
      } else {
        await resolveAlert(selectedAlert.id, actionNote);
        toast.success(t("toastResolveSuccess"));
      }
      setSelectedAlert(null);
      setActionType(null);
      setActionNote("");
      setRefreshTrigger(prev => prev + 1);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(
        codeLabel,
        message || (actionType === "ack" ? t("errors.ackFailed") : t("errors.resolveFailed"))
      );
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusBadge = (statusStr: string) => {
    switch (statusStr) {
      case "open":
        return <Badge variant="destructive" className="bg-red-500/10 text-red-400 border border-red-500/20">{t("statusOpenBadge")}</Badge>;
      case "acknowledged":
        return <Badge className="bg-amber-500/10 text-amber-400 border border-amber-500/20">{t("statusAckBadge")}</Badge>;
      case "resolved":
        return <Badge className="bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">{t("statusResolvedBadge")}</Badge>;
      default:
        return <Badge variant="outline">{statusStr}</Badge>;
    }
  };

  const getSeverityBadge = (sevStr: string) => {
    switch (sevStr) {
      case "critical":
        return <Badge variant="destructive">critical</Badge>;
      default:
        return <Badge variant="secondary">warning</Badge>;
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <PageShell className="gap-6">
      <div className="p-6 space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-white">{t("title")}</h1>
        <p className="text-muted-foreground text-sm mt-1">{t("subtitle")}</p>
      </div>

      <div className="flex flex-wrap gap-3 items-center">
        <div className="w-44">
          <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
            <SelectTrigger id="alert-status-filter" className="bg-[#0f0f11]/60 border-border">
              <SelectValue placeholder={t("statusPlaceholder")} />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-border text-white">
              <SelectItem value="all">{t("allStatuses")}</SelectItem>
              <SelectItem value="open">{t("statusOpen")}</SelectItem>
              <SelectItem value="acknowledged">{t("statusAcknowledged")}</SelectItem>
              <SelectItem value="resolved">{t("statusResolved")}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="w-44">
          <Select value={severity} onValueChange={(v) => { setSeverity(v); setPage(1); }}>
            <SelectTrigger id="alert-severity-filter" className="bg-[#0f0f11]/60 border-border">
              <SelectValue placeholder={t("severityPlaceholder")} />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-border text-white">
              <SelectItem value="all">{t("allSeverities")}</SelectItem>
              <SelectItem value="warning">{t("severityWarning")}</SelectItem>
              <SelectItem value="critical">{t("severityCritical")}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Button variant="outline" size="sm" onClick={() => setRefreshTrigger(prev => prev + 1)} className="rounded-lg border-border">
          {tc("refresh")}
        </Button>
      </div>

      <Card className="border-border/80 bg-[#0f0f11]/40 rounded-xl">
        <CardHeader>
          <CardTitle className="text-lg font-semibold text-white">{t("listTitle", { total })}</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-muted-foreground py-8 text-center animate-pulse">{t("loadingAlerts")}</p>
          ) : (
            <>
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader className="border-border">
                    <TableRow className="border-border hover:bg-transparent">
                      <TableHead className="text-muted-foreground">{t("colTitle")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colSeverity")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colStatus")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colValueThreshold")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colSourceModule")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colCreatedAt")}</TableHead>
                      <TableHead className="text-right text-muted-foreground">{t("colActions")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {alerts.length === 0 && (
                      <TableRow className="hover:bg-transparent">
                        <TableCell colSpan={7} className="text-center py-8 text-muted-foreground italic">
                          {t("empty")}
                        </TableCell>
                      </TableRow>
                    )}
                    {alerts.map((a) => (
                      <TableRow
                        key={a.id}
                        onClick={() => setSelectedAlert(a)}
                        className="cursor-pointer border-border/60 hover:bg-muted/20 transition-colors"
                      >
                        <TableCell className="font-semibold text-zinc-200">{a.title}</TableCell>
                        <TableCell>{getSeverityBadge(a.severity)}</TableCell>
                        <TableCell>{getStatusBadge(a.status)}</TableCell>
                        <TableCell className="font-mono text-zinc-300">
                          {a.metricValue !== undefined ? `${a.metricValue}/${a.thresholdValue ?? "—"}` : "—"}
                        </TableCell>
                        <TableCell className="text-muted-foreground">{a.sourceModule}</TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {new Date(a.createdAt).toLocaleString("vi-VN")}
                        </TableCell>
                        <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                          <div className="flex justify-end gap-1.5">
                            {a.status === "open" && (
                              <Button
                                size="xs"
                                variant="outline"
                                className="rounded-lg border-border text-xs text-amber-400 hover:text-amber-300 hover:bg-amber-500/5"
                                onClick={() => { setSelectedAlert(a); setActionType("ack"); }}
                              >
                                {t("ack")}
                              </Button>
                            )}
                            {(a.status === "open" || a.status === "acknowledged") && (
                              <Button
                                size="xs"
                                variant="outline"
                                className="rounded-lg border-border text-xs text-emerald-400 hover:text-emerald-300 hover:bg-emerald-500/5"
                                onClick={() => { setSelectedAlert(a); setActionType("resolve"); }}
                              >
                                {t("resolve")}
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              {totalPages > 1 && (
                <div className="flex justify-between items-center mt-6 text-sm text-muted-foreground">
                  <span>{tc("pageOf", { page, totalPages, total })}</span>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={page <= 1}
                      onClick={() => setPage(p => p - 1)}
                      className="rounded-lg border-border"
                    >
                      {tc("previous")}
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={page >= totalPages}
                      onClick={() => setPage(p => p + 1)}
                      className="rounded-lg border-border"
                    >
                      {tc("next")}
                    </Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!selectedAlert && actionType === null} onOpenChange={(open) => !open && setSelectedAlert(null)}>
        <DialogContent className="max-w-2xl bg-[#0f0f11] border-border text-white rounded-xl">
          <DialogHeader>
            <DialogTitle className="text-xl font-bold flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-red-500" /> {t("detailTitle")}
            </DialogTitle>
          </DialogHeader>
          {selectedAlert && (
            <div className="space-y-4 py-2">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 p-4 bg-background/40 rounded-lg border border-border/80">
                <div>
                  <span className="text-muted-foreground text-xs block">{t("alertType")}</span>
                  <span className="font-mono text-sm">{selectedAlert.alertType}</span>
                </div>
                <div>
                  <span className="text-muted-foreground text-xs block">{t("statusSeverity")}</span>
                  <div className="flex gap-2 mt-1">
                    {getStatusBadge(selectedAlert.status)}
                    {getSeverityBadge(selectedAlert.severity)}
                  </div>
                </div>
                <div>
                  <span className="text-muted-foreground text-xs block">{t("sourceModule")}</span>
                  <span className="text-zinc-200 text-sm font-semibold">{selectedAlert.sourceModule}</span>
                </div>
                {selectedAlert.traceId && (
                  <div>
                    <span className="text-muted-foreground text-xs block">{tc("traceId")}</span>
                    <span className="font-mono text-xs text-emerald-400 block break-all">{selectedAlert.traceId}</span>
                  </div>
                )}
              </div>

              <div>
                <Label className="text-muted-foreground text-xs">{t("messageInfo")}</Label>
                <p className="text-zinc-200 text-sm mt-1 bg-background/20 p-3 rounded-lg border border-zinc-850">{selectedAlert.message}</p>
              </div>

              {(selectedAlert.acknowledgedAt || selectedAlert.resolvedAt) && (
                <div className="space-y-2 p-3 bg-background/10 border border-border/60 rounded-lg text-xs text-muted-foreground">
                  {selectedAlert.acknowledgedAt && (
                    <div className="flex items-center gap-2">
                      <User className="h-3.5 w-3.5 text-muted-foreground" />
                      <span>
                        {t("acknowledgedAt", {
                          at: new Date(selectedAlert.acknowledgedAt).toLocaleString("vi-VN"),
                          by: selectedAlert.acknowledgedBy ?? tc("system"),
                        })}
                      </span>
                    </div>
                  )}
                  {selectedAlert.resolvedAt && (
                    <div className="flex items-center gap-2">
                      <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500" />
                      <span>
                        {t("resolvedAt", {
                          at: new Date(selectedAlert.resolvedAt).toLocaleString("vi-VN"),
                          by: selectedAlert.resolvedBy ?? tc("system"),
                        })}
                      </span>
                    </div>
                  )}
                </div>
              )}

              <DialogFooter className="gap-2">
                {selectedAlert.status === "open" && (
                  <Button
                    variant="outline"
                    className="border-border text-amber-400 hover:bg-amber-500/5 rounded-lg"
                    onClick={() => setActionType("ack")}
                  >
                    {t("ackAlert")}
                  </Button>
                )}
                {(selectedAlert.status === "open" || selectedAlert.status === "acknowledged") && (
                  <Button
                    className="bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg"
                    onClick={() => setActionType("resolve")}
                  >
                    {t("resolveAlert")}
                  </Button>
                )}
                <Button variant="outline" className="border-border text-zinc-300 rounded-lg" onClick={() => setSelectedAlert(null)}>
                  {tc("close")}
                </Button>
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={actionType !== null} onOpenChange={(open) => !open && setActionType(null)}>
        <DialogContent className="max-w-md bg-[#0f0f11] border-border text-white rounded-xl">
          <DialogHeader>
            <DialogTitle className="text-lg font-bold flex items-center gap-2">
              {actionType === "ack" ? (
                <>
                  <ShieldAlert className="h-5 w-5 text-amber-500" /> {t("ackDialogTitle")}
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-5 w-5 text-emerald-500" /> {t("resolveDialogTitle")}
                </>
              )}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <p className="text-sm text-zinc-300">
              {actionType === "ack" ? t("ackDialogHint") : t("resolveDialogHint")}
            </p>
            <div className="space-y-1.5">
              <Label htmlFor="action-note" className="text-muted-foreground text-xs">{t("noteLabel")}</Label>
              <Input
                id="action-note"
                placeholder={t("notePlaceholder")}
                value={actionNote}
                onChange={(e) => setActionNote(e.target.value)}
                className="bg-[#151518] border-border text-white rounded-lg placeholder-zinc-600"
              />
            </div>
            <DialogFooter className="gap-2">
              <Button
                variant="outline"
                className="border-border text-zinc-300 rounded-lg"
                onClick={() => { setActionType(null); setActionNote(""); }}
                disabled={actionLoading}
              >
                {tc("cancel")}
              </Button>
              <Button
                onClick={handleActionSubmit}
                disabled={actionLoading}
                className={actionType === "ack" ? "bg-amber-600 hover:bg-amber-700 text-white rounded-lg" : "bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg"}
              >
                {actionLoading ? tc("processing") : tc("confirm")}
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>
    </div>
    </PageShell>
  );
}
