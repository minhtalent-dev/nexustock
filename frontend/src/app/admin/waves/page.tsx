"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { RefreshCw, Layers, PlusCircle, ArrowRight } from "lucide-react";
import { Checkbox } from "@/components/ui/checkbox";

interface WaveListResponse {
  id: string;
  waveNo: string;
  status: string;
  createdAt: string;
  createdBy: string;
  itemCount: number;
  totalQty: number;
}

interface ShipmentResponse {
  id: string;
  shipmentNo: string;
  partnerName: string;
  status: string;
  createdAt: string;
}

export default function WavesPage() {
  const t = useTranslations("Admin.waves");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [waves, setWaves] = useState<WaveListResponse[]>([]);
  const [openShipments, setOpenShipments] = useState<ShipmentResponse[]>([]);
  const [selectedShipmentIds, setSelectedShipmentIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingShipments, setLoadingShipments] = useState(false);
  const [creating, setCreating] = useState(false);
  const [showCreateForm, setShowCreateForm] = useState(false);

  const fetchWaves = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WaveListResponse[]>("/waves");
      setWaves(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadListFailed"));
    } finally {
      setLoading(false);
    }
  }, [t, tErrors]);

  const fetchOpenShipments = useCallback(async () => {
    setLoadingShipments(true);
    try {
      const res = await api.get<ShipmentResponse[]>("/outbound/shipments");
      const openOnes = (res.data || []).filter((s) => s.status === "Open" || s.status === "Waving");
      setOpenShipments(openOnes);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadShipmentsFailed"));
    } finally {
      setLoadingShipments(false);
    }
  }, [t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchWaves());
  }, [fetchWaves]);

  const handleToggleCreateForm = () => {
    setShowCreateForm(!showCreateForm);
    if (!showCreateForm) {
      fetchOpenShipments();
      setSelectedShipmentIds([]);
    }
  };

  const handleSelectShipment = (id: string, checked: boolean) => {
    if (checked) {
      setSelectedShipmentIds([...selectedShipmentIds, id]);
    } else {
      setSelectedShipmentIds(selectedShipmentIds.filter((x) => x !== id));
    }
  };

  const handleCreateWave = async () => {
    if (selectedShipmentIds.length === 0) {
      showApiErrorToast("", t("errors.selectShipmentRequired"));
      return;
    }

    setCreating(true);
    try {
      await api.post("/waves", { shipmentIds: selectedShipmentIds });
      showSuccess(t("toastCreateSuccess"));
      setShowCreateForm(false);
      fetchWaves();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setCreating(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "DRAFT":
        return <Badge className="bg-zinc-700 hover:bg-zinc-600 text-zinc-200">{t("statusDraft")}</Badge>;
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

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Layers className="h-6 w-6 text-indigo-400" />
            {t("title")}
          </h1>
          <p className="text-xs text-zinc-400 mt-1">{t("subtitle")}</p>
        </div>
        <div className="flex gap-2">
          <Button
            onClick={handleToggleCreateForm}
            className="bg-indigo-600 hover:bg-indigo-500 text-white flex items-center gap-2 h-9 text-xs px-4"
          >
            <PlusCircle className="h-4 w-4" />
            {showCreateForm ? tc("cancel") : t("createWave")}
          </Button>
          <Button
            onClick={fetchWaves}
            variant="outline"
            className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 px-4 flex items-center gap-2 text-xs"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            {tc("refresh")}
          </Button>
        </div>
      </div>

      {showCreateForm && (
        <Card className="bg-zinc-900 border-indigo-900/40 text-white">
          <CardHeader className="border-b border-zinc-800 pb-3">
            <CardTitle className="text-sm font-semibold text-indigo-300">{t("createStepTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="pt-4 flex flex-col gap-4">
            {loadingShipments ? (
              <div className="text-center py-6 text-zinc-500 text-xs font-mono">{t("loadingShipments")}</div>
            ) : openShipments.length === 0 ? (
              <div className="text-center py-6 text-zinc-500 text-xs">{t("noOpenShipments")}</div>
            ) : (
              <div className="max-h-60 overflow-y-auto border border-zinc-800 rounded">
                <Table className="text-xs">
                  <TableHeader className="bg-zinc-950 border-b border-zinc-800">
                    <TableRow className="hover:bg-transparent">
                      <TableHead className="w-12"></TableHead>
                      <TableHead className="text-zinc-400">{t("colShipmentNo")}</TableHead>
                      <TableHead className="text-zinc-400">{t("colCustomer")}</TableHead>
                      <TableHead className="text-zinc-400">{t("colCreatedAt")}</TableHead>
                      <TableHead className="text-zinc-400">{tc("status")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {openShipments.map((s) => (
                      <TableRow key={s.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/20">
                        <TableCell>
                          <Checkbox
                            checked={selectedShipmentIds.includes(s.id)}
                            onCheckedChange={(checked) => handleSelectShipment(s.id, !!checked)}
                            disabled={s.status === "Waving"}
                          />
                        </TableCell>
                        <TableCell className="font-bold text-zinc-200 font-mono">
                          {s.shipmentNo}
                          {s.status === "Waving" && (
                            <span className="ml-2 text-[10px] text-indigo-400 font-normal italic">{t("inOtherWave")}</span>
                          )}
                        </TableCell>
                        <TableCell className="text-zinc-300">{s.partnerName}</TableCell>
                        <TableCell className="text-zinc-400">{new Date(s.createdAt).toLocaleDateString()}</TableCell>
                        <TableCell>
                          <Badge variant="outline" className={s.status === "Open" ? "border-zinc-700 text-zinc-400" : "border-indigo-800 text-indigo-400"}>
                            {s.status}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
            <div className="flex justify-end gap-2 mt-2">
              <Button
                onClick={() => setShowCreateForm(false)}
                variant="outline"
                className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-8 text-xs px-3"
              >
                {tc("cancel")}
              </Button>
              <Button
                onClick={handleCreateWave}
                disabled={creating || selectedShipmentIds.length === 0}
                className="bg-indigo-600 hover:bg-indigo-500 text-white h-8 text-xs px-4"
              >
                {creating
                  ? tc("processing")
                  : t("createWaveWithCount", { count: selectedShipmentIds.length })}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <Card className="bg-zinc-900 border-zinc-800 text-white">
        <CardContent className="p-0">
          {loading && waves.length === 0 ? (
            <div className="text-center py-12 text-zinc-500 text-xs font-mono">{t("loading")}</div>
          ) : waves.length === 0 ? (
            <div className="text-center py-12 text-zinc-500 text-xs">{t("emptyWaves")}</div>
          ) : (
            <Table className="text-xs">
              <TableHeader className="border-b border-zinc-800">
                <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                  <TableHead className="text-zinc-400">{t("colWaveNo")}</TableHead>
                  <TableHead className="text-zinc-400 text-right">{t("colItemCount")}</TableHead>
                  <TableHead className="text-zinc-400 text-right">{t("colTotalQty")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colCreatedBy")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colCreatedAt")}</TableHead>
                  <TableHead className="text-zinc-400">{tc("status")}</TableHead>
                  <TableHead className="w-20"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {waves.map((w) => (
                  <TableRow key={w.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/20">
                    <TableCell className="font-bold text-indigo-400 font-mono">{w.waveNo}</TableCell>
                    <TableCell className="text-right text-zinc-300 font-bold">{w.itemCount}</TableCell>
                    <TableCell className="text-right text-zinc-300 font-bold">{w.totalQty.toLocaleString()}</TableCell>
                    <TableCell className="text-zinc-400">{w.createdBy}</TableCell>
                    <TableCell className="text-zinc-400">{new Date(w.createdAt).toLocaleString()}</TableCell>
                    <TableCell>{getStatusBadge(w.status)}</TableCell>
                    <TableCell>
                      <Link href={`/admin/waves/${w.id}`}>
                        <Button
                          variant="ghost"
                          className="text-indigo-400 hover:text-indigo-300 hover:bg-zinc-800/80 h-7 w-7 p-0"
                        >
                          <ArrowRight className="h-4 w-4" />
                        </Button>
                      </Link>
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
