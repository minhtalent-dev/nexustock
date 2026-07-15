"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { RefreshCw, Undo2, CheckCircle2, FlaskConical, AlertTriangle, PackageSearch } from "lucide-react";

interface RmaItem {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  qtyExpected: number;
  qtyReceived: number;
  serialNo: string | null;
  reasonCode: string;
}

interface RmaRequest {
  id: string;
  rmaNo: string;
  customerId: string;
  customerName: string;
  referenceNo: string | null;
  status: string;
  createdAt: string;
  createdBy: string;
  items: RmaItem[];
}

export default function RmaPage() {
  const [rmas, setRmas] = useState<RmaRequest[]>([]);
  const [selectedRma, setSelectedRma] = useState<RmaRequest | null>(null);
  const [loading, setLoading] = useState(false);
  const [processing, setProcessing] = useState(false);

  const fetchRmas = async () => {
    setLoading(true);
    try {
      const res = await api.get<RmaRequest[]>("/rma");
      setRmas(res.data || []);
    } catch (err: any) {
      showError("Không thể tải danh sách RMA.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRmas();
  }, []);

  const handleProcessQc = async (rmaId: string, item: RmaItem, disposition: string) => {
    setProcessing(true);
    try {
      const payload = {
        results: [
          {
            rmaItemId: item.id,
            qcStatus: "PASS",
            disposition: disposition,
            qty: item.qtyReceived,
            notes: "Xử lý nhanh qua Dashboard Admin"
          }
        ]
      };
      await api.post(`/rma/${rmaId}/qc`, payload);
      showSuccess(`Đã xử lý QC: ${disposition}`);
      fetchRmas();
      if (selectedRma?.id === rmaId) {
         // Refresh detail view
         const updated = await api.get<RmaRequest>(`/rma/${rmaId}`);
         setSelectedRma(updated.data);
      }
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi khi xử lý QC.");
    } finally {
      setProcessing(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "OPEN": return <Badge className="bg-blue-500 hover:bg-blue-600">Mới tạo</Badge>;
      case "RECEIVED": return <Badge className="bg-amber-500 hover:bg-amber-600">Đã nhận hàng</Badge>;
      case "QC_COMPLETED": return <Badge className="bg-emerald-500 hover:bg-emerald-600">Hoàn tất QC</Badge>;
      case "CLOSED": return <Badge className="bg-zinc-500 hover:bg-zinc-600">Đã đóng</Badge>;
      default: return <Badge variant="outline">{status}</Badge>;
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Undo2 className="h-6 w-6 text-orange-500" />
            Quản lý Trả hàng (RMA Management)
          </h1>
          <p className="text-xs text-zinc-400 mt-1">
            Theo dõi, tiếp nhận và kiểm định hàng hóa khách hàng trả lại.
          </p>
        </div>
        <Button
          onClick={fetchRmas}
          variant="outline"
          className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 px-4 flex items-center gap-2"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
          Làm mới
        </Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardContent className="p-0">
              {loading && rmas.length === 0 ? (
                <div className="text-center py-12 text-zinc-500 text-xs font-mono">Đang tải...</div>
              ) : (
                <Table className="text-xs">
                  <TableHeader className="border-b border-zinc-800">
                    <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                      <TableHead className="text-zinc-400">Mã RMA</TableHead>
                      <TableHead className="text-zinc-400">Khách hàng</TableHead>
                      <TableHead className="text-zinc-400">Ngày tạo</TableHead>
                      <TableHead className="text-zinc-400">Trạng thái</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rmas.map((r) => (
                      <TableRow 
                        key={r.id} 
                        onClick={() => setSelectedRma(r)}
                        className={`border-b border-zinc-800/50 hover:bg-zinc-800/30 cursor-pointer ${
                          selectedRma?.id === r.id ? "bg-zinc-800/80" : ""
                        }`}
                      >
                        <TableCell className="font-bold text-zinc-200 font-mono">{r.rmaNo}</TableCell>
                        <TableCell className="text-zinc-300">{r.customerName}</TableCell>
                        <TableCell className="text-zinc-400">{new Date(r.createdAt).toLocaleDateString()}</TableCell>
                        <TableCell>{getStatusBadge(r.status)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-1">
          {selectedRma ? (
            <Card className="bg-zinc-900 border-zinc-800 text-white sticky top-6">
              <CardHeader className="border-b border-zinc-800 pb-4">
                <CardTitle className="text-sm font-semibold flex items-center justify-between">
                  Chi tiết: {selectedRma.rmaNo}
                  {getStatusBadge(selectedRma.status)}
                </CardTitle>
                <div className="text-[10px] text-zinc-500 mt-1">
                   Tham chiếu: {selectedRma.referenceNo || "N/A"} | Người tạo: {selectedRma.createdBy}
                </div>
              </CardHeader>
              <CardContent className="p-4 flex flex-col gap-4">
                <h4 className="text-[10px] uppercase font-bold text-zinc-500 tracking-wider">Danh sách sản phẩm</h4>
                <div className="space-y-3">
                  {selectedRma.items.map(item => (
                    <div key={item.id} className="bg-zinc-800/50 rounded p-3 border border-zinc-700/50 flex flex-col gap-2">
                      <div className="flex justify-between items-start">
                        <div>
                          <p className="text-[11px] font-bold text-zinc-200">{item.itemCode}</p>
                          <p className="text-[10px] text-zinc-400">{item.itemName}</p>
                        </div>
                        <div className="text-right">
                          <p className="text-[10px] text-zinc-500">Dự kiến: <span className="text-zinc-300 font-bold">{item.qtyExpected}</span></p>
                          <p className="text-[10px] text-zinc-500">Đã nhận: <span className="text-blue-400 font-bold">{item.qtyReceived}</span></p>
                        </div>
                      </div>
                      
                      {selectedRma.status === "RECEIVED" && (
                        <div className="flex gap-2 mt-2 pt-2 border-t border-zinc-700">
                          <Button 
                            disabled={processing}
                            onClick={() => handleProcessQc(selectedRma.id, item, "RESTOCK")}
                            className="bg-emerald-600 hover:bg-emerald-500 text-[10px] h-7 px-2 flex-1"
                          >
                            <CheckCircle2 className="h-3 w-3 mr-1" /> Restock
                          </Button>
                          <Button 
                            disabled={processing}
                            onClick={() => handleProcessQc(selectedRma.id, item, "SCRAP")}
                            className="bg-red-600 hover:bg-red-500 text-[10px] h-7 px-2 flex-1"
                          >
                            <AlertTriangle className="h-3 w-3 mr-1" /> Scrap
                          </Button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
                
                {selectedRma.status === "QC_COMPLETED" && (
                  <div className="mt-4 p-4 bg-emerald-900/20 border border-emerald-800/30 rounded-lg flex flex-col items-center text-center gap-2">
                    <FlaskConical className="h-8 w-8 text-emerald-500 opacity-50" />
                    <p className="text-xs text-emerald-200">Đã hoàn tất quy trình kiểm định QC.</p>
                  </div>
                )}
              </CardContent>
            </Card>
          ) : (
            <div className="bg-zinc-900 border border-zinc-800 rounded-lg p-16 text-center text-zinc-500 text-xs flex flex-col items-center justify-center gap-4">
              <PackageSearch className="h-10 w-10 opacity-20" />
              Chọn một yêu cầu RMA để xem chi tiết và thực hiện xử lý QC.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
