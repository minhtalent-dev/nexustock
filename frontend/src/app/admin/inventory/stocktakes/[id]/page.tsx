"use client";

import { useEffect, useState, use } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";
import { AlertCircle, ArrowLeft, Check, Lock, Play, Send } from "lucide-react";
import Link from "next/link";

interface StocktakeItem {
  id: string;
  locationId: string;
  locationCode: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  lotNo: string;
  systemQty: number;
  countedQty: number | null;
  varianceQty: number | null;
  status: string;
}

interface Stocktake {
  id: string;
  stocktakeNo: string;
  status: string;
  zoneId: string | null;
  totalVarianceAmount: number;
  currentApprovalLevel: number;
  startedAt: string | null;
  startedBy: string | null;
  completedAt: string | null;
  completedBy: string | null;
  createdAt: string;
  createdBy: string;
}

interface DetailsResponse {
  stocktake: Stocktake;
  items: StocktakeItem[];
}

export default function StocktakeDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [stocktake, setStocktake] = useState<Stocktake | null>(null);
  const [items, setItems] = useState<StocktakeItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Dialog nhập số lượng đếm
  const [selectedItem, setSelectedItem] = useState<StocktakeItem | null>(null);
  const [countedQty, setCountedQty] = useState("");
  const [countingModalOpen, setCountingModalOpen] = useState(false);

  // Dialog phê duyệt
  const [reasonCode, setReasonCode] = useState("ADJ-COUNT");
  const [remarks, setRemarks] = useState("");
  const [approveModalOpen, setApproveModalOpen] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  const fetchDetails = async () => {
    try {
      const res = await api.get<DetailsResponse>(`/stocktakes/${id}`);
      if (res.data) {
        setStocktake(res.data.stocktake);
        setItems(res.data.items || []);
      }
    } catch (err) {
      showError("Không thể tải thông tin chi tiết đợt kiểm kê");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDetails();
  }, [id]);

  const handleStart = async () => {
    setActionLoading(true);
    try {
      await api.post(`/stocktakes/${id}/start`);
      showSuccess("Đã bắt đầu kiểm kê. Các kệ đã bị phong tỏa!");
      fetchDetails();
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể bắt đầu kiểm kê");
    } finally {
      setActionLoading(false);
    }
  };

  const handleOpenCountModal = (item: StocktakeItem) => {
    setSelectedItem(item);
    setCountedQty(item.countedQty !== null ? item.countedQty.toString() : "");
    setCountingModalOpen(true);
  };

  const handleSaveCount = async () => {
    if (!selectedItem) return;
    const qty = parseFloat(countedQty);
    if (isNaN(qty) || qty < 0) {
      showError("Số lượng đếm phải là số lớn hơn hoặc bằng 0");
      return;
    }

    setActionLoading(true);
    try {
      await api.post(`/stocktakes/${id}/count`, {
        locationId: selectedItem.locationId,
        itemId: selectedItem.itemId,
        lotNo: selectedItem.lotNo,
        countedQty: qty
      });
      showSuccess("Ghi nhận số lượng đếm thành công!");
      setCountingModalOpen(false);
      fetchDetails();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi ghi nhận kết quả đếm");
    } finally {
      setActionLoading(false);
    }
  };

  const handleSubmitApprove = async () => {
    setActionLoading(true);
    try {
      const res = await api.post(`/stocktakes/${id}/approve`, {
        reasonCode,
        remarks
      });
      showSuccess(res.data.message || "Thao tác thành công!");
      setApproveModalOpen(false);
      fetchDetails();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi phê duyệt điều chỉnh");
    } finally {
      setActionLoading(false);
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case "Draft": return "Nháp";
      case "Counting": return "Đang kiểm đếm";
      case "Pending_L1_Approve": return "Chờ duyệt Cấp 1 (<10 triệu)";
      case "Pending_L2_Approve": return "Chờ duyệt Cấp 2 (10 - 100 triệu)";
      case "Pending_L3_Approve": return "Chờ duyệt Cấp 3 (>100 triệu)";
      case "Approved": return "Đã duyệt và áp dụng điều chỉnh";
      case "Cancelled": return "Đã hủy bỏ";
      default: return status;
    }
  };

  if (loading) return <div className="p-6 text-center text-muted-foreground">Đang tải chi tiết...</div>;
  if (!stocktake) return <div className="p-6 text-center text-red-500">Không tìm thấy đợt kiểm kê</div>;

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/inventory/stocktakes">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <h1 className="text-2xl font-bold">Đợt kiểm kê: {stocktake.stocktakeNo}</h1>
        </div>
        <div className="flex gap-2">
          {stocktake.status === "Draft" && (
            <Button onClick={handleStart} disabled={actionLoading} className="gap-2">
              <Play className="h-4 w-4" />
              Bắt đầu kiểm kê
            </Button>
          )}

          {stocktake.status === "Counting" && (
            <Button onClick={() => setApproveModalOpen(true)} disabled={actionLoading} className="gap-2">
              <Send className="h-4 w-4" />
              Gửi duyệt chênh lệch
            </Button>
          )}

          {stocktake.status.startsWith("Pending_") && (
            <Button onClick={() => setApproveModalOpen(true)} disabled={actionLoading} className="gap-2 bg-green-600 hover:bg-green-700 text-white">
              <Check className="h-4 w-4" />
              Phê duyệt điều chỉnh
            </Button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="md:col-span-1">
          <CardHeader>
            <CardTitle>Thông tin chung</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <Label className="text-muted-foreground">Trạng thái</Label>
              <div className="text-lg font-semibold">{getStatusText(stocktake.status)}</div>
            </div>
            <div>
              <Label className="text-muted-foreground">Giá trị lệch ước tính</Label>
              <div className="text-lg font-mono font-bold text-red-600">
                {stocktake.totalVarianceAmount.toLocaleString()} đ
              </div>
            </div>
            <div>
              <Label className="text-muted-foreground">Cấp duyệt hiện tại</Label>
              <div className="text-lg font-semibold">
                {stocktake.currentApprovalLevel > 0 ? `Cấp L${stocktake.currentApprovalLevel}` : "Chưa xác định"}
              </div>
            </div>
            <div>
              <Label className="text-muted-foreground">Người tạo</Label>
              <div className="font-medium">{stocktake.createdBy}</div>
            </div>
            <div>
              <Label className="text-muted-foreground">Thời gian bắt đầu</Label>
              <div className="font-medium">
                {stocktake.startedAt ? new Date(stocktake.startedAt).toLocaleString() : "Chưa bắt đầu"}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="md:col-span-2">
          <CardHeader>
            <CardTitle>Danh sách vị trí và vật tư kiểm kê</CardTitle>
          </CardHeader>
          <CardContent>
            {stocktake.status === "Draft" ? (
              <div className="py-8 text-center text-muted-foreground flex flex-col items-center gap-2">
                <Lock className="h-8 w-8 text-muted-foreground" />
                <span>Bấm "Bắt đầu kiểm kê" để quét danh sách tồn hiện tại và phong tỏa các vị trí kệ.</span>
              </div>
            ) : items.length === 0 ? (
              <div className="py-8 text-center text-muted-foreground">Không có vật tư nào trong khu vực kiểm kê.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Vị trí</TableHead>
                    <TableHead>Vật tư</TableHead>
                    <TableHead>Lô hàng</TableHead>
                    <TableHead className="text-right">Tồn hệ thống</TableHead>
                    <TableHead className="text-right">Thực tế</TableHead>
                    <TableHead className="text-right">Chênh lệch</TableHead>
                    <TableHead className="text-right">Thao tác</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((item) => {
                    const variance = item.varianceQty;
                    let varianceColor = "text-gray-900";
                    if (variance !== null) {
                      if (variance > 0) varianceColor = "text-green-600 font-bold";
                      if (variance < 0) varianceColor = "text-red-600 font-bold";
                    }

                    return (
                      <TableRow key={item.id}>
                        <TableCell className="font-mono font-semibold">{item.locationCode}</TableCell>
                        <TableCell>
                          <div>{item.itemName}</div>
                          <div className="text-xs text-muted-foreground">{item.itemCode}</div>
                        </TableCell>
                        <TableCell className="font-mono">{item.lotNo}</TableCell>
                        <TableCell className="text-right font-mono">{item.systemQty.toLocaleString()}</TableCell>
                        <TableCell className="text-right font-mono font-semibold">
                          {item.countedQty !== null ? item.countedQty.toLocaleString() : "—"}
                        </TableCell>
                        <TableCell className={`text-right font-mono ${varianceColor}`}>
                          {variance !== null ? (variance > 0 ? `+${variance.toLocaleString()}` : variance.toLocaleString()) : "—"}
                        </TableCell>
                        <TableCell className="text-right">
                          {stocktake.status === "Counting" && (
                            <Button size="sm" variant="outline" onClick={() => handleOpenCountModal(item)}>
                              Đếm
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Dialog nhập kết quả đếm */}
      <Dialog open={countingModalOpen} onOpenChange={setCountingModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Nhập kết quả đếm thực tế</DialogTitle>
          </DialogHeader>
          {selectedItem && (
            <div className="space-y-4 py-4">
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div><span className="text-muted-foreground">Vị trí:</span> <span className="font-semibold">{selectedItem.locationCode}</span></div>
                <div><span className="text-muted-foreground">Lô hàng:</span> <span className="font-semibold">{selectedItem.lotNo}</span></div>
                <div className="col-span-2"><span className="text-muted-foreground">Vật tư:</span> <span className="font-semibold">{selectedItem.itemName} ({selectedItem.itemCode})</span></div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="countedQty">Số lượng đếm thực tế</Label>
                <Input
                  id="countedQty"
                  type="number"
                  step="any"
                  value={countedQty}
                  onChange={(e) => setCountedQty(e.target.value)}
                  placeholder="Nhập số lượng đếm được"
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCountingModalOpen(false)}>Hủy</Button>
            <Button onClick={handleSaveCount} disabled={actionLoading}>Lưu kết quả</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Dialog Phê duyệt hoặc Gửi duyệt */}
      <Dialog open={approveModalOpen} onOpenChange={setApproveModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {stocktake.status === "Counting" ? "Xác nhận gửi duyệt chênh lệch" : "Phê duyệt điều chỉnh tồn kho"}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            {stocktake.status === "Counting" ? (
              <div className="flex gap-2 text-sm text-amber-600 bg-amber-50 p-3 rounded">
                <AlertCircle className="h-5 w-5 shrink-0" />
                <span>Hệ thống sẽ tính tổng giá trị tài chính chênh lệch và chuyển đợt kiểm kê này sang quy trình phê duyệt đa cấp tương ứng.</span>
              </div>
            ) : (
              <div className="space-y-3">
                <div className="text-sm font-semibold">
                  Giá trị chênh lệch cần duyệt: <span className="text-red-600">{stocktake.totalVarianceAmount.toLocaleString()} đ</span> (Cấp L{stocktake.currentApprovalLevel})
                </div>
                <div className="space-y-2">
                  <Label htmlFor="reason">Mã lý do điều chỉnh</Label>
                  <Select onValueChange={(val) => setReasonCode(val)} defaultValue="ADJ-COUNT">
                    <SelectTrigger>
                      <SelectValue placeholder="Chọn mã lý do" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ADJ-COUNT">Điều chỉnh số lượng kiểm kê (ADJ-COUNT)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="remarks">Ghi chú / Ý kiến phê duyệt</Label>
              <Input
                id="remarks"
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                placeholder="Nhập ghi chú..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveModalOpen(false)}>Hủy</Button>
            <Button onClick={handleSubmitApprove} disabled={actionLoading} className="bg-green-600 hover:bg-green-700 text-white">
              {stocktake.status === "Counting" ? "Gửi duyệt" : "Phê duyệt & Áp dụng"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
