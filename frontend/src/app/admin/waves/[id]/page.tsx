"use client";

import { useCallback, useEffect, useState, use } from "react";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { RefreshCw, ArrowLeft, Play, LayoutGrid, CheckSquare, Layers } from "lucide-react";

interface WaveItemDetail {
  id: string;
  shipmentId: string;
  shipmentNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  uomName: string;
  qtyExpected: number;
  qtyAllocated: number;
  qtyPicked: number;
  qtySorted: number;
  recommendedSlotNumber: number | null;
}

interface WavePickTask {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  fromLocationId: string;
  locationCode: string;
  qtyToPick: number;
  qtyPicked: number;
  status: string;
}

interface WaveDetailResponse {
  id: string;
  waveNo: string;
  status: string;
  createdAt: string;
  createdBy: string;
  items: WaveItemDetail[];
  pickTasks: WavePickTask[];
}

export default function WaveDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = use(params);
  const waveId = resolvedParams.id;
  const [wave, setWave] = useState<WaveDetailResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [processing, setProcessing] = useState(false);

  const fetchWaveDetails = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WaveDetailResponse>(`/waves/${waveId}`);
      setWave(res.data);
    } catch {
      showError("Không thể tải chi tiết đợt Wave.");
    } finally {
      setLoading(false);
    }
  }, [waveId]);

  useEffect(() => {
    queueMicrotask(() => void fetchWaveDetails());
  }, [fetchWaveDetails]);

  const handleReleaseWave = async () => {
    setProcessing(true);
    try {
      await api.post(`/waves/${waveId}/release`);
      showSuccess("Đã giải phóng đợt lấy hàng (Release Wave).");
      fetchWaveDetails();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi release Wave."));
    } finally {
      setProcessing(false);
    }
  };

  const handleCompleteWave = async () => {
    setProcessing(true);
    try {
      await api.post(`/waves/${waveId}/complete`);
      showSuccess("Hoàn thành phân chia đợt Wave.");
      fetchWaveDetails();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi hoàn thành Wave."));
    } finally {
      setProcessing(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "DRAFT": return <Badge className="bg-zinc-700 hover:bg-zinc-600 text-zinc-200">Bản nháp</Badge>;
      case "RELEASED": return <Badge className="bg-blue-600 hover:bg-blue-500 text-white">Đang lấy hàng</Badge>;
      case "SORTING": return <Badge className="bg-amber-600 hover:bg-amber-500 text-white">Phân loại Put-Wall</Badge>;
      case "COMPLETED": return <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white">Hoàn thành</Badge>;
      default: return <Badge variant="outline">{status}</Badge>;
    }
  };

  if (loading && !wave) {
    return <div className="text-center py-12 text-zinc-500 text-xs font-mono">Đang tải...</div>;
  }

  if (!wave) {
    return <div className="text-center py-12 text-zinc-500 text-xs">Không tìm thấy thông tin Wave.</div>;
  }

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Link href="/admin/waves">
            <Button variant="outline" className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 w-9 p-0">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-3">
              <Layers className="h-6 w-6 text-indigo-400" />
              Đợt Wave: {wave.waveNo}
            </h1>
            <p className="text-xs text-zinc-400 mt-1">
              Người tạo: {wave.createdBy} | Ngày tạo: {new Date(wave.createdAt).toLocaleString()}
            </p>
          </div>
        </div>

        <div className="flex gap-2">
          {wave.status === "DRAFT" && (
            <Button
              onClick={handleReleaseWave}
              disabled={processing}
              className="bg-indigo-600 hover:bg-indigo-500 text-white flex items-center gap-2 h-9 text-xs px-4"
            >
              <Play className="h-4 w-4" />
              Release Wave
            </Button>
          )}

          {wave.status === "SORTING" && (
            <Button
              onClick={handleCompleteWave}
              disabled={processing}
              className="bg-emerald-600 hover:bg-emerald-500 text-white flex items-center gap-2 h-9 text-xs px-4"
            >
              <CheckSquare className="h-4 w-4" />
              Hoàn thành Phân chia
            </Button>
          )}

          {(wave.status === "SORTING" || wave.status === "RELEASED" || wave.status === "COMPLETED") && (
            <Link href={`/admin/waves/${wave.id}/put-wall`}>
              <Button
                className="bg-amber-600 hover:bg-amber-500 text-white flex items-center gap-2 h-9 text-xs px-4"
              >
                <LayoutGrid className="h-4 w-4" />
                Put-Wall động
              </Button>
            </Link>
          )}

          <Button
            onClick={fetchWaveDetails}
            variant="outline"
            className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 px-4 flex items-center gap-2 text-xs"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            Làm mới
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="bg-zinc-900 border-zinc-800 text-white">
          <CardHeader className="border-b border-zinc-800 pb-3">
            <CardTitle className="text-xs font-semibold text-zinc-400">Trạng thái đợt Wave</CardTitle>
          </CardHeader>
          <CardContent className="pt-4 flex flex-col gap-2">
            <div className="flex justify-between items-center text-xs">
              <span className="text-zinc-500">Mã đợt Wave:</span>
              <span className="font-mono font-bold">{wave.waveNo}</span>
            </div>
            <div className="flex justify-between items-center text-xs">
              <span className="text-zinc-500">Trạng thái:</span>
              <span>{getStatusBadge(wave.status)}</span>
            </div>
            <div className="flex justify-between items-center text-xs">
              <span className="text-zinc-500">Số đơn gộp:</span>
              <span className="font-bold">{Array.from(new Set(wave.items.map(i => i.shipmentId))).length}</span>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-col gap-4">
        <h2 className="text-base font-bold text-zinc-300">Danh sách các nhiệm vụ lấy hàng tổng hợp (Pick Tasks)</h2>
        <Card className="bg-zinc-900 border-zinc-800 text-white">
          <CardContent className="p-0">
            {wave.pickTasks.length === 0 ? (
              <div className="text-center py-8 text-zinc-500 text-xs">Chưa có nhiệm vụ pick nào được tạo (Cần Release Wave).</div>
            ) : (
              <Table className="text-xs">
                <TableHeader className="border-b border-zinc-800">
                  <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                    <TableHead className="text-zinc-400">Vật tư</TableHead>
                    <TableHead className="text-zinc-400">Mã vật tư</TableHead>
                    <TableHead className="text-zinc-400">Vị trí lấy hàng (From Loc)</TableHead>
                    <TableHead className="text-zinc-400 text-right">Số lượng yêu cầu</TableHead>
                    <TableHead className="text-zinc-400 text-right">Số lượng đã pick</TableHead>
                    <TableHead className="text-zinc-400">Trạng thái</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {wave.pickTasks.map((t) => (
                    <TableRow key={t.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/20">
                      <TableCell className="text-zinc-200 font-bold">{t.itemName}</TableCell>
                      <TableCell className="text-zinc-400 font-mono">{t.itemCode}</TableCell>
                      <TableCell className="text-indigo-400 font-bold font-mono">{t.locationCode}</TableCell>
                      <TableCell className="text-right text-zinc-300 font-bold">{t.qtyToPick.toLocaleString()}</TableCell>
                      <TableCell className="text-right text-zinc-300 font-bold">{t.qtyPicked.toLocaleString()}</TableCell>
                      <TableCell>
                        <Badge variant="outline" className={t.status === "COMPLETED" ? "border-emerald-800 text-emerald-400" : "border-amber-800 text-amber-400"}>
                          {t.status}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-col gap-4">
        <h2 className="text-base font-bold text-zinc-300">Chi tiết sản phẩm phân chia theo đơn xuất (Wave Items)</h2>
        <Card className="bg-zinc-900 border-zinc-800 text-white">
          <CardContent className="p-0">
            <Table className="text-xs">
              <TableHeader className="border-b border-zinc-800">
                <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                  <TableHead className="text-zinc-400">Đơn xuất</TableHead>
                  <TableHead className="text-zinc-400">Ô Put-Wall</TableHead>
                  <TableHead className="text-zinc-400">Vật tư</TableHead>
                  <TableHead className="text-zinc-400 text-right">Yêu cầu</TableHead>
                  <TableHead className="text-zinc-400 text-right">Phân bổ</TableHead>
                  <TableHead className="text-zinc-400 text-right">Đã Pick</TableHead>
                  <TableHead className="text-zinc-400 text-right">Đã Sort</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {wave.items.map((i) => (
                  <TableRow key={i.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/20">
                    <TableCell className="font-bold text-zinc-200 font-mono">{i.shipmentNo}</TableCell>
                    <TableCell>
                      {i.recommendedSlotNumber ? (
                        <Badge className="bg-amber-600 text-white font-mono">Slot {i.recommendedSlotNumber}</Badge>
                      ) : (
                        <span className="text-zinc-500 italic">Chưa gán</span>
                      )}
                    </TableCell>
                    <TableCell className="text-zinc-300">
                      {i.itemName} <span className="text-zinc-500 text-[10px] font-mono">({i.itemCode})</span>
                    </TableCell>
                    <TableCell className="text-right text-zinc-300">{i.qtyExpected.toLocaleString()} {i.uomName}</TableCell>
                    <TableCell className="text-right text-zinc-300">{i.qtyAllocated.toLocaleString()}</TableCell>
                    <TableCell className="text-right text-zinc-300 font-bold">{i.qtyPicked.toLocaleString()}</TableCell>
                    <TableCell className="text-right text-zinc-300 font-bold text-emerald-400">{i.qtySorted.toLocaleString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
