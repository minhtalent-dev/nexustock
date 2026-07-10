"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { FileDown, Plus, ClipboardList, CheckCircle2, AlertCircle, Eye } from "lucide-react";

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

interface PartnerDto {
  id: string;
  name: string;
  code: string;
}

interface ProductDto {
  id: string;
  name: string;
  code: string;
}

interface UomDto {
  id: string;
  name: string;
}

interface OrderItemInput {
  itemId: string;
  uomId: string;
  expectedQty: number;
  tolerance: number;
}

export default function InboundPage() {
  const [orders, setOrders] = useState<InboundOrderResponseDto[]>([]);
  const [partners, setPartners] = useState<PartnerDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [uoms, setUoms] = useState<UomDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState("");

  // Dialog State
  const [isOpen, setIsOpen] = useState(false);
  const [partnerId, setPartnerId] = useState("");
  const [orderNo, setOrderNo] = useState("");
  const [items, setItems] = useState<OrderItemInput[]>([]);
  const [saving, setSaving] = useState(false);

  const fetchOrders = async () => {
    setLoading(true);
    try {
      const url = statusFilter ? `/inbound/orders?status=${statusFilter}` : "/inbound/orders";
      const res = await api.get<InboundOrderResponseDto[]>(url);
      setOrders(res.data);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải danh sách phiếu nhập.");
    } finally {
      setLoading(false);
    }
  };

  const fetchMetadata = async () => {
    try {
      const [partnersRes, productsRes, uomsRes] = await Promise.all([
        api.get<{ items: PartnerDto[] }>("/master-data/partners"),
        api.get<{ items: ProductDto[] }>("/master-data/products"),
        api.get<{ items: UomDto[] }>("/master-data/uoms"),
      ]);
      // Dữ liệu API trả về dạng PagedResult, chứa array trong thuộc tính items
      setPartners(partnersRes.data.items || []);
      setProducts(productsRes.data.items || []);
      setUoms(uomsRes.data.items || []);
    } catch (err: any) {
      showError("Không thể tải thông tin đối tác/vật tư.");
    }
  };

  useEffect(() => {
    fetchOrders();
  }, [statusFilter]);

  useEffect(() => {
    fetchMetadata();
  }, []);

  const openCreate = () => {
    setPartnerId("");
    setOrderNo("");
    setItems([{ itemId: "", uomId: "", expectedQty: 1, tolerance: 0 }]);
    setIsOpen(true);
  };

  const addItemRow = () => {
    setItems((prev) => [...prev, { itemId: "", uomId: "", expectedQty: 1, tolerance: 0 }]);
  };

  const removeItemRow = (index: number) => {
    setItems((prev) => prev.filter((_, i) => i !== index));
  };

  const updateItemRow = (index: number, field: keyof OrderItemInput, value: any) => {
    setItems((prev) =>
      prev.map((item, i) => (i === index ? { ...item, [field]: value } : item))
    );
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!partnerId) {
      showError("Vui lòng chọn nhà cung cấp.");
      return;
    }
    if (items.length === 0) {
      showError("Phiếu nhập phải có ít nhất một dòng hàng.");
      return;
    }
    const invalidItem = items.some((item) => !item.itemId || !item.uomId || item.expectedQty <= 0);
    if (invalidItem) {
      showError("Vui lòng nhập đầy đủ thông tin vật tư, đơn vị tính và số lượng.");
      return;
    }

    setSaving(true);
    try {
      await api.post("/inbound/orders", {
        orderNo: orderNo || undefined,
        partnerId,
        items,
      });
      showSuccess("Tạo phiếu nhập hàng thành công.");
      setIsOpen(false);
      fetchOrders();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi tạo phiếu nhập.");
    } finally {
      setSaving(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status.toUpperCase()) {
      case "COMPLETED":
        return <Badge className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">Hoàn thành</Badge>;
      case "RECEIVING":
        return <Badge className="bg-amber-500/10 text-amber-500 border-amber-500/20">Đang nhận</Badge>;
      case "OPEN":
        return <Badge className="bg-blue-500/10 text-blue-500 border-blue-500/20">Mới tạo</Badge>;
      case "CANCELLED":
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">Đã hủy</Badge>;
      default:
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">{status}</Badge>;
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            <ClipboardList className="h-6 w-6 text-emerald-500" />
            Phiếu nhập hàng
          </h1>
          <p className="text-xs text-zinc-400 mt-1">Quản lý nhận hàng từ PO/Invoice, theo dõi và đối soát số lượng thực tế.</p>
        </div>
        <Button onClick={openCreate} className="bg-emerald-600 hover:bg-emerald-500 text-white gap-2 h-9 text-sm">
          <Plus className="h-4 w-4" />
          Tạo phiếu nhập
        </Button>
      </div>

      <div className="flex gap-4">
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="flex h-9 w-48 rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1 text-sm shadow-sm transition-colors text-white focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Open">Mới tạo</option>
          <option value="Receiving">Đang nhận</option>
          <option value="Completed">Hoàn thành</option>
          <option value="Cancelled">Đã hủy</option>
        </select>
      </div>

      <Card className="bg-[#111] border-zinc-800/80">
        <CardHeader className="py-4 border-b border-zinc-800/60 flex flex-row items-center justify-between">
          <CardTitle className="text-sm font-semibold text-white">Danh sách phiếu nhập</CardTitle>
          {loading && <div className="h-4 w-4 animate-spin rounded-full border-2 border-emerald-500 border-t-transparent" />}
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader className="bg-zinc-900/30 border-b border-zinc-800/60">
              <TableRow className="hover:bg-transparent">
                <TableHead className="text-zinc-400 font-semibold h-11">Mã phiếu</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Nhà cung cấp</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Ngày tạo</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Người tạo</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11 text-center w-36">Trạng thái</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11 text-right w-24 pr-6">Thao tác</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orders.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-zinc-500 py-12">
                    Không tìm thấy phiếu nhập nào.
                  </TableCell>
                </TableRow>
              ) : (
                orders.map((o) => (
                  <TableRow key={o.id} className="border-b border-zinc-800/50 hover:bg-zinc-900/20">
                    <TableCell className="text-white font-medium">{o.orderNo}</TableCell>
                    <TableCell className="text-zinc-300">{o.partnerName}</TableCell>
                    <TableCell className="text-zinc-400">{new Date(o.createdAt).toLocaleString("vi-VN")}</TableCell>
                    <TableCell className="text-zinc-400">{o.createdBy || "System"}</TableCell>
                    <TableCell className="text-center">{getStatusBadge(o.status)}</TableCell>
                    <TableCell className="text-right pr-6">
                      <Link href={`/admin/inbound/${o.id}/receive`}>
                        <Button variant="ghost" className="h-8 w-8 p-0 text-zinc-400 hover:text-emerald-500 hover:bg-zinc-800/50">
                          <Eye className="h-4 w-4" />
                        </Button>
                      </Link>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Dialog tạo phiếu */}
      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="bg-zinc-950 border-zinc-800 text-white max-w-3xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="text-white flex items-center gap-2">
              <Plus className="h-5 w-5 text-emerald-500" />
              Tạo phiếu nhập mới
            </DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSave} className="space-y-6">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="orderNo" className="text-zinc-300 text-xs">Mã phiếu nhập (Tự động sinh nếu để trống)</Label>
                <Input
                  id="orderNo"
                  placeholder="Ví dụ: IO-20260710-001"
                  value={orderNo}
                  onChange={(e) => setOrderNo(e.target.value)}
                  className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="partner" className="text-zinc-300 text-xs">Nhà cung cấp *</Label>
                <select
                  id="partner"
                  value={partnerId}
                  onChange={(e) => setPartnerId(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-zinc-800 bg-zinc-900 px-3 py-1 text-sm shadow-sm transition-colors text-white focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                >
                  <option value="">Chọn nhà cung cấp...</option>
                  {partners.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.code})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="space-y-4">
              <div className="flex items-center justify-between border-b border-zinc-800 pb-2">
                <Label className="text-zinc-200 text-sm font-semibold">Chi tiết dòng hàng</Label>
                <Button type="button" onClick={addItemRow} size="sm" className="bg-zinc-800 hover:bg-zinc-700 text-white text-xs gap-1.5 h-8">
                  <Plus className="h-3.5 w-3.5" />
                  Thêm dòng
                </Button>
              </div>

              {items.map((item, index) => (
                <div key={index} className="grid grid-cols-12 gap-3 items-end bg-zinc-900/30 p-3 rounded-lg border border-zinc-850">
                  <div className="col-span-4 space-y-1">
                    <Label className="text-zinc-400 text-[10px]">Vật tư *</Label>
                    <select
                      value={item.itemId}
                      onChange={(e) => updateItemRow(index, "itemId", e.target.value)}
                      className="flex h-9 w-full rounded-md border border-zinc-800 bg-zinc-900 px-2 py-1 text-xs text-white focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                    >
                      <option value="">Chọn vật tư...</option>
                      {products.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.code})
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-span-3 space-y-1">
                    <Label className="text-zinc-400 text-[10px]">Đơn vị tính *</Label>
                    <select
                      value={item.uomId}
                      onChange={(e) => updateItemRow(index, "uomId", e.target.value)}
                      className="flex h-9 w-full rounded-md border border-zinc-800 bg-zinc-900 px-2 py-1 text-xs text-white focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-emerald-500"
                    >
                      <option value="">Chọn ĐVT...</option>
                      {uoms.map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-span-2 space-y-1">
                    <Label className="text-zinc-400 text-[10px]">Số lượng yêu cầu *</Label>
                    <Input
                      type="number"
                      min={0.01}
                      step="any"
                      value={item.expectedQty}
                      onChange={(e) => updateItemRow(index, "expectedQty", parseFloat(e.target.value) || 0)}
                      className="h-9 bg-zinc-900 border-zinc-800 text-xs text-white focus-visible:ring-emerald-500"
                    />
                  </div>
                  <div className="col-span-2 space-y-1">
                    <Label className="text-zinc-400 text-[10px]">Dung sai nhận (%)</Label>
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      step={1}
                      value={item.tolerance * 100}
                      onChange={(e) => updateItemRow(index, "tolerance", (parseFloat(e.target.value) || 0) / 100)}
                      className="h-9 bg-zinc-900 border-zinc-800 text-xs text-white focus-visible:ring-emerald-500"
                    />
                  </div>
                  <div className="col-span-1 text-right">
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => removeItemRow(index)}
                      className="h-9 w-9 p-0 text-zinc-500 hover:text-red-400 hover:bg-zinc-800"
                    >
                      X
                    </Button>
                  </div>
                </div>
              ))}
            </div>

            <DialogFooter className="border-t border-zinc-800 pt-4 flex gap-2">
              <Button type="button" variant="ghost" onClick={() => setIsOpen(false)} className="text-zinc-400 hover:text-white">
                Hủy bỏ
              </Button>
              <Button type="submit" disabled={saving} className="bg-emerald-600 hover:bg-emerald-500 text-white min-w-24">
                {saving ? "Đang lưu..." : "Xác nhận"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
