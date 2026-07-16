"use client";

import { useCallback, useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { QcResultDialog } from "@/features/qc/components/qc-result-dialog";
import { HoldReleaseDialog } from "@/features/qc/components/hold-release-dialog";
import { 
  CheckSquare, Search, Unlock, Lock, Ban, 
  RefreshCw, ClipboardCheck, AlertOctagon 
} from "lucide-react";

interface QcQueueItem {
  id: string;
  lotId: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  expectedQty: number;
  receivedQty: number;
  createdAt: string;
}

interface LotDetails {
  id: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  expiryDate: string;
  productionDate: string;
  qcStatus: string;
}

export default function QcPage() {
  const [queue, setQueue] = useState<QcQueueItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  // Lot Lookup State
  const [lookupLotNo, setLookupLotNo] = useState("");
  const [lookupResult, setLookupResult] = useState<LotDetails[] | null>(null);
  const [lookupLoading, setLookupLoading] = useState(false);

  // Dialog State
  const [activeLot, setActiveLot] = useState<{ id: string; lotNo: string; qcRequestId?: string } | null>(null);
  const [dialogMode, setDialogMode] = useState<"result" | "hold" | "release" | "reject" | null>(null);

  const fetchQueue = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<QcQueueItem[]>("/qc/queue");
      setQueue(res.data);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải danh sách hàng chờ QC."));
    } finally {
      setLoading(false);
    }
  }, []);

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lookupLotNo.trim()) return;

    setLookupLoading(true);
    setLookupResult(null);
    try {
      const res = await api.get<LotDetails[]>(`/lots/${lookupLotNo.trim()}`);
      setLookupResult(res.data);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không tìm thấy lô hàng."));
    } finally {
      setLookupLoading(false);
    }
  };

  useEffect(() => {
    queueMicrotask(() => void fetchQueue());
  }, [fetchQueue]);

  const openQcDialog = (item: QcQueueItem) => {
    setActiveLot({ id: item.lotId, lotNo: item.lotNo, qcRequestId: item.id });
    setDialogMode("result");
  };

  const openActionDialog = (lotId: string, lotNo: string, mode: "hold" | "release" | "reject") => {
    setActiveLot({ id: lotId, lotNo });
    setDialogMode(mode);
  };

  const handleSuccess = () => {
    fetchQueue();
    if (lookupLotNo) {
      // Re-lookup to update details
      api.get<LotDetails[]>(`/lots/${lookupLotNo.trim()}`).then((res) => {
        setLookupResult(res.data);
      }).catch(() => {});
    }
  };

  const getQcStatusBadge = (status: string) => {
    switch (status.toUpperCase()) {
      case "RELEASE":
        return <Badge className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">Giải phóng (Pass)</Badge>;
      case "HOLD":
        return <Badge className="bg-amber-500/10 text-amber-500 border-amber-500/20">Đang giữ (Hold)</Badge>;
      case "REJECT":
        return <Badge className="bg-rose-500/10 text-rose-500 border-rose-500/20">Từ chối (Reject)</Badge>;
      case "UNSPEC":
        return <Badge className="bg-zinc-500/10 text-zinc-400 border-zinc-500/20">Chưa kiểm tra</Badge>;
      default:
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">{status}</Badge>;
    }
  };

  const filteredQueue = queue.filter(item => 
    item.lotNo.toLowerCase().includes(searchQuery.toLowerCase()) ||
    item.itemName.toLowerCase().includes(searchQuery.toLowerCase()) ||
    item.itemCode.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-3">
          <CheckSquare className="h-6 w-6 text-emerald-500" />
          Kiểm định chất lượng
        </h1>
        <p className="text-xs text-zinc-400 mt-1">Quản lý kiểm định lô hàng nhận kho, chuyển trạng thái giải phóng, giữ hoặc từ chối hàng lỗi.</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* QC Queue List */}
        <div className="lg:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 border-b border-zinc-800">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <ClipboardCheck className="h-4 w-4 text-emerald-500" />
                Hàng chờ kiểm định QC ({filteredQueue.length})
              </CardTitle>
              <Button variant="ghost" size="icon" onClick={fetchQueue} className="h-8 w-8 text-zinc-400 hover:text-white">
                <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
              </Button>
            </CardHeader>
            <CardContent className="pt-4">
              <div className="flex mb-4">
                <div className="relative flex-1">
                  <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-zinc-500" />
                  <Input
                    placeholder="Tìm theo mã lô hoặc tên vật tư..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="bg-zinc-800 border-zinc-700 text-white pl-9 h-9 text-xs"
                  />
                </div>
              </div>

              {loading && queue.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">Đang tải dữ liệu...</div>
              ) : filteredQueue.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">Không có lô hàng nào chờ kiểm định.</div>
              ) : (
                <div className="overflow-x-auto">
                  <Table className="text-xs">
                    <TableHeader className="border-b border-zinc-800">
                      <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                        <TableHead className="text-zinc-400">Số lô (Lot No)</TableHead>
                        <TableHead className="text-zinc-400">Vật tư</TableHead>
                        <TableHead className="text-zinc-400 text-right">SL dự kiến</TableHead>
                        <TableHead className="text-zinc-400 text-right">SL nhận thực tế</TableHead>
                        <TableHead className="text-zinc-400">Ngày tạo y/c</TableHead>
                        <TableHead className="text-zinc-400 text-center">Thao tác</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filteredQueue.map((item) => (
                        <TableRow key={item.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                          <TableCell className="font-semibold text-zinc-200">{item.lotNo}</TableCell>
                          <TableCell>
                            <div className="font-medium text-zinc-300">{item.itemName}</div>
                            <div className="text-[10px] text-zinc-500 font-mono">{item.itemCode}</div>
                          </TableCell>
                          <TableCell className="text-right text-zinc-300">{item.expectedQty.toLocaleString()}</TableCell>
                          <TableCell className="text-right text-zinc-200 font-medium">{item.receivedQty.toLocaleString()}</TableCell>
                          <TableCell className="text-zinc-400">{new Date(item.createdAt).toLocaleString("vi-VN")}</TableCell>
                          <TableCell className="text-center">
                            <Button
                              onClick={() => openQcDialog(item)}
                              className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-3 text-[11px] rounded"
                            >
                              Kiểm định
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

        {/* Hold/Release Lookup Controller Panel */}
        <div className="flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-2">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <AlertOctagon className="h-4 w-4 text-amber-500" />
                Quản lý Hold/Release nhanh
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4 flex flex-col gap-4">
              <form onSubmit={handleLookup} className="flex gap-2">
                <Input
                  placeholder="Nhập số lô cần xử lý..."
                  value={lookupLotNo}
                  onChange={(e) => setLookupLotNo(e.target.value)}
                  className="bg-zinc-800 border-zinc-700 text-white h-9 text-xs flex-1"
                />
                <Button type="submit" disabled={lookupLoading} className="bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 text-white h-9 px-3 text-xs">
                  <Search className="h-4 w-4" />
                </Button>
              </form>

              {lookupLoading && (
                <div className="text-center py-4 text-zinc-500 text-xs">Đang tìm kiếm thông tin...</div>
              )}

              {lookupResult && lookupResult.map((lot, idx) => (
                <div key={idx} className="bg-zinc-800/50 p-4 rounded-lg border border-zinc-800 flex flex-col gap-3 text-xs">
                  <div className="flex justify-between items-start border-b border-zinc-800 pb-2">
                    <div>
                      <div className="font-semibold text-zinc-200 text-sm">{lot.lotNo}</div>
                      <span className="text-[10px] text-zinc-500 font-mono">ID: {lot.id}</span>
                    </div>
                    {getQcStatusBadge(lot.qcStatus)}
                  </div>

                  <div className="grid grid-cols-2 gap-y-2 text-[11px]">
                    <span className="text-zinc-500">Vật tư:</span>
                    <span className="text-zinc-300 text-right truncate">{lot.itemName} ({lot.itemCode})</span>
                    <span className="text-zinc-500">Hạn sử dụng:</span>
                    <span className="text-zinc-300 text-right">{lot.expiryDate ? new Date(lot.expiryDate).toLocaleDateString("vi-VN") : "N/A"}</span>
                  </div>

                  <div className="flex gap-2 mt-2 border-t border-zinc-800 pt-3">
                    <Button
                      onClick={() => openActionDialog(lot.id, lot.lotNo, "hold")}
                      disabled={lot.qcStatus.toUpperCase() === "HOLD"}
                      className="bg-amber-600/10 text-amber-500 border border-amber-600/20 hover:bg-amber-600 hover:text-white h-8 px-2 flex-1 text-[11px] gap-1"
                    >
                      <Lock className="h-3.5 w-3.5" />
                      Hold
                    </Button>
                    <Button
                      onClick={() => openActionDialog(lot.id, lot.lotNo, "release")}
                      disabled={lot.qcStatus.toUpperCase() === "RELEASE"}
                      className="bg-emerald-600/10 text-emerald-500 border border-emerald-600/20 hover:bg-emerald-600 hover:text-white h-8 px-2 flex-1 text-[11px] gap-1"
                    >
                      <Unlock className="h-3.5 w-3.5" />
                      Release
                    </Button>
                    <Button
                      onClick={() => openActionDialog(lot.id, lot.lotNo, "reject")}
                      disabled={lot.qcStatus.toUpperCase() === "REJECT"}
                      className="bg-rose-600/10 text-rose-500 border border-rose-600/20 hover:bg-rose-600 hover:text-white h-8 px-2 flex-1 text-[11px] gap-1"
                    >
                      <Ban className="h-3.5 w-3.5" />
                      Reject
                    </Button>
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* QC Result Dialog */}
      {dialogMode === "result" && activeLot && (
        <QcResultDialog
          isOpen={true}
          onClose={() => {
            setActiveLot(null);
            setDialogMode(null);
          }}
          lotId={activeLot.id}
          lotNo={activeLot.lotNo}
          qcRequestId={activeLot.qcRequestId || ""}
          onSuccess={handleSuccess}
        />
      )}

      {/* Hold/Release Action Dialogs */}
      {(dialogMode === "hold" || dialogMode === "release" || dialogMode === "reject") && activeLot && (
        <HoldReleaseDialog
          isOpen={true}
          onClose={() => {
            setActiveLot(null);
            setDialogMode(null);
          }}
          lotId={activeLot.id}
          lotNo={activeLot.lotNo}
          mode={dialogMode}
          onSuccess={handleSuccess}
        />
      )}
    </div>
  );
}
