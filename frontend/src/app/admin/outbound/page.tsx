"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { CreateShipmentDialog } from "@/features/outbound/components/create-dialog";
import { EntityAttachmentsPanel } from "@/features/files/entity-attachments-panel";
import { CompletePickDialog } from "@/features/outbound/components/pick-dialog";
import { CompletePackingDialog } from "@/features/outbound/components/pack-dialog";
import {
  Truck, Plus, ArrowRightLeft,
  RefreshCw, ClipboardCheck, ArrowUpRight
} from "lucide-react";

import { OpsExportButtons } from "@/components/ops-export-buttons";

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
  const t = useTranslations("Admin.outbound");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [shipments, setShipments] = useState<ShipmentDto[]>([]);
  const [selectedShipment, setSelectedShipment] = useState<ShipmentDto | null>(null);
  const [shipmentItems, setShipmentItems] = useState<ShipmentItem[]>([]);
  const [pickTasks, setPickTasks] = useState<PickTaskDto[]>([]);
  const [packings, setPackings] = useState<PackingRecordDto[]>([]);

  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedPickTask, setSelectedPickTask] = useState<PickTaskDto | null>(null);
  const [isPickOpen, setIsPickOpen] = useState(false);
  const [isPackOpen, setIsPackOpen] = useState(false);

  const fetchShipments = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<ShipmentDto[]>("/outbound/shipments");
      setShipments(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadListFailed"));
    } finally {
      setLoading(false);
    }
  }, [t, tErrors]);

  const fetchShipmentDetails = useCallback(async (id: string) => {
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
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadDetailFailed"));
    } finally {
      setDetailLoading(false);
    }
  }, [t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchShipments());
  }, [fetchShipments]);

  const handleSelectShipment = (s: ShipmentDto) => {
    fetchShipmentDetails(s.id);
  };

  const handleGeneratePicks = async (id: string) => {
    try {
      await api.post(`/outbound/shipments/${id}/generate-picks`);
      showSuccess(t("toastGeneratePicksSuccess"));
      fetchShipments();
      fetchShipmentDetails(id);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.generatePicksFailed"));
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
        return <span className="rounded-full bg-blue-50 px-2 py-1 text-xs font-semibold text-blue-700">{t("statusOpen")}</span>;
      case "Allocated":
        return <span className="rounded-full bg-yellow-50 px-2 py-1 text-xs font-semibold text-yellow-700">{t("statusAllocated")}</span>;
      case "Picking":
        return <span className="rounded-full bg-indigo-50 px-2 py-1 text-xs font-semibold text-indigo-700">{t("statusPicking")}</span>;
      case "Packed":
        return <span className="rounded-full bg-green-50 px-2 py-1 text-xs font-semibold text-green-700">{t("statusPacked")}</span>;
      default:
        return <span className="rounded-full bg-gray-50 px-2 py-1 text-xs font-semibold text-gray-700">{status}</span>;
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Truck className="h-6 w-6 text-primary" />
          {t("title")}
        </h1>
        <div className="flex items-center gap-2">
          <OpsExportButtons type="SHIPMENTS" />
          <Button onClick={() => setIsCreateOpen(true)} className="gap-2">
            <Plus className="h-4 w-4" />
            {t("createShipment")}
          </Button>
          <Button onClick={fetchShipments} variant="outline" className="gap-2">
            <RefreshCw className="h-4 w-4" />
            {tc("refresh")}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        <Card className="xl:col-span-1">
          <CardHeader>
            <CardTitle>{t("listTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-8 text-center text-muted-foreground">{t("loadingList")}</div>
            ) : shipments.length === 0 ? (
              <div className="py-8 text-center text-muted-foreground">{t("emptyList")}</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("colOrderNo")}</TableHead>
                    <TableHead>{t("colCustomer")}</TableHead>
                    <TableHead>{t("colStatus")}</TableHead>
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

        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>{t("detailTitle")}</CardTitle>
          </CardHeader>
          <CardContent>
            {!selectedShipment ? (
              <div className="py-8 text-center text-muted-foreground">{t("selectHint")}</div>
            ) : detailLoading ? (
              <div className="py-8 text-center text-muted-foreground">{t("loadingDetail")}</div>
            ) : (
              <div className="space-y-6">
                <div className="flex items-center justify-between border-b pb-4">
                  <div>
                    <h2 className="text-lg font-bold">{selectedShipment.shipmentNo}</h2>
                    <p className="text-sm text-muted-foreground">
                      {t("customerLabel", { partner: selectedShipment.partnerName })}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    {selectedShipment.status === "Open" && (
                      <Button onClick={() => handleGeneratePicks(selectedShipment.id)} className="gap-2">
                        <ArrowRightLeft className="h-4 w-4" />
                        {t("generatePicks")}
                      </Button>
                    )}
                    {(selectedShipment.status === "Picking" || selectedShipment.status === "Allocated") &&
                      pickTasks.length > 0 && pickTasks.every((p) => p.status === "Completed") && (
                      <Button onClick={() => setIsPackOpen(true)} className="gap-2 bg-green-600 hover:bg-green-700">
                        <ClipboardCheck className="h-4 w-4" />
                        {t("completePacking")}
                      </Button>
                    )}
                  </div>
                </div>

                <div>
                  <h3 className="text-sm font-semibold mb-2">{t("itemsTitle")}</h3>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{t("colItem")}</TableHead>
                        <TableHead>{t("colUom")}</TableHead>
                        <TableHead className="text-right">{t("colRequested")}</TableHead>
                        <TableHead className="text-right">{t("colPicked")}</TableHead>
                        <TableHead className="text-right">{t("colPacked")}</TableHead>
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

                {pickTasks.length > 0 && (
                  <div>
                    <h3 className="text-sm font-semibold mb-2">{t("pickTasksTitle")}</h3>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>{t("colLotNo")}</TableHead>
                          <TableHead>{t("colSourceLocation")}</TableHead>
                          <TableHead className="text-right">{t("colRequested")}</TableHead>
                          <TableHead className="text-right">{t("colPicked")}</TableHead>
                          <TableHead>{t("colStatus")}</TableHead>
                          <TableHead className="text-center">{t("colActions")}</TableHead>
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
                                <span className="inline-flex items-center gap-1 rounded-full bg-yellow-50 px-2 py-0.5 text-xs font-semibold text-yellow-700">
                                  {t("pickStatusPending")}
                                </span>
                              ) : (
                                <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-xs font-semibold text-green-700">
                                  {t("pickStatusCompleted")}
                                </span>
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
                                  {t("confirmPick")}
                                </Button>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}

                {packings.length > 0 && (
                  <div>
                    <h3 className="text-sm font-semibold mb-2">{t("packingsTitle")}</h3>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>{t("colPackageNo")}</TableHead>
                          <TableHead className="text-right">{t("colWeight")}</TableHead>
                          <TableHead>{t("colPackedBy")}</TableHead>
                          <TableHead>{t("colTime")}</TableHead>
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

                <div className="border-t border-border pt-4">
                  <EntityAttachmentsPanel entityType="SHIPMENT" entityId={selectedShipment.id} />
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

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
    </PageShell>
  );
}
