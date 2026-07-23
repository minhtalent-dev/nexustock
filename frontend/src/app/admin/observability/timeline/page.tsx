"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { getTimeline, getTraceDetail } from "@/features/observability/api";
import { ActivityTimelineEntry, TraceDetail, TraceLog } from "@/features/observability/types";
import { WebhookDelivery } from "@/features/webhook/types";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { GitFork, Activity, ChevronDown, ChevronUp, Eye } from "lucide-react";

export default function TimelinePage() {
  const t = useTranslations("Admin.timeline");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [items, setItems] = useState<ActivityTimelineEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [entityType, setEntityType] = useState("all");
  const [severity, setSeverity] = useState("all");
  const [traceId, setTraceId] = useState("");
  const [loading, setLoading] = useState(false);

  const [selectedTraceId, setSelectedTraceId] = useState<string | null>(null);
  const [traceDetail, setTraceDetail] = useState<TraceDetail | null>(null);
  const [traceLoading, setTraceLoading] = useState(false);

  const [expandedItems, setExpandedItems] = useState<Record<string, boolean>>({});

  const [refreshTrigger, setRefreshTrigger] = useState(0);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      try {
        const data = await getTimeline({
          entityType: entityType === "all" ? undefined : entityType,
          severity: severity === "all" ? undefined : severity,
          traceId: traceId.trim() || undefined,
          page,
          pageSize
        });
        if (active) {
          setItems(data.items);
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
  }, [entityType, severity, traceId, page, pageSize, refreshTrigger, t, tErrors]);

  useEffect(() => {
    if (!selectedTraceId) {
      const handler = setTimeout(() => {
        setTraceDetail(null);
      }, 0);
      return () => clearTimeout(handler);
    }
    let active = true;
    async function load() {
      setTraceLoading(true);
      try {
        const data = await getTraceDetail(selectedTraceId!);
        if (active) {
          setTraceDetail(data);
        }
      } catch (err) {
        const { codeLabel, message } = resolveApiError(err, tErrors);
        showApiErrorToast(codeLabel, message || t("errors.traceFailed"));
      } finally {
        if (active) setTraceLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [selectedTraceId, t, tErrors]);

  const handleRefresh = () => {
    setRefreshTrigger(prev => prev + 1);
  };

  const toggleMetadata = (id: string) => {
    setExpandedItems(prev => ({ ...prev, [id]: !prev[id] }));
  };

  const getSeverityColor = (sev: string) => {
    switch (sev) {
      case "critical":
        return "bg-red-500/15 text-red-400 border border-red-500/20";
      case "warning":
        return "bg-amber-500/15 text-amber-400 border border-amber-500/20";
      default:
        return "bg-muted text-muted-foreground border border-border/50";
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <PageShell className="gap-6">
      <div className="p-6 space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">{t("title")}</h1>
        <p className="text-muted-foreground text-sm mt-1">{t("subtitle")}</p>
      </div>

      <div className="flex flex-wrap gap-3 items-center">
        <div className="w-44">
          <Select value={entityType} onValueChange={(v) => { setEntityType(v); setPage(1); }}>
            <SelectTrigger id="timeline-entity-filter" className="bg-[#0f0f11]/60 border-border">
              <SelectValue placeholder={t("entityPlaceholder")} />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-border text-foreground">
              <SelectItem value="all">{t("allEntities")}</SelectItem>
              <SelectItem value="InboundOrder">{t("entityInboundOrder")}</SelectItem>
              <SelectItem value="Shipment">{t("entityShipment")}</SelectItem>
              <SelectItem value="InventoryMovement">{t("entityInventoryMovement")}</SelectItem>
              <SelectItem value="WebhookDelivery">{t("entityWebhookDelivery")}</SelectItem>
              <SelectItem value="Alert">{t("entityAlert")}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="w-44">
          <Select value={severity} onValueChange={(v) => { setSeverity(v); setPage(1); }}>
            <SelectTrigger id="timeline-severity-filter" className="bg-[#0f0f11]/60 border-border">
              <SelectValue placeholder={t("severityPlaceholder")} />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-border text-foreground">
              <SelectItem value="all">{t("allSeverities")}</SelectItem>
              <SelectItem value="info">{t("severityInfo")}</SelectItem>
              <SelectItem value="warning">{t("severityWarning")}</SelectItem>
              <SelectItem value="critical">{t("severityCritical")}</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Input
          id="timeline-trace-filter"
          placeholder={t("traceFilterPlaceholder")}
          value={traceId}
          onChange={(e) => { setTraceId(e.target.value); setPage(1); }}
          className="w-64 bg-[#0f0f11]/60 border-border placeholder-zinc-600 rounded-lg text-foreground"
        />

        <Button variant="outline" size="sm" onClick={handleRefresh} className="rounded-lg border-border">
          {tc("refresh")}
        </Button>
      </div>

      <Card className="border-border/80 bg-[#0f0f11]/40 rounded-xl">
        <CardContent className="pt-6">
          {loading ? (
            <p className="text-sm text-muted-foreground py-8 text-center animate-pulse">{t("loading")}</p>
          ) : items.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center italic">{t("empty")}</p>
          ) : (
            <div className="space-y-4">
              {items.map((item) => (
                <div key={item.id} className="p-4 border border-border/60 bg-background/10 hover:bg-background/20 transition-all duration-200 rounded-xl flex items-start gap-4">
                  <div className="p-2 bg-card rounded-lg border border-border text-muted-foreground mt-1">
                    <Activity className="h-4 w-4" />
                  </div>
                  <div className="flex-1 space-y-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <span className="text-foreground font-semibold text-sm">{item.title}</span>
                        <Badge variant="outline" className={getSeverityColor(item.severity)}>
                          {item.severity}
                        </Badge>
                      </div>
                      <span className="text-xs text-muted-foreground">
                        {new Date(item.createdAt).toLocaleString("vi-VN")}
                      </span>
                    </div>

                    {item.description && (
                      <p className="text-xs text-muted-foreground leading-relaxed bg-background/10 p-2 border border-zinc-850/40 rounded-lg">
                        {item.description}
                      </p>
                    )}

                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-[10px] text-muted-foreground">
                      <span className="font-mono bg-card px-1.5 py-0.5 rounded text-muted-foreground border border-border">
                        {item.entityType}
                      </span>
                      <span>
                        {tc("entityId")}: <span className="font-mono text-muted-foreground">{item.entityId}</span>
                      </span>
                      {item.actorName && (
                        <span>
                          {t("actor")}: <span className="text-muted-foreground">{item.actorName}</span>
                        </span>
                      )}
                      <span
                        onClick={() => setSelectedTraceId(item.traceId)}
                        className="cursor-pointer hover:underline text-emerald-400 flex items-center gap-1 font-mono"
                      >
                        {tc("traceId")}: {item.traceId} <Eye className="h-3 w-3" />
                      </span>
                    </div>

                    {item.metadataJson && (
                      <div className="space-y-1.5 pt-1">
                        <Button
                          variant="ghost"
                          size="xs"
                          onClick={() => toggleMetadata(item.id)}
                          className="h-6 text-muted-foreground hover:text-muted-foreground p-0 text-[10px] flex items-center gap-1 hover:bg-transparent"
                        >
                          {expandedItems[item.id] ? (
                            <>
                              {t("hideDetails")} <ChevronUp className="h-3 w-3" />
                            </>
                          ) : (
                            <>
                              {t("showDetails")} <ChevronDown className="h-3 w-3" />
                            </>
                          )}
                        </Button>
                        {expandedItems[item.id] && (
                          <pre className="text-[10px] bg-background/60 p-3 rounded-lg border border-zinc-850/80 text-muted-foreground overflow-x-auto max-h-48 font-mono leading-relaxed">
                            {(() => {
                              try {
                                return JSON.stringify(JSON.parse(item.metadataJson), null, 2);
                              } catch {
                                return item.metadataJson;
                              }
                            })()}
                          </pre>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              ))}

              {totalPages > 1 && (
                <div className="flex justify-between items-center mt-6 text-sm text-muted-foreground pt-4 border-t border-zinc-900">
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
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={selectedTraceId !== null} onOpenChange={(open) => !open && setSelectedTraceId(null)}>
        <DialogContent className="max-w-4xl bg-[#0f0f11] border-border text-foreground rounded-xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="text-lg font-bold flex items-center gap-2">
              <GitFork className="h-5 w-5 text-emerald-400" /> {t("traceDialogTitle")}
            </DialogTitle>
          </DialogHeader>
          {traceLoading ? (
            <p className="text-sm text-muted-foreground py-12 text-center animate-pulse">{t("loadingTrace")}</p>
          ) : traceDetail ? (
            <div className="space-y-6 py-2">
              <div className="p-3 bg-background/40 border border-border rounded-lg text-xs font-mono text-muted-foreground flex flex-col gap-1">
                <span>{tc("traceId")}: <span className="text-emerald-400">{traceDetail.traceId}</span></span>
              </div>

              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-foreground">{t("technicalLogs")}</h3>
                {traceDetail.traceLogs.length === 0 ? (
                  <p className="text-xs text-muted-foreground italic p-3 bg-background/10 border border-zinc-850 rounded-lg">{t("noTechnicalLogs")}</p>
                ) : (
                  <div className="max-h-60 overflow-y-auto border border-zinc-850 bg-background/10 rounded-lg divide-y divide-zinc-900">
                    <Table>
                      <TableHeader className="bg-card/30">
                        <TableRow className="hover:bg-transparent border-zinc-850">
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colTime")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colLevel")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colSpanSource")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colLogContent")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {traceDetail.traceLogs.map((l: TraceLog) => (
                          <TableRow key={l.id} className="hover:bg-card/20 border-zinc-900">
                            <TableCell className="text-[10px] text-muted-foreground py-1.5 font-mono">
                              {new Date(l.createdAt).toLocaleTimeString("vi-VN")}
                            </TableCell>
                            <TableCell className="py-1.5">
                              <Badge variant={l.level === "error" ? "destructive" : l.level === "warning" ? "secondary" : "outline"} className="text-[9px] px-1 py-0 h-4">
                                {l.level}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-[10px] text-muted-foreground py-1.5 font-mono">
                              {l.spanName} ({l.source})
                            </TableCell>
                            <TableCell className="text-xs text-muted-foreground py-1.5 leading-relaxed">
                              {l.message}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>

              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-foreground">{t("businessEvents")}</h3>
                {traceDetail.timelineEntries.length === 0 ? (
                  <p className="text-xs text-muted-foreground italic p-3 bg-background/10 border border-zinc-850 rounded-lg">{t("noBusinessEvents")}</p>
                ) : (
                  <div className="space-y-2 max-h-60 overflow-y-auto border border-zinc-850 bg-background/10 p-3 rounded-lg">
                    {traceDetail.timelineEntries.map((e) => (
                      <div key={e.id} className="relative pl-5 border-l border-border pb-2 text-xs">
                        <div className="absolute left-[-3.5px] top-1.5 h-1.5 w-1.5 rounded-full bg-emerald-400" />
                        <div className="flex justify-between items-center gap-2">
                          <span className="font-semibold text-muted-foreground">{e.title}</span>
                          <span className="text-[10px] text-muted-foreground">{new Date(e.createdAt).toLocaleString("vi-VN")}</span>
                        </div>
                        {e.description && <p className="text-muted-foreground mt-0.5 leading-relaxed">{e.description}</p>}
                        <div className="text-[9px] text-muted-foreground mt-0.5">
                          EntityType: {e.entityType} | {tc("entityId")}: {e.entityId}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-foreground">{t("webhookTransactions")}</h3>
                {traceDetail.webhookDeliveries.length === 0 ? (
                  <p className="text-xs text-muted-foreground italic p-3 bg-background/10 border border-zinc-850 rounded-lg">{t("noWebhookTransactions")}</p>
                ) : (
                  <div className="max-h-60 overflow-y-auto border border-zinc-850 bg-background/10 rounded-lg">
                    <Table>
                      <TableHeader className="bg-card/30">
                        <TableRow className="hover:bg-transparent border-zinc-850">
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colEventType")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{tc("status")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colRetry")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colHttpCode")}</TableHead>
                          <TableHead className="text-muted-foreground text-xs py-2 h-8">{t("colErrorDetail")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {traceDetail.webhookDeliveries.map((w: WebhookDelivery) => (
                          <TableRow key={w.id} className="hover:bg-card/20 border-zinc-900">
                            <TableCell className="text-[10px] text-muted-foreground py-1.5 font-mono">{w.eventType}</TableCell>
                            <TableCell className="py-1.5">
                              <Badge variant={w.status === "delivered" ? "default" : w.status === "deadLetter" ? "destructive" : "secondary"} className="text-[9px] px-1 py-0 h-4">
                                {w.status}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-xs text-muted-foreground py-1.5">{w.retryCount}</TableCell>
                            <TableCell className="text-xs text-muted-foreground py-1.5">{w.lastResponseCode ?? "—"}</TableCell>
                            <TableCell className="text-[10px] text-red-400 py-1.5 max-w-[200px] truncate" title={w.lastError}>
                              {w.lastError ?? "—"}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
    </PageShell>
  );
}
