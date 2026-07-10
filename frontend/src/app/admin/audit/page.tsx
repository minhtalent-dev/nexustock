"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { showError } from "@/lib/toast";
import { FileText, Eye, Filter, ArrowLeft, ArrowRight, Search } from "lucide-react";
import { format } from "date-fns";

interface AuditLogDto {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  oldValue: string;
  newValue: string;
  userId: string;
  userName: string;
  timestamp: string;
  traceId: string;
}

interface AuditLogResponse {
  total: number;
  page: number;
  pageSize: number;
  items: AuditLogDto[];
}

export default function AuditPage() {
  const [data, setData] = useState<AuditLogResponse | null>(null);
  const [loading, setLoading] = useState(false);

  // Filters
  const [entityName, setEntityName] = useState("");
  const [action, setAction] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);

  // Active filter params
  const [activeParams, setActiveParams] = useState({
    entityName: "",
    action: "",
    from: "",
    to: "",
  });

  // Dialog detail
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);
  const [isOpen, setIsOpen] = useState(false);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    try {
      const params: any = {
        page,
        pageSize,
      };
      if (activeParams.entityName) params.entityName = activeParams.entityName;
      if (activeParams.action) params.action = activeParams.action;
      if (activeParams.from) params.from = new Date(activeParams.from).toISOString();
      if (activeParams.to) params.to = new Date(activeParams.to).toISOString();

      const res = await api.get<AuditLogResponse>("/audit-logs", { params });
      setData(res.data);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải nhật ký hệ thống.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, activeParams]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const handleApplyFilter = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    setActiveParams({
      entityName,
      action,
      from,
      to,
    });
  };

  const handleResetFilter = () => {
    setEntityName("");
    setAction("");
    setFrom("");
    setTo("");
    setPage(1);
    setActiveParams({
      entityName: "",
      action: "",
      from: "",
      to: "",
    });
  };

  const openDetail = (log: AuditLogDto) => {
    setSelectedLog(log);
    setIsOpen(true);
  };

  const formattedDate = (dateStr: string) => {
    try {
      return format(new Date(dateStr), "dd/MM/yyyy HH:mm:ss");
    } catch {
      return dateStr;
    }
  };

  const totalPages = useMemo(() => {
    if (!data) return 0;
    return Math.ceil(data.total / pageSize);
  }, [data, pageSize]);

  // Format JSON to show beautifully
  const renderJson = (jsonStr: string) => {
    if (!jsonStr || jsonStr === "null") return <span className="text-zinc-600 font-mono text-xs">Empty</span>;
    try {
      const parsed = JSON.parse(jsonStr);
      return (
        <pre className="text-[11px] font-mono text-zinc-300 bg-zinc-950 p-3 rounded-lg border border-zinc-850 overflow-x-auto max-h-60 leading-relaxed">
          {JSON.stringify(parsed, null, 2)}
        </pre>
      );
    } catch {
      return (
        <pre className="text-[11px] font-mono text-zinc-300 bg-zinc-950 p-3 rounded-lg border border-zinc-850 overflow-x-auto max-h-60 leading-relaxed">
          {jsonStr}
        </pre>
      );
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-3">
          <FileText className="h-6 w-6 text-emerald-500" />
          Nhật ký hệ thống (Audit Logs)
        </h1>
        <p className="text-xs text-zinc-400 mt-1">
          Tra cứu chi tiết mọi thao tác thay đổi dữ liệu, truy vết Trace ID và giá trị cũ/mới của các thực thể.
        </p>
      </div>

      {/* Filter panel */}
      <Card className="bg-[#111] border-zinc-800/80">
        <CardHeader className="py-4 border-b border-zinc-800/60">
          <CardTitle className="text-sm font-semibold text-white flex items-center gap-2">
            <Filter className="h-4 w-4 text-emerald-500" />
            Bộ lọc tra cứu
          </CardTitle>
        </CardHeader>
        <CardContent className="p-4">
          <form onSubmit={handleApplyFilter} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="entity" className="text-xs text-zinc-400">Tên bảng / Đối tượng</Label>
              <Input
                id="entity"
                value={entityName}
                onChange={(e) => setEntityName(e.target.value)}
                placeholder="Ví dụ: Product, Uom..."
                className="bg-zinc-900 border-zinc-800 text-sm h-9"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="act" className="text-xs text-zinc-400">Thao tác</Label>
              <Input
                id="act"
                value={action}
                onChange={(e) => setAction(e.target.value)}
                placeholder="Added, Modified, Deleted"
                className="bg-zinc-900 border-zinc-800 text-sm h-9"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="from" className="text-xs text-zinc-400">Từ ngày</Label>
              <Input
                id="from"
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
                className="bg-zinc-900 border-zinc-800 text-sm h-9"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="to" className="text-xs text-zinc-400">Đến ngày</Label>
              <Input
                id="to"
                type="date"
                value={to}
                onChange={(e) => setTo(e.target.value)}
                className="bg-zinc-900 border-zinc-800 text-sm h-9"
              />
            </div>

            <div className="md:col-span-4 flex items-center justify-end gap-2 mt-2">
              <Button type="button" onClick={handleResetFilter} variant="ghost" className="text-zinc-400 hover:text-zinc-200 h-9 text-xs">
                Mặc định
              </Button>
              <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500 text-white h-9 text-xs gap-1.5 px-4">
                <Search className="h-3.5 w-3.5" />
                Lọc dữ liệu
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {/* List Table */}
      <Card className="bg-[#111] border-zinc-800/80">
        <CardHeader className="py-4 border-b border-zinc-800/60 flex flex-row items-center justify-between">
          <div>
            <CardTitle className="text-sm font-semibold text-white">Lịch sử thay đổi</CardTitle>
            <CardDescription className="text-[10px] text-zinc-550">
              Tổng số: <span className="font-mono text-zinc-400">{data?.total || 0}</span> bản ghi
            </CardDescription>
          </div>
          {loading && <div className="h-4 w-4 animate-spin rounded-full border-2 border-emerald-500 border-t-transparent" />}
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader className="bg-zinc-900/30 border-b border-zinc-800/60">
              <TableRow className="hover:bg-transparent">
                <TableHead className="text-zinc-400 font-semibold h-11 pl-6">Thời gian</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Người sửa</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Đối tượng</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Hành động</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Trace ID</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11 text-right w-24 pr-6">Chi tiết</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data === null || data.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-10 text-zinc-550 text-sm">
                    {loading ? "Đang tải dữ liệu..." : "Không tìm thấy dữ liệu audit log."}
                  </TableCell>
                </TableRow>
              ) : (
                data.items.map((log) => (
                  <TableRow key={log.id} className="border-b border-zinc-800/30 hover:bg-zinc-900/10">
                    <TableCell className="font-mono text-sm text-zinc-300 h-12 pl-6">
                      {formattedDate(log.timestamp)}
                    </TableCell>
                    <TableCell className="text-white font-medium h-12">{log.userName || "SYSTEM"}</TableCell>
                    <TableCell className="h-12">
                      <span className="font-semibold text-zinc-200">{log.entityName}</span>
                      <span className="text-[10px] text-zinc-550 font-mono block mt-0.5">{log.entityId}</span>
                    </TableCell>
                    <TableCell className="h-12">
                      <span
                        className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold ${
                          log.action === "Added"
                            ? "bg-green-500/10 text-green-400"
                            : log.action === "Modified"
                            ? "bg-blue-500/10 text-blue-400"
                            : "bg-red-500/10 text-red-400"
                        }`}
                      >
                        {log.action}
                      </span>
                    </TableCell>
                    <TableCell className="font-mono text-xs text-zinc-500 truncate max-w-[120px] h-12" title={log.traceId}>
                      {log.traceId || "—"}
                    </TableCell>
                    <TableCell className="text-right h-12 pr-6">
                      <Button onClick={() => openDetail(log)} variant="ghost" size="sm" className="text-emerald-400 hover:text-emerald-300 hover:bg-emerald-500/10 h-8 w-8 p-0 rounded-md">
                        <Eye className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between px-6 py-4 border-t border-zinc-800/60">
              <span className="text-xs text-zinc-550">
                Trang <span className="font-mono text-zinc-400">{page}</span> / <span className="font-mono text-zinc-400">{totalPages}</span>
              </span>
              <div className="flex items-center gap-2">
                <Button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  variant="outline"
                  size="sm"
                  className="border-zinc-800 hover:bg-zinc-900 text-zinc-300 h-8 w-8 p-0 rounded-md"
                >
                  <ArrowLeft className="h-4 w-4" />
                </Button>
                <Button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  variant="outline"
                  size="sm"
                  className="border-zinc-800 hover:bg-zinc-900 text-zinc-300 h-8 w-8 p-0 rounded-md"
                >
                  <ArrowRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Dialog xem chi tiết */}
      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="bg-[#111] border border-zinc-800 text-zinc-100 max-w-2xl font-sans">
          <DialogHeader className="border-b border-zinc-850 pb-3">
            <DialogTitle className="text-sm font-semibold text-white">
              Chi tiết thay đổi thực thể
            </DialogTitle>
          </DialogHeader>

          {selectedLog && (
            <div className="flex flex-col gap-4 py-3">
              <div className="grid grid-cols-2 gap-4 text-xs">
                <div className="flex flex-col gap-1">
                  <span className="text-zinc-500">Đối tượng (Bảng)</span>
                  <span className="font-semibold text-white">{selectedLog.entityName}</span>
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-zinc-500">Hành động</span>
                  <span className="font-mono font-bold text-emerald-400">{selectedLog.action}</span>
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-zinc-500">Người thực hiện</span>
                  <span className="font-semibold text-zinc-200">{selectedLog.userName}</span>
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-zinc-500">Thời gian</span>
                  <span className="font-mono text-zinc-350">{formattedDate(selectedLog.timestamp)}</span>
                </div>
                <div className="flex flex-col gap-1 col-span-2">
                  <span className="text-zinc-500">Trace ID</span>
                  <span className="font-mono text-zinc-400 break-all">{selectedLog.traceId || "—"}</span>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-2">
                <div className="flex flex-col gap-2">
                  <span className="text-xs font-semibold text-zinc-450 uppercase tracking-wider">Giá trị cũ (Before)</span>
                  {renderJson(selectedLog.oldValue)}
                </div>
                <div className="flex flex-col gap-2">
                  <span className="text-xs font-semibold text-zinc-450 uppercase tracking-wider">Giá trị mới (After)</span>
                  {renderJson(selectedLog.newValue)}
                </div>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
