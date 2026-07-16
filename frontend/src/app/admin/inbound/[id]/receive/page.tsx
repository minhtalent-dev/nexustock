"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, CheckCircle2, AlertTriangle, Plus, ShieldAlert } from "lucide-react";

interface InboundOrderResponseDto {
  id: string;
  orderNo: string;
  partnerId: string;
  partnerName: string;
  status: string;
  createdAt: string;
  createdBy: string;
  items: InboundOrderItemResponseDto[];
}

interface InboundOrderItemResponseDto {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  uomId: string;
  uomName: string;
  expectedQty: number;
  receivedQty: number;
  tolerance: number;
}

interface LocationDto {
  id: string;
  name: string;
  code: string;
}

export default function ReceivePage() {
  const params = useParams();
  const orderId = params.id as string;

  const [order, setOrder] = useState<InboundOrderResponseDto | null>(null);
  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Dialog State
  const [isOpen, setIsOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState<InboundOrderItemResponseDto | null>(null);
  const [lotNo, setLotNo] = useState("");
  const [receivedQty, setReceivedQty] = useState(0);
  const [toLocationId, setToLocationId] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [productionDate, setProductionDate] = useState("");
  const [saving, setSaving] = useState(false);

  const fetchOrderDetails = useCallback(async () => {
    try {
      const res = await api.get<InboundOrderResponseDto>(`/inbound/orders/${orderId}`);
      setOrder(res.data);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải chi tiết phiếu nhập."));
    }
  }, [orderId]);

  const fetchLocations = useCallback(async () => {
    try {
      const res = await api.get<{ items: LocationDto[] }>("/master-data/storage-locations");
      setLocations(res.data.items || []);
    } catch {
      showError("Không thể tải danh sách vị trí kho.");
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => {
      const init = async () => {
        setLoading(true);
        await Promise.all([fetchOrderDetails(), fetchLocations()]);
        setLoading(false);
      };
      void init();
    });
  }, [fetchOrderDetails, fetchLocations]);

  const openReceiveDialog = (item: InboundOrderItemResponseDto) => {
    setSelectedItem(item);
    setLotNo("");
    // Mặc định số lượng còn lại cần nhận
    const remain = Math.max(0, item.expectedQty - item.receivedQty);
    setReceivedQty(remain);
    setToLocationId("");
    setExpiryDate("");
    setProductionDate("");
    setIsOpen(true);
  };

  const handleReceive = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    if (!lotNo.trim()) {
      showError("Vui lòng nhập số lô hàng.");
      return;
    }
    if (receivedQty <= 0) {
      showError("Số lượng nhận thực tế phải lớn hơn 0.");
      return;
    }
    if (!toLocationId) {
      showError("Vui lòng chọn vị trí lưu kho.");
      return;
    }

    setSaving(true);
    try {
      await api.post(`/inbound/orders/${orderId}/receive`, {
        itemId: selectedItem.itemId,
        lotNo,
        receivedQty,
        toLocationId,
        expiryDate: expiryDate ? new Date(expiryDate).toISOString() : null,
        productionDate: productionDate ? new Date(productionDate).toISOString() : null,
      });

      showSuccess(`Đã nhận hàng thành công cho vật tư ${selectedItem.itemName}.`);
      setIsOpen(false);
      await fetchOrderDetails();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi nhận hàng."));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4 text-zinc-400">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-emerald-500 border-t-transparent" />
        <span className="text-sm">Đang tải chi tiết phiếu nhập...</span>
      </div>
    );
  }

  if (!order) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4 text-zinc-400">
        <AlertTriangle className="h-12 w-12 text-red-500" />
        <span className="text-sm">Không tìm thấy thông tin phiếu nhập hàng.</span>
        <Link href="/admin/inbound">
          <Button className="bg-zinc-800 hover:bg-zinc-700 text-white gap-2">
            <ArrowLeft className="h-4 w-4" />
            Quay lại danh sách
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div className="flex items-center gap-4">
        <Link href="/admin/inbound">
          <Button variant="ghost" className="h-9 w-9 p-0 border border-zinc-800 hover:bg-zinc-850 text-zinc-400 hover:text-white">
            <ArrowLeft className="h-4 w-4" />
          </Button>
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            Chi tiết nhận hàng: {order.orderNo}
          </h1>
          <p className="text-xs text-zinc-400 mt-1">
            Nhà cung cấp: <span className="text-emerald-400 font-semibold">{order.partnerName}</span> • Trạng thái: {order.status}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-3 gap-6">
        <Card className="bg-[#111] border-zinc-800/80 col-span-3">
          <CardHeader className="py-4 border-b border-zinc-800/60">
            <CardTitle className="text-sm font-semibold text-white">Chi tiết dòng hàng cần nhận</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader className="bg-zinc-900/30 border-b border-zinc-800/60">
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-zinc-400 font-semibold h-11">Vật tư</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11">Đơn vị tính</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-right">Số lượng yêu cầu</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-right">Số lượng đã nhận</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-right">Dung sai (%)</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-right">Tiến độ</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-right w-32 pr-6">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {order.items.map((i) => {
                  const progressPercent = Math.min(100, Math.round((i.receivedQty / i.expectedQty) * 100)) || 0;
                  const isCompleted = i.receivedQty >= i.expectedQty;

                  return (
                    <TableRow key={i.id} className="border-b border-zinc-800/50 hover:bg-zinc-900/20">
                      <TableCell className="text-white font-medium">
                        <div>
                          <p>{i.itemName}</p>
                          <p className="text-[10px] text-zinc-500 font-normal">{i.itemCode}</p>
                        </div>
                      </TableCell>
                      <TableCell className="text-zinc-300">{i.uomName}</TableCell>
                      <TableCell className="text-right text-zinc-300 font-mono">{i.expectedQty}</TableCell>
                      <TableCell className="text-right text-emerald-400 font-mono font-semibold">{i.receivedQty}</TableCell>
                      <TableCell className="text-right text-zinc-400 font-mono">{(i.tolerance * 100).toFixed(0)}%</TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-2">
                          <div className="w-16 bg-zinc-800 rounded-full h-1.5 overflow-hidden">
                            <div
                              className={`h-full rounded-full ${isCompleted ? "bg-emerald-500" : "bg-amber-500"}`}
                              style={{ width: `${progressPercent}%` }}
                            />
                          </div>
                          <span className="text-xs font-mono text-zinc-400">{progressPercent}%</span>
                        </div>
                      </TableCell>
                      <TableCell className="text-right pr-6">
                        {order.status === "Completed" || order.status === "Cancelled" ? (
                          <span className="text-xs text-zinc-500">N/A</span>
                        ) : (
                          <Button
                            onClick={() => openReceiveDialog(i)}
                            className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-3 py-1 gap-1.5"
                          >
                            <Plus className="h-3.5 w-3.5" />
                            Nhận hàng
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      {/* Dialog nhận hàng */}
      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="bg-zinc-950 border-zinc-800 text-white max-w-lg">
          <DialogHeader>
            <DialogTitle className="text-white flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-emerald-500" />
              Nhận hàng thực tế
            </DialogTitle>
          </DialogHeader>
          {selectedItem && (
            <form onSubmit={handleReceive} className="space-y-4">
              <div className="bg-zinc-900/60 p-3 rounded-lg border border-zinc-850">
                <p className="text-xs text-zinc-500 font-semibold uppercase">Vật tư cần nhận</p>
                <p className="text-sm font-bold text-white mt-0.5">{selectedItem.itemName}</p>
                <p className="text-[10px] text-zinc-400 font-normal">Mã vật tư: {selectedItem.itemCode}</p>
                <div className="grid grid-cols-3 gap-2 mt-3 text-xs text-zinc-400">
                  <div>
                    <p className="text-zinc-500">Yêu cầu</p>
                    <p className="font-semibold text-zinc-200 mt-0.5 font-mono">{selectedItem.expectedQty} {selectedItem.uomName}</p>
                  </div>
                  <div>
                    <p className="text-zinc-500">Đã nhận</p>
                    <p className="font-semibold text-emerald-400 mt-0.5 font-mono">{selectedItem.receivedQty} {selectedItem.uomName}</p>
                  </div>
                  <div>
                    <p className="text-zinc-500">Dung sai</p>
                    <p className="font-semibold text-amber-500 mt-0.5 font-mono">{(selectedItem.tolerance * 100).toFixed(0)}%</p>
                  </div>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="lotNo" className="text-zinc-300 text-xs">Số lô hàng (Lot no) *</Label>
                <Input
                  id="lotNo"
                  placeholder="Ví dụ: LOT-20260710-01"
                  value={lotNo}
                  onChange={(e) => setLotNo(e.target.value)}
                  className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="qty" className="text-zinc-300 text-xs">Số lượng nhận *</Label>
                  <Input
                    id="qty"
                    type="number"
                    min={0.01}
                    step="any"
                    value={receivedQty}
                    onChange={(e) => setReceivedQty(parseFloat(e.target.value) || 0)}
                    className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500 font-mono"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="location" className="text-zinc-300 text-xs">Vị trí lưu kho *</Label>
                  <select
                    id="location"
                    value={toLocationId}
                    onChange={(e) => setToLocationId(e.target.value)}
                    className="flex h-10 w-full rounded-md border border-zinc-800 bg-zinc-900 px-3 py-1 text-sm shadow-sm transition-colors text-white focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                  >
                    <option value="">Chọn vị trí...</option>
                    {locations.map((loc) => (
                      <option key={loc.id} value={loc.id}>
                        {loc.name} ({loc.code})
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="prodDate" className="text-zinc-300 text-xs">Ngày sản xuất</Label>
                  <Input
                    id="prodDate"
                    type="date"
                    value={productionDate}
                    onChange={(e) => setProductionDate(e.target.value)}
                    className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="expDate" className="text-zinc-300 text-xs">Hạn sử dụng</Label>
                  <Input
                    id="expDate"
                    type="date"
                    value={expiryDate}
                    onChange={(e) => setExpiryDate(e.target.value)}
                    className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500"
                  />
                </div>
              </div>

              {/* Thông báo nếu vượt quá dung sai cho phép */}
              {receivedQty + selectedItem.receivedQty > selectedItem.expectedQty * (1 + selectedItem.tolerance) && (
                <div className="flex gap-2 p-3 bg-red-900/10 border border-red-500/20 rounded-lg text-xs text-red-400">
                  <ShieldAlert className="h-4 w-4 shrink-0" />
                  <p>Số lượng nhận vượt quá dung sai cho phép. Cần quyền &quot;Inbound.Orders.Approve&quot; để phê duyệt giao dịch này.</p>
                </div>
              )}

              <DialogFooter className="border-t border-zinc-800 pt-4 flex gap-2">
                <Button type="button" variant="ghost" onClick={() => setIsOpen(false)} className="text-zinc-400 hover:text-white">
                  Hủy bỏ
                </Button>
                <Button type="submit" disabled={saving} className="bg-emerald-600 hover:bg-emerald-500 text-white min-w-24">
                  {saving ? "Đang xử lý..." : "Xác nhận"}
                </Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
