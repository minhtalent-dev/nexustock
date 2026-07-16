"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ClipboardCheck, Plus, RefreshCw } from "lucide-react";
import Link from "next/link";

interface Stocktake {
  id: string;
  stocktakeNo: string;
  status: string;
  zoneName: string;
  totalVarianceAmount: number;
  currentApprovalLevel: number;
  createdAt: string;
  createdBy: string;
}

export default function StocktakesPage() {
  const [stocktakes, setStocktakes] = useState<Stocktake[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchStocktakes = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<Stocktake[]>("/stocktakes");
      setStocktakes(res.data || []);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải danh sách đợt kiểm kê."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => void fetchStocktakes());
  }, [fetchStocktakes]);

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Draft":
        return <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-700">Nháp</span>;
      case "Counting":
        return <span className="rounded-full bg-blue-100 px-2 py-1 text-xs font-semibold text-blue-700">Đang đếm</span>;
      case "Pending_L1_Approve":
        return <span className="rounded-full bg-yellow-100 px-2 py-1 text-xs font-semibold text-yellow-700">Chờ duyệt L1</span>;
      case "Pending_L2_Approve":
        return <span className="rounded-full bg-orange-100 px-2 py-1 text-xs font-semibold text-orange-700">Chờ duyệt L2</span>;
      case "Pending_L3_Approve":
        return <span className="rounded-full bg-red-100 px-2 py-1 text-xs font-semibold text-red-700">Chờ duyệt L3</span>;
      case "Approved":
        return <span className="rounded-full bg-green-100 px-2 py-1 text-xs font-semibold text-green-700">Đã duyệt</span>;
      case "Cancelled":
        return <span className="rounded-full bg-red-100 px-2 py-1 text-xs font-semibold text-red-700">Đã hủy</span>;
      default:
        return <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-700">{status}</span>;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <ClipboardCheck className="h-6 w-6 text-primary" />
          Kiểm kê chu kỳ
        </h1>
        <div className="flex gap-2">
          <Button asChild>
            <Link href="/admin/inventory/stocktakes/new" className="gap-2">
              <Plus className="h-4 w-4" />
              Tạo đợt kiểm kê
            </Link>
          </Button>
          <Button onClick={fetchStocktakes} variant="outline" className="gap-2">
            <RefreshCw className="h-4 w-4" />
            Tải lại
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Danh sách đợt kiểm kê</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-8 text-center text-muted-foreground">Đang tải danh sách...</div>
          ) : stocktakes.length === 0 ? (
            <div className="py-8 text-center text-muted-foreground">Không có đợt kiểm kê nào.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Mã đợt</TableHead>
                  <TableHead>Khu vực</TableHead>
                  <TableHead>Giá trị lệch (VNĐ)</TableHead>
                  <TableHead>Trạng thái</TableHead>
                  <TableHead>Người tạo</TableHead>
                  <TableHead>Thời gian tạo</TableHead>
                  <TableHead className="text-right">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {stocktakes.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="font-semibold">{s.stocktakeNo}</TableCell>
                    <TableCell>{s.zoneName}</TableCell>
                    <TableCell className="font-mono">{s.totalVarianceAmount.toLocaleString()} đ</TableCell>
                    <TableCell>{getStatusBadge(s.status)}</TableCell>
                    <TableCell>{s.createdBy}</TableCell>
                    <TableCell>{new Date(s.createdAt).toLocaleString()}</TableCell>
                    <TableCell className="text-right">
                      <Button asChild size="sm" variant="outline">
                        <Link href={`/admin/inventory/stocktakes/${s.id}`}>Chi tiết</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
