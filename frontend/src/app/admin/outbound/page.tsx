"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { CreateShipmentDialog } from "@/features/outbound/components/create-dialog";
import { CompletePickDialog } from "@/features/outbound/components/pick-dialog";
import { CompletePackingDialog } from "@/features/outbound/components/pack-dialog";
import { 
  Truck, Plus, ArrowRightLeft, FileCheck, CheckCircle2,
  RefreshCw, ClipboardCheck, ArrowUpRight
} from "lucide-react";

interface ShipmentItem {
  id: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  uomName: string;
  requestedQty: number;
  pickedQty: number;
  packedQty: number;
}

interface PickTaskDto {
  id: string;
  itemId: string;
  itemName: string;
  lotNo: string;
  fromLocationId: string;
  locationCode: string;
  qty: number;
  pickedQty: number;
  status: string;
}

interface PackingRecordDto {
  id: string;
  packageNo: string;
  weight: number;
  status: string;
  createdAt: string;
  createdBy: string;
}

interface ShipmentDto {
  id: string;
  shipmentNo: string;
  partnerId: string;
  partnerName: string;
  status: string;
  createdAt: string;
  createdBy: string;
}

export default function OutboundPage() {
  const [shipments, setShipments] = useState<ShipmentDto[]>([]);
  const [selectedShipment, setSelectedShipment] = useState<ShipmentDto | null>(null);
  const [shipmentItems, setShipmentItems] = useState<ShipmentItem[]>([]);
  const [pickTasks, setPickTasks] = useState<PickTaskDto[]>([]);
  const [packings, setPackings] = useState<PackingRecordDto[]>([]);

  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);

  // Dialog States
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedPickTask, setSelectedPickTask] = useState<PickTaskDto | null>(null);
  const [isPickOpen, setIsPickOpen] = useState(false);
  const [isPackOpen, setIsPackOpen] = useState(false);

  const fetchShipments = async () => {
    setLoading(true);
    try {
      const res = await api.get<ShipmentDto[]>("/outbound/shipments");
      setShipments(res.data || []);
    } catch (err) {
      showError("Không thể tải danh sách đơn xuất.");
    } finally {
      setLoading(false);
    }
  };

  const fetchShipmentDetails = async (id: string) => {
    setDetailLoading(true);
    try {
      const res = await api.get<{
        shipment: ShipmentDto;
        items: ShipmentItem[];
        picks: PickTaskDto[];
        packings: PackingRecordDto[];
      }>(`/outbound/shipments/${id}`);
      
      setSelectedShipment(res.data.shipment);
      setShipmentItems(res.data.items || []);
      setPickTasks(res.data.picks || []);
      setPackings(res.data.packings || []);
    } catch (err) {
      showError("Không thể tải chi tiết đơn xuất.");
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    fetchShipments();
  }, []);

  const handleSelectShipment = (s: ShipmentDto) => {
    fetchShipmentDetails(s.id);
  };

  const handleGeneratePicks = async (id: string) => {
    try {
      await api.post(`/outbound/shipments/${id}/generate-picks`);
      showSuccess("Đã sinh nhiệm vụ lấy hàng thành công.");
      fetchShipments();
      fetchShipmentDetails(id);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể sinh nhiệm vụ pick.");
    }
  };

  const openPickDialog = (task: PickTaskDto) => {
    setSelectedPickTask(task);
    setIsPickOpen(true);
  };

  const handlePickSuccess = () => {
    if (selectedShipment) {
      fetchShipmentDetails(selectedShipment.id);
      fetchShipments();
    }
  };

  const handlePackSuccess = () => {
    if (selectedShipment) {
      fetchShipmentDetails(selectedShipment.id);
      fetchShipments();
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Open":
        return <span className="rounded-full bg-blue-50 px-2 py-1 text-xs font-semibold text-blue-700">Mới tạo</span>;
      case "Allocated":
        return <span className="rounded-full bg-yellow-50 px-2 py-1 text-xs font-semibold text-yellow-700">Đã phân bổ</span>;
      case "Picking":
        return <span className="rounded-full bg-indigo-50 px-2 py-1 text-xs font-semibold text-indigo-700">Đang lấy hàng</span>;
      case "Packed":
        return <span className="rounded-full bg-green-50 px-2 py-1 text-xs font-semibold text-green-700">Đã đóng gói</span>;
      default:
        return <span className="rounded-full bg-gray-50 px-2 py-1 text-xs font-semibold text-gray-700">{status}</span>;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Truck className="h-6 w-6 text-primary" />
          Đơn xuất kho
        </h1>
        <div className="flex gap-2">
          <Button onClick={() => setIsCreateOpen(true)} className="gap-2">
            <Plus className="h-4 w-4" />
            Tạo đơn xuất
          </Button>
          <Button onClick={fetchShipments} variant="outline" className="gap-2">
            <RefreshCw className="h-4 w-4" />
            Tải lại
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        {/* Danh sách đơn hàng */}
        <Card className="xl:col-span-1">
          <CardHeader>
            <CardTitle>Danh sách đơn xuất</CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-8 text-center text-muted-foreground">Đang tải danh sách đơn xuất...</div>
            ) : shipments.length === 0 ? (
              <div className="py-8 text-center text-muted-foreground">Không có đơn xuất nào.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Mã đơn</TableHead>
                    <TableHead>Khách hàng</TableHead>
                    <TableHead>Trạng thái</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {shipments.map((s) => (
                    <TableRow 
                      key={s.id} 
                      className={`cursor-pointer hover:bg-muted ${selectedShipment?.id === s.id ? "bg-muted" : ""}`}
                      onClick={() => handleSelectShipment(s)}
                    >
                      <TableCell className="font-semibold">{s.shipmentNo}</TableCell>
                      <TableCell className="text-xs">{s.partnerName}</TableCell>
                      <TableCell>{getStatusBadge(s.status)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        {/* Chi tiết đơn hàng */}
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>Chi tiết đơn hàng</CardTitle>
          </CardHeader>
          <CardContent>
            {!selectedShipment ? (
              <div className="py-8 text-center text-muted-foreground">Chọn một đơn xuất để xem chi tiết.</div>
            ) : detailLoading ? (
              <div className="py-8 text-center text-muted-foreground">Đang tải chi tiết đơn hàng...</div>
            ) : (
              <div className="space-y-6">
                <div className="flex items-center justify-between border-b pb-4">
                  <div>
                    <h2 className="text-lg font-bold">{selectedShipment.shipmentNo}</h2>
                    <p className="text-sm text-muted-foreground">Khách hàng: {selectedShipment.partnerName}</p>
                  </div>
                  <div className="flex gap-2">
                    {selectedShipment.status === "Open" && (
                      <Button onClick={() => handleGeneratePicks(selectedShipment.id)} className="gap-2">
                        <ArrowRightLeft className="h-4 w-4" />
                        Sinh nhiệm vụ Pick
                      </Button>
                    )}
                    {(selectedShipment.status === "Picking" || selectedShipment.status === "Allocated") && 
                      pickTasks.length > 0 && pickTasks.every(p => p.status === "Completed") && (
                      <Button onClick={() => setIsPackOpen(true)} className="gap-2 bg-green-600 hover:bg-green-700">
                        <ClipboardCheck className="h-4 w-4" />
                        Hoàn tất đóng gói
                      </Button>
                    )}
                  </div>
                </div>

                {/* Bảng Items */}
                <div>
                  <h3 className="text-sm font-semibold mb-2">Chi tiết hàng hóa</h3>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Vật tư</TableHead>
                        <TableHead>ĐVT</TableHead>
                        <TableHead className="text-right">Yêu cầu</TableHead>
                        <TableHead className="text-right">Đã lấy</TableHead>
                        <TableHead className="text-right">Đóng gói</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {shipmentItems.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell>
                            <div className="font-semibold">{item.itemName}</div>
                            <div className="text-xs text-muted-foreground">{item.itemCode}</div>
                          </TableCell>
                          <TableCell>{item.uomName}</TableCell>
                          <TableCell className="text-right font-semibold">{item.requestedQty}</TableCell>
                          <TableCell className="text-right text-indigo-600 font-semibold">{item.pickedQty}</TableCell>
                          <TableCell className="text-right text-green-600 font-bold">{item.packedQty}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>

                {/* Bảng Pick Tasks */}
                {pickTasks.length > 0 && (
                  <div>
                    <h3 className="text-sm font-semibold mb-2">Nhiệm vụ lấy hàng (Pick Tasks)</h3>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Số lô</TableHead>
                          <TableHead>Vị trí nguồn</TableHead>
                          <TableHead className="text-right">Yêu cầu</TableHead>
                          <TableHead className="text-right">Đã lấy</TableHead>
                          <TableHead>Trạng thái</TableHead>
                          <TableHead className="text-center">Thao tác</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {pickTasks.map((task) => (
                          <TableRow key={task.id}>
                            <TableCell className="font-mono text-xs">{task.lotNo}</TableCell>
                            <TableCell className="font-bold text-amber-600">{task.locationCode}</TableCell>
                            <TableCell className="text-right font-semibold">{task.qty}</TableCell>
                            <TableCell className="text-right font-semibold">{task.pickedQty}</TableCell>
                            <TableCell>
                              {task.status === "Pending" ? (
                                <span className="inline-flex items-center gap-1 rounded-full bg-yellow-50 px-2 py-0.5 text-xs font-semibold text-yellow-700">Chờ lấy</span>
                              ) : (
                                <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-xs font-semibold text-green-700">Hoàn thành</span>
                              )}
                            </TableCell>
                            <TableCell className="text-center">
                              {task.status === "Pending" && (
                                <Button 
                                  size="sm" 
                                  variant="outline" 
                                  onClick={() => openPickDialog(task)}
                                  className="text-xs gap-1"
                                >
                                  <ArrowUpRight className="h-3 w-3" />
                                  Xác nhận Pick
                                </Button>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}

                {/* Bảng Packing Records */}
                {packings.length > 0 && (
                  <div>
                    <h3 className="text-sm font-semibold mb-2">Thông tin kiện đóng gói</h3>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Mã kiện</TableHead>
                          <TableHead className="text-right">Cân nặng (kg)</TableHead>
                          <TableHead>Người đóng</TableHead>
                          <TableHead>Thời gian</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {packings.map((pkg) => (
                          <TableRow key={pkg.id}>
                            <TableCell className="font-semibold text-green-700">{pkg.packageNo}</TableCell>
                            <TableCell className="text-right font-semibold">{pkg.weight}</TableCell>
                            <TableCell>{pkg.createdBy}</TableCell>
                            <TableCell>{new Date(pkg.createdAt).toLocaleString()}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Dialogs */}
      <CreateShipmentDialog
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onSuccess={fetchShipments}
      />

      {selectedPickTask && (
        <CompletePickDialog
          isOpen={isPickOpen}
          onClose={() => setIsPickOpen(false)}
          onSuccess={handlePickSuccess}
          pickTaskId={selectedPickTask.id}
          itemName={selectedPickTask.itemName}
          lotNo={selectedPickTask.lotNo}
          locationCode={selectedPickTask.locationCode}
          allocatedQty={selectedPickTask.qty}
        />
      )}

      {selectedShipment && (
        <CompletePackingDialog
          isOpen={isPackOpen}
          onClose={() => setIsPackOpen(false)}
          onSuccess={handlePackSuccess}
          shipmentId={selectedShipment.id}
          shipmentNo={selectedShipment.shipmentNo}
        />
      )}
    </div>
  );
}
