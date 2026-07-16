"use client";

import { useCallback, useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
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
  const [exceptions, setExceptions] = useState<ExceptionDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page] = useState(1);
  const [pageSize] = useState(20);

  // Filters
  const [severityFilter, setSeverityFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");

  // Detail Modal State
  const [selectedException, setSelectedException] = useState<ExceptionDto | null>(null);
  const [events, setEvents] = useState<ExceptionEventDto[]>([]);
  const [loadingEvents, setLoadingEvents] = useState(false);

  // Assign Dialog State
  const [isAssignOpen, setIsAssignOpen] = useState(false);
  const [owner, setOwner] = useState("");
  const [slaHours, setSlaHours] = useState(4);
  const [assigning, setAssigning] = useState(false);

  // Resolve Dialog State
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
      showError(getHttpErrorMessage(err, "Không thể tải danh sách ngoại lệ."));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, severityFilter, typeFilter]);

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
      showError("Không thể tải lịch sử sự kiện ngoại lệ.");
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
      showSuccess("Gán người xử lý thành công.");
      setIsAssignOpen(false);
      // Refresh details
      viewDetails(selectedException);
      fetchExceptions();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi gán người xử lý."));
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
      showSuccess("Giải quyết ngoại lệ thành công.");
      setIsResolveOpen(false);
      setSelectedException(null); // Close detail
      fetchExceptions();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi giải quyết ngoại lệ."));
    } finally {
      setResolving(false);
    }
  };

  const getSeverityBadge = (sev: string) => {
    switch (sev) {
      case "CRITICAL":
        return <Badge variant="destructive">Khẩn cấp</Badge>;
      case "HIGH":
        return <Badge className="bg-orange-500 hover:bg-orange-600 text-white">Cao</Badge>;
      case "MEDIUM":
        return <Badge className="bg-yellow-500 hover:bg-yellow-600 text-white">Trung bình</Badge>;
      default:
        return <Badge variant="secondary">Thấp</Badge>;
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Resolved":
        return <Badge className="bg-green-600 text-white">Đã xử lý</Badge>;
      case "In_Progress":
        return <Badge className="bg-blue-600 text-white">Đang xử lý</Badge>;
      case "Cancelled":
        return <Badge variant="outline">Đã hủy</Badge>;
      default:
        return <Badge variant="secondary">Chờ xử lý</Badge>;
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Sự cố vận hành</h1>
          <p className="text-muted-foreground text-sm">Theo dõi và khắc phục các ngoại lệ phát sinh tại kho hàng</p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Phiếu chờ xử lý</CardTitle>
            <AlertCircle className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalCount}</div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-4 bg-card p-4 rounded-lg border">
        <div className="space-y-1 w-48">
          <Label className="text-xs">Mức độ khẩn cấp</Label>
          <select
            className="w-full bg-background border rounded px-2 py-1 text-sm h-9"
            value={severityFilter}
            onChange={(e) => setSeverityFilter(e.target.value)}
          >
            <option value="">Tất cả</option>
            <option value="CRITICAL">Khẩn cấp</option>
            <option value="HIGH">Cao</option>
            <option value="MEDIUM">Trung bình</option>
            <option value="LOW">Thấp</option>
          </select>
        </div>
        <div className="space-y-1 w-48">
          <Label className="text-xs">Loại sự cố</Label>
          <Input
            placeholder="Lọc loại (VD: SHORTAGE)"
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="h-9"
          />
        </div>
        <Button variant="secondary" onClick={() => { setSeverityFilter(""); setTypeFilter(""); }} className="mt-5 h-9">
          Làm mới bộ lọc
        </Button>
      </div>

      {/* Main Grid */}
      <div className="grid gap-6 md:grid-cols-3">
        {/* Table List */}
        <div className="md:col-span-2 bg-card border rounded-lg overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Mã sự cố</TableHead>
                <TableHead>Loại sự cố</TableHead>
                <TableHead>Khẩn cấp</TableHead>
                <TableHead>Trạng thái</TableHead>
                <TableHead>Thời gian tạo</TableHead>
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
                    Không tìm thấy sự cố nào.
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

        {/* Details Side Panel */}
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
                  <span className="text-muted-foreground">Loại:</span>
                  <span className="font-medium text-right">{selectedException.type}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">Lô hàng:</span>
                  <span className="font-medium text-right">{selectedException.lotNo || "-"}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">Số lượng lệch:</span>
                  <span className="font-medium text-right">{selectedException.qty}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">Vị trí kệ:</span>
                  <span className="font-medium text-right">{selectedException.locationId || "-"}</span>
                </div>
                <div className="grid grid-cols-2">
                  <span className="text-muted-foreground">Mã tham chiếu:</span>
                  <span className="font-medium text-right text-xs truncate max-w-[150px]" title={selectedException.referenceId}>
                    {selectedException.referenceId}
                  </span>
                </div>
                <div className="space-y-1">
                  <span className="text-muted-foreground">Ghi chú sự cố:</span>
                  <p className="bg-muted p-2 rounded text-xs italic">{selectedException.note || "Không có ghi chú"}</p>
                </div>
              </div>

              {/* Actions */}
              {selectedException.status !== "Resolved" && selectedException.status !== "Cancelled" && (
                <div className="flex items-center gap-2 border-b pb-4">
                  <Button variant="outline" size="sm" onClick={() => setIsAssignOpen(true)} className="flex-1 gap-1">
                    <UserCheck className="h-4 w-4" /> Gán việc
                  </Button>
                  <Button size="sm" onClick={() => setIsResolveOpen(true)} className="flex-1 gap-1">
                    <CheckCircle2 className="h-4 w-4" /> Giải quyết
                  </Button>
                </div>
              )}

              {/* Timeline Events */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold flex items-center gap-1.5">
                  <History className="h-4 w-4 text-muted-foreground" /> Lịch sử timeline
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
                            Bởi {e.actor} - {new Date(e.createdAt).toLocaleString("vi-VN")}
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
              <p className="text-sm">Chọn một sự cố trong danh sách để xem chi tiết và xử lý</p>
            </div>
          )}
        </div>
      </div>

      {/* Assign Dialog */}
      <Dialog open={isAssignOpen} onOpenChange={setIsAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Gán xử lý sự cố</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleAssign} className="space-y-4">
            <div className="space-y-1">
              <Label>Người phụ trách</Label>
              <Input
                placeholder="Nhập tên tài khoản hoặc mã nhân viên"
                value={owner}
                onChange={(e) => setOwner(e.target.value)}
                required
              />
            </div>
            <div className="space-y-1">
              <Label>Thời gian SLA (giờ)</Label>
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
                Hủy
              </Button>
              <Button type="submit" disabled={assigning}>
                {assigning && <Loader2 className="h-4 w-4 animate-spin mr-1" />} Xác nhận gán
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Resolve Dialog */}
      <Dialog open={isResolveOpen} onOpenChange={setIsResolveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Giải quyết ngoại lệ</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleResolve} className="space-y-4">
            <div className="space-y-1">
              <Label>Phương án xử lý</Label>
              <select
                className="w-full bg-background border rounded px-2 py-1.5 text-sm h-10"
                value={resolveAction}
                onChange={(e) => setResolveAction(e.target.value)}
              >
                <option value="CORRECTIVE_TRANSACTION">Điều chỉnh số lượng tồn kho (Real-time Sync)</option>
                <option value="CANCEL">Hủy phiếu (Không điều chỉnh tồn kho)</option>
              </select>
            </div>
            <div className="space-y-1">
              <Label>Mã nguyên nhân khắc phục</Label>
              <Input
                placeholder="Ví dụ: SHORTAGE, OVERAGE, LOT_MISMATCH"
                value={resolveReason}
                onChange={(e) => setResolveReason(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>Ghi chú kết quả</Label>
              <textarea
                className="w-full bg-background border rounded p-2 text-sm h-20"
                placeholder="Nhập chi tiết biện pháp khắc phục sự cố"
                value={resolveNote}
                onChange={(e) => setResolveNote(e.target.value)}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setIsResolveOpen(false)}>
                Hủy
              </Button>
              <Button type="submit" disabled={resolving}>
                {resolving && <Loader2 className="h-4 w-4 animate-spin mr-1" />} Xác nhận hoàn thành
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
