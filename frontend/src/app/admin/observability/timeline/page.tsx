"use client";

import { useEffect, useState } from "react";
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
import { showError } from "@/lib/toast";
import { GitFork, Activity, ChevronDown, ChevronUp, Eye } from "lucide-react";

export default function TimelinePage() {
  const [items, setItems] = useState<ActivityTimelineEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [entityType, setEntityType] = useState("all");
  const [severity, setSeverity] = useState("all");
  const [traceId, setTraceId] = useState("");
  const [loading, setLoading] = useState(false);

  // Trace detail dialog state
  const [selectedTraceId, setSelectedTraceId] = useState<string | null>(null);
  const [traceDetail, setTraceDetail] = useState<TraceDetail | null>(null);
  const [traceLoading, setTraceLoading] = useState(false);

  // Collapsed metadata tracking
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
      } catch {
        showError("Không thể tải dòng thời gian hoạt động.");
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [entityType, severity, traceId, page, pageSize, refreshTrigger]);

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
      } catch {
        showError("Không thể lấy chi tiết Trace.");
      } finally {
        if (active) setTraceLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [selectedTraceId]);

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
        return "bg-zinc-800 text-zinc-400 border border-zinc-700/50";
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="p-6 space-y-4">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-white">Dòng thời gian hoạt động</h1>
        <p className="text-zinc-400 text-sm mt-1">Truy vết dòng thời gian hoạt động của các đơn hàng và đối tượng nghiệp vụ kho.</p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="w-44">
          <Select value={entityType} onValueChange={(v) => { setEntityType(v); setPage(1); }}>
            <SelectTrigger id="timeline-entity-filter" className="bg-[#0f0f11]/60 border-zinc-800">
              <SelectValue placeholder="Đối tượng" />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-zinc-800 text-white">
              <SelectItem value="all">Tất cả đối tượng</SelectItem>
              <SelectItem value="InboundOrder">Inbound Order</SelectItem>
              <SelectItem value="Shipment">Shipment</SelectItem>
              <SelectItem value="InventoryMovement">Inventory Movement</SelectItem>
              <SelectItem value="WebhookDelivery">Webhook Delivery</SelectItem>
              <SelectItem value="Alert">Alert</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="w-44">
          <Select value={severity} onValueChange={(v) => { setSeverity(v); setPage(1); }}>
            <SelectTrigger id="timeline-severity-filter" className="bg-[#0f0f11]/60 border-zinc-800">
              <SelectValue placeholder="Mức độ" />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-zinc-800 text-white">
              <SelectItem value="all">Tất cả mức độ</SelectItem>
              <SelectItem value="info">Thông tin (info)</SelectItem>
              <SelectItem value="warning">Cảnh báo (warning)</SelectItem>
              <SelectItem value="critical">Nghiêm trọng (critical)</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Input
          id="timeline-trace-filter"
          placeholder="Filter by Trace ID..."
          value={traceId}
          onChange={(e) => { setTraceId(e.target.value); setPage(1); }}
          className="w-64 bg-[#0f0f11]/60 border-zinc-800 placeholder-zinc-600 rounded-lg text-white"
        />

        <Button variant="outline" size="sm" onClick={handleRefresh} className="rounded-lg border-zinc-800">
          Refresh
        </Button>
      </div>

      {/* Main List */}
      <Card className="border-zinc-800/80 bg-[#0f0f11]/40 rounded-xl">
        <CardContent className="pt-6">
          {loading ? (
            <p className="text-sm text-zinc-500 py-8 text-center animate-pulse">Đang tải dòng thời gian...</p>
          ) : items.length === 0 ? (
            <p className="text-sm text-zinc-500 py-8 text-center italic">Không tìm thấy hoạt động nào.</p>
          ) : (
            <div className="space-y-4">
              {items.map((item) => (
                <div key={item.id} className="p-4 border border-zinc-800/60 bg-zinc-950/10 hover:bg-zinc-950/20 transition-all duration-200 rounded-xl flex items-start gap-4">
                  <div className="p-2 bg-zinc-900 rounded-lg border border-zinc-800 text-zinc-400 mt-1">
                    <Activity className="h-4 w-4" />
                  </div>
                  <div className="flex-1 space-y-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <span className="text-zinc-200 font-semibold text-sm">{item.title}</span>
                        <Badge variant="outline" className={getSeverityColor(item.severity)}>
                          {item.severity}
                        </Badge>
                      </div>
                      <span className="text-xs text-zinc-500">
                        {new Date(item.createdAt).toLocaleString("vi-VN")}
                      </span>
                    </div>

                    {item.description && (
                      <p className="text-xs text-zinc-400 leading-relaxed bg-zinc-950/10 p-2 border border-zinc-850/40 rounded-lg">
                        {item.description}
                      </p>
                    )}

                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-[10px] text-zinc-500">
                      <span className="font-mono bg-zinc-900 px-1.5 py-0.5 rounded text-zinc-400 border border-zinc-800">
                        {item.entityType}
                      </span>
                      <span>
                        Entity ID: <span className="font-mono text-zinc-400">{item.entityId}</span>
                      </span>
                      {item.actorName && (
                        <span>
                          Người thực hiện: <span className="text-zinc-400">{item.actorName}</span>
                        </span>
                      )}
                      <span
                        onClick={() => setSelectedTraceId(item.traceId)}
                        className="cursor-pointer hover:underline text-emerald-400 flex items-center gap-1 font-mono"
                      >
                        Trace ID: {item.traceId} <Eye className="h-3 w-3" />
                      </span>
                    </div>

                    {/* Metadata Toggle */}
                    {item.metadataJson && (
                      <div className="space-y-1.5 pt-1">
                        <Button
                          variant="ghost"
                          size="xs"
                          onClick={() => toggleMetadata(item.id)}
                          className="h-6 text-zinc-500 hover:text-zinc-400 p-0 text-[10px] flex items-center gap-1 hover:bg-transparent"
                        >
                          {expandedItems[item.id] ? (
                            <>
                              Hide Details <ChevronUp className="h-3 w-3" />
                            </>
                          ) : (
                            <>
                              Show Details <ChevronDown className="h-3 w-3" />
                            </>
                          )}
                        </Button>
                        {expandedItems[item.id] && (
                          <pre className="text-[10px] bg-zinc-950/60 p-3 rounded-lg border border-zinc-850/80 text-zinc-400 overflow-x-auto max-h-48 font-mono leading-relaxed">
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

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex justify-between items-center mt-6 text-sm text-zinc-400 pt-4 border-t border-zinc-900">
                  <span>Trang {page}/{totalPages} — Tổng {total} bản ghi</span>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={page <= 1}
                      onClick={() => setPage(p => p - 1)}
                      className="rounded-lg border-zinc-800"
                    >
                      Trước
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={page >= totalPages}
                      onClick={() => setPage(p => p + 1)}
                      className="rounded-lg border-zinc-800"
                    >
                      Tiếp
                    </Button>
                  </div>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Trace ID Lookup Dialog */}
      <Dialog open={selectedTraceId !== null} onOpenChange={(open) => !open && setSelectedTraceId(null)}>
        <DialogContent className="max-w-4xl bg-[#0f0f11] border-zinc-800 text-white rounded-xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="text-lg font-bold flex items-center gap-2">
              <GitFork className="h-5 w-5 text-emerald-400" /> Liên kết hoạt động của Trace ID
            </DialogTitle>
          </DialogHeader>
          {traceLoading ? (
            <p className="text-sm text-zinc-500 py-12 text-center animate-pulse">Đang truy vấn liên kết trace logs...</p>
          ) : traceDetail ? (
            <div className="space-y-6 py-2">
              <div className="p-3 bg-zinc-950/40 border border-zinc-800 rounded-lg text-xs font-mono text-zinc-400 flex flex-col gap-1">
                <span>Trace ID: <span className="text-emerald-400">{traceDetail.traceId}</span></span>
              </div>

              {/* Trace Logs List */}
              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-zinc-200">1. Logs kỹ thuật hệ thống</h3>
                {traceDetail.traceLogs.length === 0 ? (
                  <p className="text-xs text-zinc-500 italic p-3 bg-zinc-950/10 border border-zinc-850 rounded-lg">Không tìm thấy trace log kỹ thuật nào.</p>
                ) : (
                  <div className="max-h-60 overflow-y-auto border border-zinc-850 bg-zinc-950/10 rounded-lg divide-y divide-zinc-900">
                    <Table>
                      <TableHeader className="bg-zinc-900/30">
                        <TableRow className="hover:bg-transparent border-zinc-850">
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Thời gian</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Level</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Span / Source</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Nội dung log</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {traceDetail.traceLogs.map((l: TraceLog) => (
                          <TableRow key={l.id} className="hover:bg-zinc-900/20 border-zinc-900">
                            <TableCell className="text-[10px] text-zinc-500 py-1.5 font-mono">
                              {new Date(l.createdAt).toLocaleTimeString("vi-VN")}
                            </TableCell>
                            <TableCell className="py-1.5">
                              <Badge variant={l.level === "error" ? "destructive" : l.level === "warning" ? "secondary" : "outline"} className="text-[9px] px-1 py-0 h-4">
                                {l.level}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-[10px] text-zinc-400 py-1.5 font-mono">
                              {l.spanName} ({l.source})
                            </TableCell>
                            <TableCell className="text-xs text-zinc-300 py-1.5 leading-relaxed">
                              {l.message}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>

              {/* Activity Timeline list */}
              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-zinc-200">2. Sự kiện dòng thời gian nghiệp vụ</h3>
                {traceDetail.timelineEntries.length === 0 ? (
                  <p className="text-xs text-zinc-500 italic p-3 bg-zinc-950/10 border border-zinc-850 rounded-lg">Không tìm thấy sự kiện dòng thời gian nào.</p>
                ) : (
                  <div className="space-y-2 max-h-60 overflow-y-auto border border-zinc-850 bg-zinc-950/10 p-3 rounded-lg">
                    {traceDetail.timelineEntries.map((e) => (
                      <div key={e.id} className="relative pl-5 border-l border-zinc-800 pb-2 text-xs">
                        <div className="absolute left-[-3.5px] top-1.5 h-1.5 w-1.5 rounded-full bg-emerald-400" />
                        <div className="flex justify-between items-center gap-2">
                          <span className="font-semibold text-zinc-300">{e.title}</span>
                          <span className="text-[10px] text-zinc-500">{new Date(e.createdAt).toLocaleString("vi-VN")}</span>
                        </div>
                        {e.description && <p className="text-zinc-400 mt-0.5 leading-relaxed">{e.description}</p>}
                        <div className="text-[9px] text-zinc-500 mt-0.5">
                          EntityType: {e.entityType} | EntityID: {e.entityId}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Webhook Deliveries list */}
              <div className="space-y-2">
                <h3 className="text-sm font-semibold text-zinc-200">3. Giao dịch tích hợp gửi Webhook</h3>
                {traceDetail.webhookDeliveries.length === 0 ? (
                  <p className="text-xs text-zinc-500 italic p-3 bg-zinc-950/10 border border-zinc-850 rounded-lg">Không tìm thấy giao dịch webhook nào.</p>
                ) : (
                  <div className="max-h-60 overflow-y-auto border border-zinc-850 bg-zinc-950/10 rounded-lg">
                    <Table>
                      <TableHeader className="bg-zinc-900/30">
                        <TableRow className="hover:bg-transparent border-zinc-850">
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Event Type</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Trạng thái</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Retry</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">HTTP Code</TableHead>
                          <TableHead className="text-zinc-400 text-xs py-2 h-8">Lỗi chi tiết</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {traceDetail.webhookDeliveries.map((w: WebhookDelivery) => (
                          <TableRow key={w.id} className="hover:bg-zinc-900/20 border-zinc-900">
                            <TableCell className="text-[10px] text-zinc-400 py-1.5 font-mono">{w.eventType}</TableCell>
                            <TableCell className="py-1.5">
                              <Badge variant={w.status === "delivered" ? "default" : w.status === "deadLetter" ? "destructive" : "secondary"} className="text-[9px] px-1 py-0 h-4">
                                {w.status}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-xs text-zinc-300 py-1.5">{w.retryCount}</TableCell>
                            <TableCell className="text-xs text-zinc-300 py-1.5">{w.lastResponseCode ?? "—"}</TableCell>
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
  );
}
