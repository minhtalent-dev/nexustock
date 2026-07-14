"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { RefreshCw, Layers, Plus, Trash2, ArrowRight, ClipboardList, CheckCircle, Settings, X, LogIn, LogOut } from "lucide-react";

interface Lpn {
  id: string;
  lpnNo: string;
  locationId: string;
  status: string;
  createdAt: string;
}

interface LpnEvent {
  id: string;
  eventType: string;
  itemId: string | null;
  itemCode: string | null;
  itemName: string | null;
  lotNo: string | null;
  qty: number | null;
  fromLocationCode: string | null;
  toLocationCode: string | null;
  createdAt: string;
  createdBy: string;
}

interface Product {
  id: string;
  code: string;
  name: string;
}

interface StorageLocation {
  id: string;
  code: string;
}

interface InventoryBalance {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  lotNo: string;
  locationId: string;
  locationCode: string;
  qtyOnHand: number;
  qtyReserved: number;
  qtyAvailable: number;
  lpnId: string | null;
}

export default function LpnPage() {
  const [lpns, setLpns] = useState<Lpn[]>([]);
  const [selectedLpn, setSelectedLpn] = useState<Lpn | null>(null);
  const [lpnEvents, setLpnEvents] = useState<LpnEvent[]>([]);
  const [lpnItems, setLpnItems] = useState<InventoryBalance[]>([]);
  
  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);
  
  const [loadingLpns, setLoadingLpns] = useState(false);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [submittingLpn, setSubmittingLpn] = useState(false);

  // Form states
  const [newLpn, setNewLpn] = useState({
    lpnNo: "",
    locationId: ""
  });

  const [attachForm, setAttachForm] = useState({
    itemId: "",
    lotNo: "",
    qty: 0
  });

  const [moveLocationId, setMoveLocationId] = useState("");
  const [showAttachModal, setShowAttachModal] = useState(false);
  const [showMoveModal, setShowMoveModal] = useState(false);

  const fetchLpns = async () => {
    setLoadingLpns(true);
    try {
      const res = await api.get<Lpn[]>("/lpns");
      setLpns(res.data || []);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải danh sách LPN.");
    } finally {
      setLoadingLpns(false);
    }
  };

  const fetchMetadata = async () => {
    try {
      const prodRes = await api.get<Product[]>("/masterdata/products");
      setProducts(prodRes.data || []);
    } catch {
      // Bỏ qua nếu chưa có dữ liệu
    }

    try {
      const locRes = await api.get<StorageLocation[]>("/masterdata/locations");
      setLocations(locRes.data || []);
    } catch {
      // Bỏ qua
    }
  };

  const fetchLpnDetails = async (lpn: Lpn) => {
    setLoadingDetails(true);
    try {
      // 1. Tải sự kiện của LPN
      const eventsRes = await api.get<LpnEvent[]>(`/lpns/${lpn.id}/events`);
      setLpnEvents(eventsRes.data || []);

      // 2. Tải các inventories gắn với LPN này
      const balancesRes = await api.get<{ items: InventoryBalance[] }>("/inventory/balances", {
        params: { lpnId: lpn.id }
      });
      setLpnItems(balancesRes.data?.items || []);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải chi tiết LPN.");
    } finally {
      setLoadingDetails(false);
    }
  };

  useEffect(() => {
    fetchLpns();
    fetchMetadata();
  }, []);

  const handleSelectLpn = (lpn: Lpn) => {
    setSelectedLpn(lpn);
    fetchLpnDetails(lpn);
  };

  const handleCreateLpn = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newLpn.lpnNo || !newLpn.locationId) {
      showError("Vui lòng điền đầy đủ mã LPN và vị trí kệ.");
      return;
    }

    setSubmittingLpn(true);
    try {
      const res = await api.post<Lpn>("/lpns", newLpn);
      showSuccess("Tạo mã LPN thành công.");
      setNewLpn({ lpnNo: "", locationId: "" });
      fetchLpns();
      if (res.data) {
        handleSelectLpn(res.data);
      }
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi tạo LPN.");
    } finally {
      setSubmittingLpn(false);
    }
  };

  const handleAttachItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedLpn) return;
    if (!attachForm.itemId || !attachForm.lotNo || attachForm.qty <= 0) {
      showError("Vui lòng nhập đầy đủ thông tin đóng pallet.");
      return;
    }

    try {
      await api.post(`/lpns/${selectedLpn.id}/attach`, attachForm);
      showSuccess("Đóng hàng vào LPN thành công.");
      setAttachForm({ itemId: "", lotNo: "", qty: 0 });
      setShowAttachModal(false);
      fetchLpnDetails(selectedLpn);
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi đóng hàng.");
    }
  };

  const handleDetachItem = async (item: InventoryBalance, qtyToDetach: number) => {
    if (!selectedLpn) return;
    if (qtyToDetach <= 0 || qtyToDetach > item.qtyOnHand) {
      showError("Số lượng rút hàng không hợp lệ.");
      return;
    }

    if (!confirm(`Bạn có chắc chắn muốn rút ${qtyToDetach} sản phẩm khỏi LPN này?`)) {
      return;
    }

    try {
      await api.post(`/lpns/${selectedLpn.id}/detach`, {
        itemId: item.itemId,
        lotNo: item.lotNo,
        qty: qtyToDetach
      });
      showSuccess("Đã rút hàng khỏi LPN.");
      fetchLpnDetails(selectedLpn);
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi rút hàng.");
    }
  };

  const handleMoveLpn = async () => {
    if (!selectedLpn || !moveLocationId) return;

    try {
      await api.post(`/lpns/${selectedLpn.id}/move`, {
        targetLocationId: moveLocationId
      });
      showSuccess("Dịch chuyển LPN thành công.");
      setShowMoveModal(false);
      setMoveLocationId("");
      
      // Reload LPN lists and details
      const updatedLpn = { ...selectedLpn, locationId: moveLocationId };
      setSelectedLpn(updatedLpn);
      fetchLpns();
      fetchLpnDetails(updatedLpn);
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi dịch chuyển LPN.");
    }
  };

  const getLocationCode = (id: string) => {
    const loc = locations.find((l) => l.id === id);
    return loc ? loc.code : id.substring(0, 8);
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Layers className="h-6 w-6 text-emerald-500" />
            Quản lý Pallet / LPN
          </h1>
          <p className="text-xs text-zinc-400 mt-1">
            Gom nhiều lô hàng hóa khác nhau lên Pallet và thực hiện di chuyển, xuất kho hàng loạt.
          </p>
        </div>
        <div>
          <Button
            onClick={fetchLpns}
            variant="outline"
            className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 px-4 flex items-center gap-2"
          >
            <RefreshCw className={`h-4 w-4 ${loadingLpns ? "animate-spin" : ""}`} />
            Làm mới
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left column: LPN list and form */}
        <div className="lg:col-span-1 flex flex-col gap-6">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader>
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Plus className="h-4 w-4 text-emerald-500" />
                Tạo mới Pallet / LPN
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleCreateLpn} className="flex flex-col gap-4 text-xs">
                <div className="flex flex-col gap-1.5">
                  <label className="text-[10px] text-zinc-500">Mã LPN / Pallet</label>
                  <input
                    type="text"
                    placeholder="Ví dụ: LPN-20260714-001"
                    value={newLpn.lpnNo}
                    onChange={(e) => setNewLpn({ ...newLpn, lpnNo: e.target.value.toUpperCase() })}
                    className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-mono"
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-[10px] text-zinc-500">Vị trí kệ ban đầu</label>
                  <select
                    value={newLpn.locationId}
                    onChange={(e) => setNewLpn({ ...newLpn, locationId: e.target.value })}
                    className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                  >
                    <option value="">-- Chọn vị trí kệ --</option>
                    {locations.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code}
                      </option>
                    ))}
                  </select>
                </div>
                <Button
                  type="submit"
                  disabled={submittingLpn}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white w-full h-9 text-xs rounded"
                >
                  {submittingLpn ? "Đang tạo..." : "Tạo LPN"}
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader>
              <CardTitle className="text-sm font-semibold">
                Danh sách mã Pallet / LPN
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {loadingLpns && lpns.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">Đang tải danh sách LPN...</div>
              ) : lpns.length === 0 ? (
                <div className="text-center py-8 text-zinc-500 text-xs">Không có Pallet nào hoạt động.</div>
              ) : (
                <div className="flex flex-col max-h-[400px] overflow-y-auto">
                  {lpns.map((lpn) => (
                    <button
                      key={lpn.id}
                      onClick={() => handleSelectLpn(lpn)}
                      className={`text-left p-3.5 border-b border-zinc-800 hover:bg-zinc-800/40 transition-all flex items-center justify-between text-xs ${
                        selectedLpn?.id === lpn.id ? "bg-zinc-800/80 border-l-4 border-l-emerald-500" : ""
                      }`}
                    >
                      <div className="flex flex-col gap-1">
                        <span className="font-bold text-zinc-200 font-mono">{lpn.lpnNo}</span>
                        <span className="text-[10px] text-zinc-400">Vị trí: {getLocationCode(lpn.locationId)}</span>
                      </div>
                      <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white text-[9px] scale-90">
                        {lpn.status}
                      </Badge>
                    </button>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Right columns: selected LPN details */}
        <div className="lg:col-span-2 flex flex-col gap-6">
          {selectedLpn ? (
            <>
              <div className="flex items-center justify-between gap-4 bg-zinc-900 p-4 border border-zinc-800 rounded-lg">
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] text-zinc-400 uppercase tracking-wider font-semibold">Đang chọn Pallet</span>
                  <h2 className="text-xl font-bold font-mono text-emerald-400">{selectedLpn.lpnNo}</h2>
                  <span className="text-xs text-zinc-300">Vị trí: <span className="font-mono font-bold text-white">{getLocationCode(selectedLpn.locationId)}</span></span>
                </div>
                <div className="flex items-center gap-3">
                  <Button
                    onClick={() => setShowAttachModal(true)}
                    className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4 flex items-center gap-2"
                  >
                    <LogIn className="h-4 w-4" />
                    Đóng hàng vào pallet
                  </Button>
                  <Button
                    onClick={() => setShowMoveModal(true)}
                    variant="outline"
                    className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 text-xs h-8 px-4 flex items-center gap-2"
                  >
                    <ArrowRight className="h-4 w-4 text-emerald-500" />
                    Dịch chuyển kệ
                  </Button>
                </div>
              </div>

              {/* LPN Items list */}
              <Card className="bg-zinc-900 border-zinc-800 text-white">
                <CardHeader>
                  <CardTitle className="text-sm font-semibold flex items-center gap-2">
                    <ClipboardList className="h-4 w-4 text-emerald-500" />
                    Danh sách hàng hóa trên pallet
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  {loadingDetails ? (
                    <div className="text-center py-8 text-zinc-500 text-xs">Đang tải danh sách hàng hóa...</div>
                  ) : lpnItems.length === 0 ? (
                    <div className="text-center py-8 text-zinc-500 text-xs">Pallet này hiện đang trống hàng.</div>
                  ) : (
                    <Table className="text-xs">
                      <TableHeader className="border-b border-zinc-800">
                        <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                          <TableHead className="text-zinc-400">Sản phẩm</TableHead>
                          <TableHead className="text-zinc-400">Số lô (Lot No)</TableHead>
                          <TableHead className="text-zinc-400 text-right">Tổng tồn kho</TableHead>
                          <TableHead className="text-zinc-400 text-right">Khóa giữ</TableHead>
                          <TableHead className="text-zinc-400 text-right">Khả dụng</TableHead>
                          <TableHead className="text-zinc-400 text-center">Hành động</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {lpnItems.map((item) => (
                          <TableRow key={item.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                            <TableCell className="font-semibold text-zinc-300">{item.itemCode} - {item.itemName}</TableCell>
                            <TableCell className="text-zinc-300 font-mono">{item.lotNo}</TableCell>
                            <TableCell className="text-right font-bold text-white">{item.qtyOnHand}</TableCell>
                            <TableCell className="text-right text-amber-500">{item.qtyReserved}</TableCell>
                            <TableCell className="text-right text-emerald-400 font-bold">{item.qtyAvailable}</TableCell>
                            <TableCell className="text-center">
                              <Button
                                onClick={() => handleDetachItem(item, item.qtyOnHand)}
                                variant="outline"
                                className="border-zinc-800 hover:bg-zinc-800 text-rose-500 h-7 px-3 text-[10px] rounded"
                              >
                                Rút khỏi Pallet
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>

              {/* LPN Events Timeline */}
              <Card className="bg-zinc-900 border-zinc-800 text-white">
                <CardHeader>
                  <CardTitle className="text-sm font-semibold flex items-center gap-2">
                    <Settings className="h-4 w-4 text-emerald-500" />
                    Lịch sử hoạt động của Pallet
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  {loadingDetails ? (
                    <div className="text-center py-8 text-zinc-500 text-xs">Đang tải lịch sử sự kiện...</div>
                  ) : lpnEvents.length === 0 ? (
                    <div className="text-center py-8 text-zinc-500 text-xs">Chưa có sự kiện nào được ghi nhận.</div>
                  ) : (
                    <div className="relative border-l border-zinc-800 pl-4 space-y-4 text-xs ml-2">
                      {lpnEvents.map((evt) => (
                        <div key={evt.id} className="relative">
                          {/* Dot marker */}
                          <div className="absolute -left-[21px] top-1 bg-zinc-900 border border-zinc-700 w-2.5 h-2.5 rounded-full flex items-center justify-center">
                            <div className="w-1 h-1 bg-emerald-500 rounded-full" />
                          </div>
                          <div className="flex flex-col gap-1">
                            <div className="flex items-center gap-2">
                              <Badge className="bg-zinc-800 text-zinc-300 text-[9px] hover:bg-zinc-800 scale-90 px-2 py-0.5">
                                {evt.eventType}
                              </Badge>
                              <span className="text-[10px] text-zinc-500">
                                {new Date(evt.createdAt).toLocaleString()} bởi {evt.createdBy}
                              </span>
                            </div>
                            <p className="text-zinc-300">
                              {evt.eventType === "CREATE" && `Tạo Pallet trống tại vị trí kệ.`}
                              {evt.eventType === "ATTACH" && `Đóng gói ${evt.qty} sản phẩm [${evt.itemCode}] (Lô ${evt.lotNo}) vào pallet.`}
                              {evt.eventType === "DETACH" && `Rút ${evt.qty} sản phẩm [${evt.itemCode}] (Lô ${evt.lotNo}) khỏi pallet.`}
                              {evt.eventType === "MOVE" && `Dịch chuyển nguyên pallet từ kệ [${evt.fromLocationCode}] sang kệ [${evt.toLocationCode}].`}
                            </p>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            </>
          ) : (
            <div className="bg-zinc-900 border border-zinc-800 rounded-lg p-16 text-center text-zinc-500 text-xs flex flex-col items-center justify-center gap-2">
              <Layers className="h-10 w-10 text-zinc-700 animate-pulse" />
              Vui lòng chọn hoặc tạo mới một Pallet / LPN ở danh sách bên trái để quản lý chi tiết.
            </div>
          )}
        </div>
      </div>

      {/* Attach Modal Dialog */}
      {showAttachModal && selectedLpn && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-zinc-900 border border-zinc-800 rounded-lg max-w-sm w-full text-white shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-zinc-800">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <LogIn className="h-4 w-4 text-emerald-500" />
                Đóng hàng vào Pallet {selectedLpn.lpnNo}
              </h3>
              <button
                onClick={() => setShowAttachModal(false)}
                className="text-zinc-500 hover:text-white transition-all"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <form onSubmit={handleAttachItem} className="p-4 flex flex-col gap-4 text-xs">
              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Sản phẩm</label>
                <select
                  value={attachForm.itemId}
                  onChange={(e) => setAttachForm({ ...attachForm, itemId: e.target.value })}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                >
                  <option value="">-- Chọn sản phẩm --</option>
                  {products.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.code} - {p.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Mã số lô (Lot No)</label>
                <input
                  type="text"
                  placeholder="Nhập mã số lô cần đóng..."
                  value={attachForm.lotNo}
                  onChange={(e) => setAttachForm({ ...attachForm, lotNo: e.target.value })}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Số lượng đóng vào LPN</label>
                <input
                  type="number"
                  value={attachForm.qty}
                  onChange={(e) => setAttachForm({ ...attachForm, qty: parseFloat(e.target.value) || 0 })}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-bold"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <Button
                  type="button"
                  onClick={() => setShowAttachModal(false)}
                  variant="outline"
                  className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 text-xs h-8 px-4"
                >
                  Hủy bỏ
                </Button>
                <Button
                  type="submit"
                  className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4"
                >
                  Đóng hàng
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Move LPN Modal Dialog */}
      {showMoveModal && selectedLpn && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-zinc-900 border border-zinc-800 rounded-lg max-w-sm w-full text-white shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-zinc-800">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <ArrowRight className="h-4 w-4 text-emerald-500" />
                Dịch chuyển Pallet {selectedLpn.lpnNo}
              </h3>
              <button
                onClick={() => setShowMoveModal(false)}
                className="text-zinc-500 hover:text-white transition-all"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="p-4 flex flex-col gap-4 text-xs">
              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Vị trí kệ đích</label>
                <select
                  value={moveLocationId}
                  onChange={(e) => setMoveLocationId(e.target.value)}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                >
                  <option value="">-- Chọn vị trí kệ đích --</option>
                  {locations
                    .filter((l) => l.id !== selectedLpn.locationId)
                    .map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.code}
                      </option>
                    ))}
                </select>
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <Button
                  onClick={() => setShowMoveModal(false)}
                  variant="outline"
                  className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 text-xs h-8 px-4"
                >
                  Hủy bỏ
                </Button>
                <Button
                  onClick={handleMoveLpn}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4"
                >
                  Xác nhận
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
