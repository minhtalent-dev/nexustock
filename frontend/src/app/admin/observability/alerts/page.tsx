"use client";

import { useEffect, useState } from "react";
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
import { showError } from "@/lib/toast";
import { toast } from "sonner";
import { AlertCircle, CheckCircle2, ShieldAlert, User } from "lucide-react";

export default function AlertCenterPage() {
  const [alerts, setAlerts] = useState<OperationalAlert[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [status, setStatus] = useState<string>("all");
  const [severity, setSeverity] = useState<string>("all");
  const [loading, setLoading] = useState(false);

  // Dialog states
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
      } catch {
        showError("Không thể tải danh sách cảnh báo.");
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [status, severity, page, pageSize, refreshTrigger]);

  const handleActionSubmit = async () => {
    if (!selectedAlert || !actionType) return;
    setActionLoading(true);
    try {
      if (actionType === "ack") {
        await ackAlert(selectedAlert.id, actionNote);
        toast.success("Xác nhận cảnh báo thành công");
      } else {
        await resolveAlert(selectedAlert.id, actionNote);
        toast.success("Giải quyết cảnh báo thành công");
      }
      setSelectedAlert(null);
      setActionType(null);
      setActionNote("");
      setRefreshTrigger(prev => prev + 1);
    } catch {
      showError(actionType === "ack" ? "Xác nhận thất bại." : "Giải quyết thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusBadge = (statusStr: string) => {
    switch (statusStr) {
      case "open":
        return <Badge variant="destructive" className="bg-red-500/10 text-red-400 border border-red-500/20">Mở</Badge>;
      case "acknowledged":
        return <Badge className="bg-amber-500/10 text-amber-400 border border-amber-500/20">Xác nhận</Badge>;
      case "resolved":
        return <Badge className="bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">Đã sửa</Badge>;
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
    <div className="p-6 space-y-4">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-white">Trung tâm cảnh báo</h1>
        <p className="text-zinc-400 text-sm mt-1">Quản lý và giải quyết các cảnh báo vận hành phát sinh trong hệ thống.</p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="w-44">
          <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
            <SelectTrigger id="alert-status-filter" className="bg-[#0f0f11]/60 border-zinc-800">
              <SelectValue placeholder="Trạng thái" />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-zinc-800 text-white">
              <SelectItem value="all">Tất cả trạng thái</SelectItem>
              <SelectItem value="open">Đang mở (open)</SelectItem>
              <SelectItem value="acknowledged">Đã xác nhận</SelectItem>
              <SelectItem value="resolved">Đã giải quyết</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="w-44">
          <Select value={severity} onValueChange={(v) => { setSeverity(v); setPage(1); }}>
            <SelectTrigger id="alert-severity-filter" className="bg-[#0f0f11]/60 border-zinc-800">
              <SelectValue placeholder="Mức độ nghiêm trọng" />
            </SelectTrigger>
            <SelectContent className="bg-[#151518] border-zinc-800 text-white">
              <SelectItem value="all">Tất cả mức độ</SelectItem>
              <SelectItem value="warning">Cảnh báo (warning)</SelectItem>
              <SelectItem value="critical">Nghiêm trọng (critical)</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Button variant="outline" size="sm" onClick={() => setRefreshTrigger(prev => prev + 1)} className="rounded-lg border-zinc-800">
          Refresh
        </Button>
      </div>

      {/* Main Table Card */}
      <Card className="border-zinc-800/80 bg-[#0f0f11]/40 rounded-xl">
        <CardHeader>
          <CardTitle className="text-lg font-semibold text-white">Danh sách cảnh báo ({total})</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-zinc-500 py-8 text-center animate-pulse">Đang tải dữ liệu cảnh báo...</p>
          ) : (
            <>
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader className="border-zinc-800">
                    <TableRow className="border-zinc-800 hover:bg-transparent">
                      <TableHead className="text-zinc-400">Tiêu đề</TableHead>
                      <TableHead className="text-zinc-400">Mức độ</TableHead>
                      <TableHead className="text-zinc-400">Trạng thái</TableHead>
                      <TableHead className="text-zinc-400">Giá trị/Ngưỡng</TableHead>
                      <TableHead className="text-zinc-400">Module nguồn</TableHead>
                      <TableHead className="text-zinc-400">Thời gian tạo</TableHead>
                      <TableHead className="text-right text-zinc-400">Thao tác</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {alerts.length === 0 && (
                      <TableRow className="hover:bg-transparent">
                        <TableCell colSpan={7} className="text-center py-8 text-zinc-500 italic">
                          Không tìm thấy cảnh báo nào phù hợp.
                        </TableCell>
                      </TableRow>
                    )}
                    {alerts.map((a) => (
                      <TableRow
                        key={a.id}
                        onClick={() => setSelectedAlert(a)}
                        className="cursor-pointer border-zinc-800/60 hover:bg-zinc-800/20 transition-colors"
                      >
                        <TableCell className="font-semibold text-zinc-200">{a.title}</TableCell>
                        <TableCell>{getSeverityBadge(a.severity)}</TableCell>
                        <TableCell>{getStatusBadge(a.status)}</TableCell>
                        <TableCell className="font-mono text-zinc-300">
                          {a.metricValue !== undefined ? `${a.metricValue}/${a.thresholdValue ?? "—"}` : "—"}
                        </TableCell>
                        <TableCell className="text-zinc-400">{a.sourceModule}</TableCell>
                        <TableCell className="text-xs text-zinc-500">
                          {new Date(a.createdAt).toLocaleString("vi-VN")}
                        </TableCell>
                        <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                          <div className="flex justify-end gap-1.5">
                            {a.status === "open" && (
                              <Button
                                size="xs"
                                variant="outline"
                                className="rounded-lg border-zinc-800 text-xs text-amber-400 hover:text-amber-300 hover:bg-amber-500/5"
                                onClick={() => { setSelectedAlert(a); setActionType("ack"); }}
                              >
                                Ack
                              </Button>
                            )}
                            {(a.status === "open" || a.status === "acknowledged") && (
                              <Button
                                size="xs"
                                variant="outline"
                                className="rounded-lg border-zinc-800 text-xs text-emerald-400 hover:text-emerald-300 hover:bg-emerald-500/5"
                                onClick={() => { setSelectedAlert(a); setActionType("resolve"); }}
                              >
                                Resolve
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex justify-between items-center mt-6 text-sm text-zinc-400">
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
            </>
          )}
        </CardContent>
      </Card>

      {/* Detail & Action Dialog */}
      <Dialog open={!!selectedAlert && actionType === null} onOpenChange={(open) => !open && setSelectedAlert(null)}>
        <DialogContent className="max-w-2xl bg-[#0f0f11] border-zinc-800 text-white rounded-xl">
          <DialogHeader>
            <DialogTitle className="text-xl font-bold flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-red-500" /> Chi tiết cảnh báo
            </DialogTitle>
          </DialogHeader>
          {selectedAlert && (
            <div className="space-y-4 py-2">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 p-4 bg-zinc-950/40 rounded-lg border border-zinc-800/80">
                <div>
                  <span className="text-zinc-500 text-xs block">Mã loại cảnh báo</span>
                  <span className="font-mono text-sm">{selectedAlert.alertType}</span>
                </div>
                <div>
                  <span className="text-zinc-500 text-xs block">Trạng thái / Mức độ</span>
                  <div className="flex gap-2 mt-1">
                    {getStatusBadge(selectedAlert.status)}
                    {getSeverityBadge(selectedAlert.severity)}
                  </div>
                </div>
                <div>
                  <span className="text-zinc-500 text-xs block">Phân hệ nguồn</span>
                  <span className="text-zinc-200 text-sm font-semibold">{selectedAlert.sourceModule}</span>
                </div>
                {selectedAlert.traceId && (
                  <div>
                    <span className="text-zinc-500 text-xs block">Trace ID</span>
                    <span className="font-mono text-xs text-emerald-400 block break-all">{selectedAlert.traceId}</span>
                  </div>
                )}
              </div>

              <div>
                <Label className="text-zinc-400 text-xs">Thông tin thông báo</Label>
                <p className="text-zinc-200 text-sm mt-1 bg-zinc-950/20 p-3 rounded-lg border border-zinc-850">{selectedAlert.message}</p>
              </div>

              {/* Audit fields */}
              {(selectedAlert.acknowledgedAt || selectedAlert.resolvedAt) && (
                <div className="space-y-2 p-3 bg-zinc-950/10 border border-zinc-800/60 rounded-lg text-xs text-zinc-400">
                  {selectedAlert.acknowledgedAt && (
                    <div className="flex items-center gap-2">
                      <User className="h-3.5 w-3.5 text-zinc-500" />
                      <span>Xác nhận lúc {new Date(selectedAlert.acknowledgedAt).toLocaleString("vi-VN")} bởi {selectedAlert.acknowledgedBy ?? "Hệ thống"}</span>
                    </div>
                  )}
                  {selectedAlert.resolvedAt && (
                    <div className="flex items-center gap-2">
                      <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500" />
                      <span>Giải quyết lúc {new Date(selectedAlert.resolvedAt).toLocaleString("vi-VN")} bởi {selectedAlert.resolvedBy ?? "Hệ thống"}</span>
                    </div>
                  )}
                </div>
              )}

              <DialogFooter className="gap-2">
                {selectedAlert.status === "open" && (
                  <Button
                    variant="outline"
                    className="border-zinc-800 text-amber-400 hover:bg-amber-500/5 rounded-lg"
                    onClick={() => setActionType("ack")}
                  >
                    Xác nhận cảnh báo (Ack)
                  </Button>
                )}
                {(selectedAlert.status === "open" || selectedAlert.status === "acknowledged") && (
                  <Button
                    className="bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg"
                    onClick={() => setActionType("resolve")}
                  >
                    Giải quyết (Resolve)
                  </Button>
                )}
                <Button variant="outline" className="border-zinc-800 text-zinc-300 rounded-lg" onClick={() => setSelectedAlert(null)}>
                  Đóng
                </Button>
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Action Dialog (Ack/Resolve Confirmation) */}
      <Dialog open={actionType !== null} onOpenChange={(open) => !open && setActionType(null)}>
        <DialogContent className="max-w-md bg-[#0f0f11] border-zinc-800 text-white rounded-xl">
          <DialogHeader>
            <DialogTitle className="text-lg font-bold flex items-center gap-2">
              {actionType === "ack" ? (
                <>
                  <ShieldAlert className="h-5 w-5 text-amber-500" /> Xác nhận cảnh báo
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-5 w-5 text-emerald-500" /> Giải quyết cảnh báo
                </>
              )}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <p className="text-sm text-zinc-300">
              {actionType === "ack"
                ? "Ghi chú lại lý do/tiến trình kiểm tra cảnh báo này để các quản trị viên khác nắm thông tin."
                : "Bạn có chắc chắn cảnh báo này đã được khắc phục hoàn toàn?"}
            </p>
            <div className="space-y-1.5">
              <Label htmlFor="action-note" className="text-zinc-400 text-xs">Ghi chú (Note)</Label>
              <Input
                id="action-note"
                placeholder="Nhập ghi chú vận hành..."
                value={actionNote}
                onChange={(e) => setActionNote(e.target.value)}
                className="bg-[#151518] border-zinc-800 text-white rounded-lg placeholder-zinc-600"
              />
            </div>
            <DialogFooter className="gap-2">
              <Button
                variant="outline"
                className="border-zinc-800 text-zinc-300 rounded-lg"
                onClick={() => { setActionType(null); setActionNote(""); }}
                disabled={actionLoading}
              >
                Hủy bỏ
              </Button>
              <Button
                onClick={handleActionSubmit}
                disabled={actionLoading}
                className={actionType === "ack" ? "bg-amber-600 hover:bg-amber-700 text-white rounded-lg" : "bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg"}
              >
                {actionLoading ? "Đang xử lý..." : "Xác nhận"}
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
