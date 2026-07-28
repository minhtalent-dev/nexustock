"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState, use } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { RefreshCw, ArrowLeft, Play, LayoutGrid, CheckSquare, Layers } from "lucide-react";
import { EntityAttachmentsPanel } from "@/features/files/entity-attachments-panel";

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
  const t = useTranslations("Admin.waves");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [wave, setWave] = useState<WaveDetailResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [processing, setProcessing] = useState(false);

  const fetchWaveDetails = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WaveDetailResponse>(`/waves/${waveId}`);
      setWave(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadDetailFailed"));
    } finally {
      setLoading(false);
    }
  }, [waveId, t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchWaveDetails());
  }, [fetchWaveDetails]);

  const handleReleaseWave = async () => {
    setProcessing(true);
    try {
      await api.post(`/waves/${waveId}/release`);
      showSuccess(t("toastReleaseSuccess"));
      fetchWaveDetails();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.releaseFailed"));
    } finally {
      setProcessing(false);
    }
  };

  const handleCompleteWave = async () => {
    setProcessing(true);
    try {
      await api.post(`/waves/${waveId}/complete`);
      showSuccess(t("toastCompleteSuccess"));
      fetchWaveDetails();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.completeFailed"));
    } finally {
      setProcessing(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "DRAFT":
        return <Badge className="bg-zinc-700 hover:bg-zinc-600 text-foreground">{t("statusDraft")}</Badge>;
      case "RELEASED":
        return <Badge className="bg-blue-600 hover:bg-blue-500 text-white">{t("statusReleased")}</Badge>;
      case "SORTING":
        return <Badge className="bg-amber-600 hover:bg-amber-500 text-white">{t("statusSorting")}</Badge>;
      case "COMPLETED":
        return <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white">{t("statusCompleted")}</Badge>;
      default:
        return <Badge variant="outline">{status}</Badge>;
    }
  };

  if (loading && !wave) {
    return <div className="text-center py-12 text-muted-foreground text-xs font-mono">{t("loading")}</div>;
  }

  if (!wave) {
    return <div className="text-center py-12 text-muted-foreground text-xs">{t("notFound")}</div>;
  }

  return (
    <PageShell className="gap-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Link href="/admin/waves">
            <Button variant="outline" className="border-border hover:bg-muted text-muted-foreground h-9 w-9 p-0">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-3">
              <Layers className="h-6 w-6 text-indigo-400" />
              {t("waveTitle", { waveNo: wave.waveNo })}
            </h1>
            <p className="text-xs text-muted-foreground mt-1">
              {t("createdMeta", {
                by: wave.createdBy,
                at: new Date(wave.createdAt).toLocaleString(),
              })}
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
              {t("releaseWave")}
            </Button>
          )}

          {wave.status === "SORTING" && (
            <Button
              onClick={handleCompleteWave}
              disabled={processing}
              className="bg-emerald-600 hover:bg-emerald-500 text-white flex items-center gap-2 h-9 text-xs px-4"
            >
              <CheckSquare className="h-4 w-4" />
              {t("completeSorting")}
            </Button>
          )}

          {(wave.status === "SORTING" || wave.status === "RELEASED" || wave.status === "COMPLETED") && (
            <Link href={`/admin/waves/${wave.id}/put-wall`}>
              <Button className="bg-amber-600 hover:bg-amber-500 text-white flex items-center gap-2 h-9 text-xs px-4">
                <LayoutGrid className="h-4 w-4" />
                {t("putWallDynamic")}
              </Button>
            </Link>
          )}

          <Button
            onClick={fetchWaveDetails}
            variant="outline"
            className="border-border hover:bg-muted text-muted-foreground h-9 px-4 flex items-center gap-2 text-xs"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            {tc("refresh")}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="bg-card border-border text-foreground">
          <CardHeader className="border-b border-border pb-3">
            <CardTitle className="text-xs font-semibold text-muted-foreground">{t("statusCardTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="pt-4 flex flex-col gap-2">
            <div className="flex justify-between items-center text-xs">
              <span className="text-muted-foreground">{t("waveNoLabel")}</span>
              <span className="font-mono font-bold">{wave.waveNo}</span>
            </div>
            <div className="flex justify-between items-center text-xs">
              <span className="text-muted-foreground">{t("statusLabel")}</span>
              <span>{getStatusBadge(wave.status)}</span>
            </div>
            <div className="flex justify-between items-center text-xs">
              <span className="text-muted-foreground">{t("mergedOrdersLabel")}</span>
              <span className="font-bold">{Array.from(new Set(wave.items.map((i) => i.shipmentId))).length}</span>
            </div>
          </CardContent>
        </Card>
      </div>

      {wave.id && (
        <EntityAttachmentsPanel entityType="WAVE" entityId={wave.id} />
      )}

      <div className="flex flex-col gap-4">
        <h2 className="text-base font-bold text-muted-foreground">{t("pickTasksTitle")}</h2>
        <Card className="bg-card border-border text-foreground">
          <CardContent className="p-0">
            {wave.pickTasks.length === 0 ? (
              <div className="text-center py-8 text-muted-foreground text-xs">{t("noPickTasks")}</div>
            ) : (
              <Table className="text-xs">
                <TableHeader className="border-b border-border">
                  <TableRow className="border-b border-border hover:bg-muted/50">
                    <TableHead className="text-muted-foreground">{t("colItem")}</TableHead>
                    <TableHead className="text-muted-foreground">{t("colItemCode")}</TableHead>
                    <TableHead className="text-muted-foreground">{t("colFromLoc")}</TableHead>
                    <TableHead className="text-muted-foreground text-right">{t("colQtyRequired")}</TableHead>
                    <TableHead className="text-muted-foreground text-right">{t("colQtyPicked")}</TableHead>
                    <TableHead className="text-muted-foreground">{tc("status")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {wave.pickTasks.map((task) => (
                    <TableRow key={task.id} className="border-b border-border/50 hover:bg-muted/20">
                      <TableCell className="text-foreground font-bold">{task.itemName}</TableCell>
                      <TableCell className="text-muted-foreground font-mono">{task.itemCode}</TableCell>
                      <TableCell className="text-indigo-400 font-bold font-mono">{task.locationCode}</TableCell>
                      <TableCell className="text-right text-muted-foreground font-bold">{task.qtyToPick.toLocaleString()}</TableCell>
                      <TableCell className="text-right text-muted-foreground font-bold">{task.qtyPicked.toLocaleString()}</TableCell>
                      <TableCell>
                        <Badge
                          variant="outline"
                          className={task.status === "COMPLETED" ? "border-emerald-800 text-emerald-400" : "border-amber-800 text-amber-400"}
                        >
                          {task.status}
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
        <h2 className="text-base font-bold text-muted-foreground">{t("waveItemsTitle")}</h2>
        <Card className="bg-card border-border text-foreground">
          <CardContent className="p-0">
            <Table className="text-xs">
              <TableHeader className="border-b border-border">
                <TableRow className="border-b border-border hover:bg-muted/50">
                  <TableHead className="text-muted-foreground">{t("colShipment")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colPutWallSlot")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colItem")}</TableHead>
                  <TableHead className="text-muted-foreground text-right">{t("colRequired")}</TableHead>
                  <TableHead className="text-muted-foreground text-right">{t("colAllocated")}</TableHead>
                  <TableHead className="text-muted-foreground text-right">{t("colPicked")}</TableHead>
                  <TableHead className="text-muted-foreground text-right">{t("colSorted")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {wave.items.map((i) => (
                  <TableRow key={i.id} className="border-b border-border/50 hover:bg-muted/20">
                    <TableCell className="font-bold text-foreground font-mono">{i.shipmentNo}</TableCell>
                    <TableCell>
                      {i.recommendedSlotNumber ? (
                        <Badge className="bg-amber-600 text-white font-mono">
                          {t("slotLabel", { number: i.recommendedSlotNumber })}
                        </Badge>
                      ) : (
                        <span className="text-muted-foreground italic">{t("notAssigned")}</span>
                      )}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {i.itemName} <span className="text-muted-foreground text-[10px] font-mono">({i.itemCode})</span>
                    </TableCell>
                    <TableCell className="text-right text-muted-foreground">
                      {i.qtyExpected.toLocaleString()} {i.uomName}
                    </TableCell>
                    <TableCell className="text-right text-muted-foreground">{i.qtyAllocated.toLocaleString()}</TableCell>
                    <TableCell className="text-right text-muted-foreground font-bold">{i.qtyPicked.toLocaleString()}</TableCell>
                    <TableCell className="text-right text-muted-foreground font-bold text-emerald-400">{i.qtySorted.toLocaleString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </PageShell>
  );
}
