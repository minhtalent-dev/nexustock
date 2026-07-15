"use client";

import { useState } from "react";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError } from "@/lib/toast";
import { Search, Tag, AlertCircle } from "lucide-react";

interface LotResponseDto {
  id: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  expiryDate: string | null;
  productionDate: string | null;
  qcStatus: string;
}

export default function LotsPage() {
  const [searchLotNo, setSearchLotNo] = useState("");
  const [lots, setLots] = useState<LotResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchLotNo.trim()) {
      showError("Vui lòng nhập số lô hàng để tìm kiếm.");
      return;
    }

    setLoading(true);
    setSearched(true);
    try {
      const res = await api.get<LotResponseDto[]>(`/lots/${searchLotNo.trim()}`);
      setLots(res.data);
    } catch (err: any) {
      if (err.response?.status === 404) {
        setLots([]);
      } else {
        showError(err.response?.data?.message || "Lỗi khi tra cứu lô hàng.");
      }
    } finally {
      setLoading(false);
    }
  };

  const getQcBadge = (status: string) => {
    switch (status.toUpperCase()) {
      case "RELEASE":
        return <Badge className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">Release (Đạt)</Badge>;
      case "HOLD":
        return <Badge className="bg-amber-500/10 text-amber-500 border-amber-500/20">Hold (Chờ kiểm)</Badge>;
      case "REJECT":
        return <Badge className="bg-red-500/10 text-red-500 border-red-500/20">Reject (Không đạt)</Badge>;
      case "UNSPEC":
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">Chưa xác định</Badge>;
      default:
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">{status}</Badge>;
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-3">
          <Tag className="h-6 w-6 text-emerald-500" />
          Tra cứu lô hàng
        </h1>
        <p className="text-xs text-zinc-400 mt-1">Tra cứu thông tin ngày sản xuất, hạn sử dụng và trạng thái QC của lô hàng thực tế.</p>
      </div>

      <Card className="bg-[#111] border-zinc-800/80">
        <CardContent className="p-6">
          <form onSubmit={handleSearch} className="flex gap-3 max-w-md">
            <Input
              placeholder="Nhập số lô hàng (ví dụ: LOT-...)"
              value={searchLotNo}
              onChange={(e) => setSearchLotNo(e.target.value)}
              className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500 text-sm h-10"
            />
            <Button type="submit" disabled={loading} className="bg-emerald-600 hover:bg-emerald-500 text-white h-10 px-5 gap-2 shrink-0">
              {loading ? (
                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
              ) : (
                <Search className="h-4 w-4" />
              )}
              Tìm kiếm
            </Button>
          </form>
        </CardContent>
      </Card>

      {searched && (
        <Card className="bg-[#111] border-zinc-800/80">
          <CardHeader className="py-4 border-b border-zinc-800/60">
            <CardTitle className="text-sm font-semibold text-white">Kết quả tra cứu</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader className="bg-zinc-900/30 border-b border-zinc-800/60">
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-zinc-400 font-semibold h-11">Số lô hàng</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11">Vật tư</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-center w-40">Ngày sản xuất</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-center w-40">Hạn sử dụng</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-center w-40">Trạng thái QC</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {lots.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-zinc-500 py-12">
                      <div className="flex flex-col items-center gap-2">
                        <AlertCircle className="h-8 w-8 text-zinc-600" />
                        <span>Không tìm thấy lô hàng nào trùng khớp với số lô đã nhập.</span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  lots.map((l) => (
                    <TableRow key={l.id} className="border-b border-zinc-800/50 hover:bg-zinc-900/20">
                      <TableCell className="text-white font-semibold">
                        <Link href={`/admin/genealogy/${l.lotNo}`} className="text-indigo-400 hover:underline">
                          {l.lotNo}
                        </Link>
                      </TableCell>
                      <TableCell className="text-zinc-300">
                        <div>
                          <p>{l.itemName}</p>
                          <p className="text-[10px] text-zinc-500 font-normal">{l.itemCode}</p>
                        </div>
                      </TableCell>
                      <TableCell className="text-center text-zinc-400 font-mono">
                        {l.productionDate ? new Date(l.productionDate).toLocaleDateString("vi-VN") : "—"}
                      </TableCell>
                      <TableCell className="text-center text-zinc-400 font-mono">
                        {l.expiryDate ? new Date(l.expiryDate).toLocaleDateString("vi-VN") : "—"}
                      </TableCell>
                      <TableCell className="text-center">{getQcBadge(l.qcStatus)}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
