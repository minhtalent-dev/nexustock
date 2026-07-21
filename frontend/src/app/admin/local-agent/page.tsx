"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { LocalAgentClient, AgentStatusInfo, AgentStatus } from "@/lib/local-agent-client";
import { resolveApiError } from "@/lib/api-error-i18n";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { showSuccess, showWarning, showApiErrorToast } from "@/lib/toast";
import { Monitor, RefreshCw, Key, ShieldAlert, CheckCircle2, AlertCircle, XCircle } from "lucide-react";

interface StationResponseDto {
  stationId: string;
  stationCode: string;
  name: string;
  status: string;
  machineName?: string;
  lastHeartbeatAt?: string;
  devices: Array<{
    deviceId: string;
    deviceType: string;
    connectionState: string;
    lastHeartbeatAt: string;
    lastErrorMessage?: string;
  }>;
}

export default function LocalAgentAdminPage() {
  const t = useTranslations("Admin.localAgent");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [stations, setStations] = useState<StationResponseDto[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const [agentClient] = useState(() => new LocalAgentClient());
  const [localAgentInfo, setLocalAgentInfo] = useState<AgentStatusInfo>({ status: "connecting" });
  const [isScanning, setIsScanning] = useState(false);

  const [pairingStationCode, setPairingStationCode] = useState("");
  const [pairingCode, setPairingCode] = useState("");
  const [isPairing, setIsPairing] = useState(false);

  const [showPairingDialog, setShowPairingDialog] = useState(false);
  const [dialogStationCode, setDialogStationCode] = useState("");
  const [dialogStationName, setDialogStationName] = useState("");
  const [generatedCode, setGeneratedCode] = useState("");
  const [codeExpiresAt, setCodeExpiresAt] = useState<string | null>(null);
  const [isGeneratingCode, setIsGeneratingCode] = useState(false);

  const [showRevokeDialog, setShowRevokeDialog] = useState(false);
  const [revokeStationId, setRevokeStationId] = useState<string | null>(null);
  const [revokeReason, setRevokeReason] = useState("DECOMMISSIONED");
  const [revokeDescription, setRevokeDescription] = useState("");
  const [isRevoking, setIsRevoking] = useState(false);

  const fetchStations = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await api.get<{ items: StationResponseDto[]; totalCount: number }>(`/agent/stations?page=${page}&pageSize=${pageSize}&search=${encodeURIComponent(search)}`);
      setStations(response.data.items);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, `${t("errors.loadStationsPrefix")} ${message || t("errors.loadStationsFailed")}`);
    } finally {
      setIsLoading(false);
    }
  }, [page, pageSize, search, t, tErrors]);

  const scanLocalAgent = useCallback(async () => {
    setIsScanning(true);
    setLocalAgentInfo({ status: "connecting" });
    try {
      const info = await agentClient.scanAgentPort();
      setLocalAgentInfo(info);
      if (info.status === "paired") {
        showSuccess(t("toastPaired", { port: info.port ?? "", station: info.stationCode ?? "" }));
      } else if (info.status === "unpaired") {
        showWarning(t("toastUnpairedFound", { port: info.port ?? "" }));
      }
    } catch {
      setLocalAgentInfo({ status: "port_unavailable", error: t("errors.agentNotFound") });
    } finally {
      setIsScanning(false);
    }
  }, [agentClient, t]);

  useEffect(() => {
    queueMicrotask(() => {
      void fetchStations();
      void scanLocalAgent();
    });
  }, [fetchStations, scanLocalAgent]);

  const handlePairAgent = async () => {
    if (!localAgentInfo.port) {
      showApiErrorToast(tErrors("codes.UNKNOWN"), t("errors.noPort"));
      return;
    }
    if (!pairingStationCode.trim() || !pairingCode.trim()) {
      showApiErrorToast(tErrors("codes.UNKNOWN"), t("errors.pairFieldsRequired"));
      return;
    }

    setIsPairing(true);
    try {
      const info = await agentClient.pairAgent(localAgentInfo.port, pairingStationCode.trim(), pairingCode.trim());
      setLocalAgentInfo(info);
      showSuccess(t("toastPairSuccess"));
      fetchStations();
      setPairingCode("");
      setPairingStationCode("");
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.pairFailed"));
    } finally {
      setIsPairing(false);
    }
  };

  const handleCreatePairingCode = async () => {
    if (!dialogStationCode.trim() || !dialogStationName.trim()) {
      showApiErrorToast(tErrors("codes.UNKNOWN"), t("errors.stationFieldsRequired"));
      return;
    }

    setIsGeneratingCode(true);
    try {
      const response = await api.post("/agent/stations/pairing-code", {
        stationCode: dialogStationCode.trim(),
        name: dialogStationName.trim()
      });
      setGeneratedCode(response.data.pairingCode);
      setCodeExpiresAt(response.data.expiresAt);
      showSuccess(t("toastCodeGenerated"));
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, `${t("errors.generateCodePrefix")} ${message || t("errors.generateCodeFailed")}`);
    } finally {
      setIsGeneratingCode(false);
    }
  };

  const handleRevokeStation = async () => {
    if (!revokeStationId) return;
    setIsRevoking(true);
    try {
      await api.post(`/agent/stations/${revokeStationId}/revoke`, {
        reasonCode: revokeReason,
        description: revokeDescription
      });
      showSuccess(t("toastRevokeSuccess"));
      setShowRevokeDialog(false);
      setRevokeStationId(null);
      setRevokeDescription("");
      fetchStations();
      scanLocalAgent();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, `${t("errors.revokePrefix")} ${message || t("errors.revokeFailed")}`);
    } finally {
      setIsRevoking(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "active":
        return <Badge className="bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">{t("statusActive")}</Badge>;
      case "revoked":
        return <Badge className="bg-rose-500/20 text-rose-400 border border-rose-500/30">{t("statusRevoked")}</Badge>;
      default:
        return <Badge className="bg-zinc-500/20 text-zinc-400 border border-zinc-500/30">{status}</Badge>;
    }
  };

  const getLocalAgentStatusIcon = (status: AgentStatus) => {
    switch (status) {
      case "paired":
        return <CheckCircle2 className="h-5 w-5 text-emerald-400" />;
      case "unpaired":
        return <AlertCircle className="h-5 w-5 text-amber-400" />;
      case "certificate_error":
        return <ShieldAlert className="h-5 w-5 text-rose-500" />;
      case "port_unavailable":
        return <XCircle className="h-5 w-5 text-zinc-500" />;
      case "connecting":
      default:
        return <RefreshCw className="h-5 w-5 text-indigo-400 animate-spin" />;
    }
  };

  const getLocalAgentStatusText = (status: AgentStatus) => {
    switch (status) {
      case "paired":
        return <span className="text-emerald-400 font-semibold">{t("statusPaired")}</span>;
      case "unpaired":
        return <span className="text-amber-400 font-semibold">{t("statusUnpaired")}</span>;
      case "certificate_error":
        return <span className="text-rose-400 font-semibold">{t("statusCertError")}</span>;
      case "port_unavailable":
        return <span className="text-zinc-400">{t("statusPortUnavailable")}</span>;
      case "connecting":
      default:
        return <span className="text-indigo-400">{t("statusConnecting")}</span>;
    }
  };

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans bg-zinc-950 min-h-screen">
      <div className="flex items-center justify-between border-b border-zinc-800 pb-4">
        <div className="flex items-center gap-3">
          <Monitor className="h-6 w-6 text-indigo-400" />
          <div>
            <h1 className="text-2xl font-bold">{t("title")}</h1>
            <p className="text-zinc-400 text-sm mt-0.5">{t("subtitle")}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button
            onClick={() => {
              setDialogStationCode("");
              setDialogStationName("");
              setGeneratedCode("");
              setCodeExpiresAt(null);
              setShowPairingDialog(true);
            }}
            className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium"
          >
            <Key className="h-4 w-4 mr-2" />
            {t("createPairingCode")}
          </Button>
          <Button onClick={scanLocalAgent} variant="outline" className="border-zinc-700 hover:bg-zinc-800 text-zinc-300">
            <RefreshCw className={`h-4 w-4 mr-2 ${isScanning ? "animate-spin" : ""}`} />
            {t("rescanAgent")}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1 flex flex-col gap-6">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-4">
              <CardTitle className="text-lg flex items-center gap-2">
                {getLocalAgentStatusIcon(localAgentInfo.status)}
                {t("localConnectionTitle")}
              </CardTitle>
              <CardDescription className="text-zinc-400 text-xs">
                {t("localConnectionHint")}
              </CardDescription>
            </CardHeader>
            <CardContent className="pt-4 flex flex-col gap-4">
              <div className="flex justify-between items-center bg-zinc-950 p-3 rounded-lg border border-zinc-800">
                <span className="text-zinc-400 text-sm">{t("statusLabel")}</span>
                <span className="text-sm">{getLocalAgentStatusText(localAgentInfo.status)}</span>
              </div>

              {localAgentInfo.port && (
                <div className="flex justify-between items-center bg-zinc-950 p-3 rounded-lg border border-zinc-800 text-xs">
                  <span className="text-zinc-400">{t("activePort")}</span>
                  <code className="text-indigo-300">{localAgentInfo.port}</code>
                </div>
              )}

              {localAgentInfo.stationCode && (
                <div className="flex justify-between items-center bg-zinc-950 p-3 rounded-lg border border-zinc-800 text-xs">
                  <span className="text-zinc-400">{t("stationCode")}</span>
                  <code className="text-indigo-300">{localAgentInfo.stationCode}</code>
                </div>
              )}

              {localAgentInfo.status === "unpaired" && (
                <div className="flex flex-col gap-3 mt-2 border-t border-zinc-800 pt-4">
                  <span className="text-xs font-semibold text-zinc-300">{t("pairSectionTitle")}</span>
                  <Input
                    placeholder={t("stationCodePlaceholder")}
                    value={pairingStationCode}
                    onChange={(e) => setPairingStationCode(e.target.value)}
                    className="bg-zinc-950 border-zinc-800 text-white text-xs placeholder:text-zinc-600 focus:border-indigo-500"
                  />
                  <Input
                    placeholder={t("pairingCodePlaceholder")}
                    value={pairingCode}
                    onChange={(e) => setPairingCode(e.target.value)}
                    className="bg-zinc-950 border-zinc-800 text-white text-xs placeholder:text-zinc-600 focus:border-indigo-500"
                  />
                  <Button
                    onClick={handlePairAgent}
                    disabled={isPairing}
                    className="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium w-full mt-1"
                  >
                    {isPairing ? t("pairing") : t("pairDevice")}
                  </Button>
                </div>
              )}

              {localAgentInfo.status === "certificate_error" && (
                <div className="bg-rose-950/20 border border-rose-900/30 p-3 rounded-lg text-xs text-rose-400 flex flex-col gap-2">
                  <p>{t("certErrorHint")}</p>
                  <a
                    href={`https://127.0.0.1:${localAgentInfo.port || 9000}/ws`}
                    target="_blank"
                    rel="noreferrer"
                    className="underline hover:text-rose-300 font-medium"
                  >
                    {t("certErrorLink")}
                  </a>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-4 flex flex-row items-center justify-between">
              <div>
                <CardTitle className="text-lg">{t("stationsTitle")}</CardTitle>
                <CardDescription className="text-zinc-400 text-xs">
                  {t("stationsHint")}
                </CardDescription>
              </div>
              <div className="w-64">
                <Input
                  placeholder={t("searchPlaceholder")}
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  className="bg-zinc-950 border-zinc-800 text-white text-xs placeholder:text-zinc-600 focus:border-indigo-500"
                />
              </div>
            </CardHeader>
            <CardContent className="p-0">
              <Table>
                <TableHeader className="bg-zinc-950/50">
                  <TableRow className="border-zinc-800">
                    <TableHead className="text-zinc-400 text-xs font-medium">{t("colStationCode")}</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">{t("colStationName")}</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">{t("colStatus")}</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">{t("colMachineName")}</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">{t("colLastHeartbeat")}</TableHead>
                    <TableHead className="text-right text-zinc-400 text-xs font-medium pr-4">{t("colActions")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {isLoading ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-8 text-zinc-500 text-xs">
                        {t("loadingStations")}
                      </TableCell>
                    </TableRow>
                  ) : stations.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-8 text-zinc-500 text-xs">
                        {t("emptyStations")}
                      </TableCell>
                    </TableRow>
                  ) : (
                    stations.map((st) => (
                      <TableRow key={st.stationId} className="border-zinc-800 hover:bg-zinc-800/30">
                        <TableCell className="font-semibold text-xs text-indigo-300">{st.stationCode}</TableCell>
                        <TableCell className="text-xs">{st.name}</TableCell>
                        <TableCell className="text-xs">{getStatusBadge(st.status)}</TableCell>
                        <TableCell className="text-xs text-zinc-400">{st.machineName || "-"}</TableCell>
                        <TableCell className="text-xs text-zinc-400">
                          {st.lastHeartbeatAt ? new Date(st.lastHeartbeatAt).toLocaleString() : "-"}
                        </TableCell>
                        <TableCell className="text-right pr-4">
                          {st.status === "active" && (
                            <Button
                              onClick={() => {
                                setRevokeStationId(st.stationId);
                                setShowRevokeDialog(true);
                              }}
                              size="sm"
                              variant="ghost"
                              className="text-rose-400 hover:text-rose-300 hover:bg-rose-500/10 text-xs"
                            >
                              {t("revoke")}
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </div>
      </div>

      <Dialog open={showPairingDialog} onOpenChange={setShowPairingDialog}>
        <DialogContent className="bg-zinc-900 border-zinc-800 text-white">
          <DialogHeader>
            <DialogTitle>{t("pairingDialogTitle")}</DialogTitle>
            <DialogDescription className="text-zinc-400 text-xs">
              {t("pairingDialogHint")}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 my-2">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">{t("labelStationCode")}</label>
              <Input
                placeholder={t("stationCodeExample")}
                value={dialogStationCode}
                onChange={(e) => setDialogStationCode(e.target.value)}
                disabled={!!generatedCode}
                className="bg-zinc-950 border-zinc-800 text-white placeholder:text-zinc-700 focus:border-indigo-500"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">{t("labelStationName")}</label>
              <Input
                placeholder={t("stationNameExample")}
                value={dialogStationName}
                onChange={(e) => setDialogStationName(e.target.value)}
                disabled={!!generatedCode}
                className="bg-zinc-950 border-zinc-800 text-white placeholder:text-zinc-700 focus:border-indigo-500"
              />
            </div>

            {generatedCode && (
              <div className="bg-zinc-950 border border-zinc-800 p-4 rounded-lg flex flex-col items-center gap-2 mt-2">
                <span className="text-xs text-zinc-400">{t("generatedCodeLabel")}</span>
                <span className="text-3xl font-extrabold tracking-widest text-indigo-400">{generatedCode}</span>
                <span className="text-2xs text-rose-400 mt-1">
                  {t("expiresAt", { time: codeExpiresAt ? new Date(codeExpiresAt).toLocaleTimeString() : "-" })}
                </span>
              </div>
            )}
          </div>

          <DialogFooter>
            {!generatedCode ? (
              <>
                <Button variant="ghost" onClick={() => setShowPairingDialog(false)} className="text-zinc-400">{tc("cancel")}</Button>
                <Button onClick={handleCreatePairingCode} disabled={isGeneratingCode} className="bg-indigo-600 hover:bg-indigo-500 text-white">
                  {isGeneratingCode ? t("generating") : t("generateCode")}
                </Button>
              </>
            ) : (
              <Button onClick={() => setShowPairingDialog(false)} className="bg-zinc-800 hover:bg-zinc-700 text-white w-full">{tc("close")}</Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={showRevokeDialog} onOpenChange={setShowRevokeDialog}>
        <DialogContent className="bg-zinc-900 border-zinc-800 text-white">
          <DialogHeader>
            <DialogTitle className="text-rose-400">{t("revokeDialogTitle")}</DialogTitle>
            <DialogDescription className="text-zinc-400 text-xs">
              {t("revokeDialogHint")}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 my-2">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">{t("revokeReasonLabel")}</label>
              <select
                value={revokeReason}
                onChange={(e) => setRevokeReason(e.target.value)}
                className="bg-zinc-950 border border-zinc-800 text-white rounded px-3 py-2 text-xs focus:border-indigo-500"
              >
                <option value="DECOMMISSIONED">{t("reasonDecommissioned")}</option>
                <option value="SECURITY_BREACH">{t("reasonSecurityBreach")}</option>
                <option value="REPLACED">{t("reasonReplaced")}</option>
                <option value="OTHER">{t("reasonOther")}</option>
              </select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">{t("revokeDescLabel")}</label>
              <Input
                placeholder={t("revokeDescPlaceholder")}
                value={revokeDescription}
                onChange={(e) => setRevokeDescription(e.target.value)}
                className="bg-zinc-950 border-zinc-800 text-white placeholder:text-zinc-700 focus:border-indigo-500"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setShowRevokeDialog(false)} className="text-zinc-400">{tc("cancel")}</Button>
            <Button onClick={handleRevokeStation} disabled={isRevoking} className="bg-rose-600 hover:bg-rose-500 text-white">
              {isRevoking ? tc("processing") : t("confirmRevoke")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
