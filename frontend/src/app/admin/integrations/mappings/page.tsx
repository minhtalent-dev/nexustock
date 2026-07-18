"use client";

import { useEffect, useState, useCallback } from "react";
import {
  getIntegrationMappings,
  createIntegrationMapping,
  updateIntegrationMapping,
  deleteIntegrationMapping
} from "@/features/erp-integration/api";
import { IntegrationMapping } from "@/features/erp-integration/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { showError, showSuccess } from "@/lib/toast";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";

export default function IntegrationMappingsPage() {
  const [mappings, setMappings] = useState<IntegrationMapping[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [mappingType, setMappingType] = useState<string>("all");
  const [searchCode, setSearchCode] = useState("");
  const [loading, setLoading] = useState(false);

  // Dialog State
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [newMapping, setNewMapping] = useState({
    externalSystem: "SAP-ERP",
    mappingType: "item",
    externalCode: "",
    internalCode: ""
  });
  const [isEditing, setIsEditing] = useState<IntegrationMapping | null>(null);
  const [editForm, setEditForm] = useState({
    internalCode: "",
    status: "active" as "active" | "inactive"
  });

  const fetchMappings = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getIntegrationMappings({
        mappingType: mappingType === "all" ? undefined : mappingType,
        externalCode: searchCode.trim() || undefined,
        page,
        pageSize
      });
      setMappings(data.items);
      setTotal(data.total);
    } catch {
      showError("Không thể tải cấu hình ánh xạ.");
    } finally {
      setLoading(false);
    }
  }, [mappingType, searchCode, page, pageSize]);

  useEffect(() => {
    queueMicrotask(() => void fetchMappings());
  }, [fetchMappings]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    fetchMappings();
  };

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newMapping.externalCode.trim() || !newMapping.internalCode.trim()) {
      showError("Vui lòng điền đầy đủ thông tin.");
      return;
    }
    try {
      await createIntegrationMapping(newMapping);
      showSuccess("Tạo ánh xạ thành công.");
      setIsAddOpen(false);
      setNewMapping({
        externalSystem: "SAP-ERP",
        mappingType: "item",
        externalCode: "",
        internalCode: ""
      });
      fetchMappings();
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      showError(error.response?.data?.message || "Lỗi khi tạo ánh xạ.");
    }
  };

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isEditing) return;
    if (!editForm.internalCode.trim()) {
      showError("Mã WMS không được để trống.");
      return;
    }
    try {
      await updateIntegrationMapping(isEditing.id, editForm);
      showSuccess("Cập nhật ánh xạ thành công.");
      setIsEditing(null);
      fetchMappings();
    } catch {
      showError("Lỗi khi cập nhật ánh xạ.");
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Bạn có chắc chắn muốn xóa ánh xạ này không?")) return;
    try {
      await deleteIntegrationMapping(id);
      showSuccess("Xóa ánh xạ thành công.");
      fetchMappings();
    } catch {
      showError("Lỗi khi xóa ánh xạ.");
    }
  };

  const getTypeLabel = (type: string) => {
    switch (type) {
      case "item": return "Vật tư (Item)";
      case "warehouse": return "Nhà kho (Warehouse)";
      case "partner": return "Đối tác (Partner)";
      case "uom": return "Đơn vị tính (UOM)";
      default: return type;
    }
  };

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">Ánh xạ dữ liệu ERP (SAP -&gt; WMS)</h1>
        <div className="flex gap-4">
          <form onSubmit={handleSearch} className="flex gap-2">
            <Input
              placeholder="Mã ERP..."
              value={searchCode}
              onChange={(e) => setSearchCode(e.target.value)}
              className="bg-zinc-900 border-zinc-800 text-white w-48 text-xs h-9"
            />
            <Select value={mappingType} onValueChange={(val) => { setMappingType(val); setPage(1); }}>
              <SelectTrigger className="bg-zinc-900 border-zinc-800 text-white w-40 text-xs h-9">
                <SelectValue placeholder="Loại dữ liệu" />
              </SelectTrigger>
              <SelectContent className="bg-zinc-900 border-zinc-800 text-white text-xs">
                <SelectItem value="all">Tất cả loại</SelectItem>
                <SelectItem value="item">Vật tư</SelectItem>
                <SelectItem value="warehouse">Nhà kho</SelectItem>
                <SelectItem value="partner">Đối tác</SelectItem>
                <SelectItem value="uom">Đơn vị tính</SelectItem>
              </SelectContent>
            </Select>
            <Button type="submit" size="sm" className="bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 text-xs">Tìm</Button>
          </form>
          <Button onClick={() => setIsAddOpen(true)} size="sm" className="bg-emerald-600 hover:bg-emerald-500 text-xs">
            Thêm ánh xạ
          </Button>
        </div>
      </div>

      <Card className="bg-zinc-900 border-zinc-800 text-white">
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Bảng cấu hình mapping bí danh</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-6 text-xs text-zinc-400 font-mono">Đang tải danh sách...</div>
          ) : (
            <Table className="text-xs">
              <TableHeader className="border-b border-zinc-800">
                <TableRow>
                  <TableHead className="text-zinc-400">External System</TableHead>
                  <TableHead className="text-zinc-400">Loại Mapping</TableHead>
                  <TableHead className="text-zinc-400">Mã SAP / ERP (External)</TableHead>
                  <TableHead className="text-zinc-400">Mã WMS (Internal)</TableHead>
                  <TableHead className="text-zinc-400">Trạng thái</TableHead>
                  <TableHead className="text-zinc-400">Ngày tạo</TableHead>
                  <TableHead className="text-zinc-400 text-right">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {mappings.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center py-6 text-zinc-500">
                      Chưa cấu hình ánh xạ dữ liệu nào.
                    </TableCell>
                  </TableRow>
                ) : (
                  mappings.map((m) => (
                    <TableRow key={m.id} className="hover:bg-zinc-800/30">
                      <TableCell className="font-semibold">{m.externalSystem}</TableCell>
                      <TableCell>{getTypeLabel(m.mappingType)}</TableCell>
                      <TableCell className="font-mono text-amber-400">{m.externalCode}</TableCell>
                      <TableCell className="font-mono text-emerald-400">{m.internalCode}</TableCell>
                      <TableCell>
                        <Badge variant={m.status === "active" ? "default" : "secondary"} className={m.status === "active" ? "bg-emerald-600/20 text-emerald-400 border border-emerald-500/30" : ""}>
                          {m.status === "active" ? "Hoạt động" : "Tắt"}
                        </Badge>
                      </TableCell>
                      <TableCell>{new Date(m.createdAt).toLocaleDateString("vi-VN")}</TableCell>
                      <TableCell className="text-right flex justify-end gap-2">
                        <Button
                          size="xs"
                          variant="outline"
                          onClick={() => {
                            setIsEditing(m);
                            setEditForm({ internalCode: m.internalCode, status: m.status });
                          }}
                          className="border-zinc-700 text-zinc-300 hover:text-white hover:bg-zinc-800 text-[10px] h-7"
                        >
                          Sửa
                        </Button>
                        <Button
                          size="xs"
                          variant="destructive"
                          onClick={() => handleDelete(m.id)}
                          className="text-[10px] h-7 bg-rose-950/40 text-rose-400 border border-rose-900/50 hover:bg-rose-900/30"
                        >
                          Xóa
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          )}

          <div className="flex justify-between items-center mt-4">
            <div className="text-[10px] text-zinc-500">Tổng cộng: {total} mapping</div>
            <div className="flex gap-2">
              <Button
                size="xs"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="bg-zinc-800 border border-zinc-750 text-white text-[10px] h-7 disabled:opacity-50"
              >
                Trước
              </Button>
              <Button
                size="xs"
                disabled={page * pageSize >= total}
                onClick={() => setPage(page + 1)}
                className="bg-zinc-800 border border-zinc-750 text-white text-[10px] h-7 disabled:opacity-50"
              >
                Sau
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Dialog Add Mapping */}
      <Dialog open={isAddOpen} onOpenChange={setIsAddOpen}>
        <DialogContent className="bg-zinc-950 border-zinc-850 text-white text-xs max-w-md">
          <DialogHeader>
            <DialogTitle className="text-sm font-bold">Thêm ánh xạ ERP mới</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleAdd} className="space-y-4">
            <div className="space-y-2">
              <Label className="text-zinc-400">Hệ thống ngoài</Label>
              <Input
                value={newMapping.externalSystem}
                onChange={(e) => setNewMapping({ ...newMapping, externalSystem: e.target.value })}
                className="bg-zinc-900 border-zinc-800 text-white text-xs h-9"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-zinc-400">Loại Mapping</Label>
              <Select
                value={newMapping.mappingType}
                onValueChange={(val) => setNewMapping({ ...newMapping, mappingType: val })}
              >
                <SelectTrigger className="bg-zinc-900 border-zinc-800 text-white text-xs h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="bg-zinc-900 border-zinc-800 text-white text-xs">
                  <SelectItem value="item">Vật tư (Item)</SelectItem>
                  <SelectItem value="warehouse">Nhà kho (Warehouse)</SelectItem>
                  <SelectItem value="partner">Đối tác (Partner)</SelectItem>
                  <SelectItem value="uom">Đơn vị tính (UOM)</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label className="text-zinc-400">Mã ERP / SAP (External Code)</Label>
              <Input
                placeholder="Ví dụ: SAP-MILK-900"
                value={newMapping.externalCode}
                onChange={(e) => setNewMapping({ ...newMapping, externalCode: e.target.value })}
                className="bg-zinc-900 border-zinc-800 text-white text-xs h-9"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-zinc-400">Mã WMS tương ứng (Internal Code)</Label>
              <Input
                placeholder="Ví dụ: MILK-DRY-900"
                value={newMapping.internalCode}
                onChange={(e) => setNewMapping({ ...newMapping, internalCode: e.target.value })}
                className="bg-zinc-900 border-zinc-800 text-white text-xs h-9"
              />
            </div>
            <DialogFooter className="pt-2">
              <Button type="button" variant="ghost" onClick={() => setIsAddOpen(false)} className="text-xs">Hủy</Button>
              <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500 text-xs">Lưu</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Dialog Edit Mapping */}
      <Dialog open={isEditing !== null} onOpenChange={() => setIsEditing(null)}>
        <DialogContent className="bg-zinc-950 border-zinc-850 text-white text-xs max-w-md">
          <DialogHeader>
            <DialogTitle className="text-sm font-bold">Cập nhật ánh xạ ERP</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleUpdate} className="space-y-4">
            <div className="space-y-2">
              <Label className="text-zinc-400">Mã SAP / ERP (Không thể sửa)</Label>
              <Input
                disabled
                value={isEditing?.externalCode || ""}
                className="bg-zinc-900/50 border-zinc-800 text-zinc-500 text-xs h-9 cursor-not-allowed"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-zinc-400">Mã WMS tương ứng (Internal Code)</Label>
              <Input
                value={editForm.internalCode}
                onChange={(e) => setEditForm({ ...editForm, internalCode: e.target.value })}
                className="bg-zinc-900 border-zinc-800 text-white text-xs h-9"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-zinc-400">Trạng thái</Label>
              <Select
                value={editForm.status}
                onValueChange={(val: "active" | "inactive") => setEditForm({ ...editForm, status: val })}
              >
                <SelectTrigger className="bg-zinc-900 border-zinc-800 text-white text-xs h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="bg-zinc-900 border-zinc-800 text-white text-xs">
                  <SelectItem value="active">Hoạt động (Active)</SelectItem>
                  <SelectItem value="inactive">Tắt (Inactive)</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <DialogFooter className="pt-2">
              <Button type="button" variant="ghost" onClick={() => setIsEditing(null)} className="text-xs">Hủy</Button>
              <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500 text-xs">Lưu thay đổi</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
