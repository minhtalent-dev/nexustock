"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { LocalAgentClient, AgentStatusInfo, AgentStatus } from "@/lib/local-agent-client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Monitor, RefreshCw, Key, ShieldAlert, CheckCircle2, AlertCircle, XCircle } from "lucide-react";
import { toast } from "sonner";

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
  const [stations, setStations] = useState<StationResponseDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  // Trạng thái Local Agent cục bộ
  const [agentClient] = useState(() => new LocalAgentClient());
  const [localAgentInfo, setLocalAgentInfo] = useState<AgentStatusInfo>({ status: "connecting" });
  const [isScanning, setIsScanning] = useState(false);

  // Trạng thái Form Ghép cặp
  const [pairingStationCode, setPairingStationCode] = useState("");
  const [pairingCode, setPairingCode] = useState("");
  const [isPairing, setIsPairing] = useState(false);

  // Dialog Tạo Pairing Code
  const [showPairingDialog, setShowPairingDialog] = useState(false);
  const [dialogStationCode, setDialogStationCode] = useState("");
  const [dialogStationName, setDialogStationName] = useState("");
  const [generatedCode, setGeneratedCode] = useState("");
  const [codeExpiresAt, setCodeExpiresAt] = useState<string | null>(null);
  const [isGeneratingCode, setIsGeneratingCode] = useState(false);

  // Dialog Revoke
  const [showRevokeDialog, setShowRevokeDialog] = useState(false);
  const [revokeStationId, setRevokeStationId] = useState<string | null>(null);
  const [revokeReason, setRevokeReason] = useState("DECOMMISSIONED");
  const [revokeDescription, setRevokeDescription] = useState("");
  const [isRevoking, setIsRevoking] = useState(false);

  useEffect(() => {
    fetchStations();
    scanLocalAgent();
  }, [page, search]);

  const fetchStations = async () => {
    setIsLoading(true);
    try {
      const response = await api.get(`/agent/stations?page=${page}&pageSize=${pageSize}&search=${encodeURIComponent(search)}`);
      setStations(response.data.items);
      setTotalCount(response.data.totalCount);
    } catch (err: any) {
      toast.error("Lỗi lấy danh sách trạm: " + (err.response?.data?.message || err.message));
    } finally {
      setIsLoading(false);
    }
  };

  const scanLocalAgent = async () => {
    setIsScanning(true);
    setLocalAgentInfo({ status: "connecting" });
    try {
      const info = await agentClient.scanAgentPort();
      setLocalAgentInfo(info);
      if (info.status === "paired") {
        toast.success(`Đã kết nối Local Agent (Port ${info.port}) - Trạm: ${info.stationCode}`);
      } else if (info.status === "unpaired") {
        toast.warning(`Tìm thấy Local Agent (Port ${info.port}) nhưng chưa được ghép cặp.`);
      }
    } catch (err) {
      setLocalAgentInfo({ status: "port_unavailable", error: "Không tìm thấy Local Agent hoạt động." });
    } finally {
      setIsScanning(false);
    }
  };

  const handlePairAgent = async () => {
    if (!localAgentInfo.port) {
      toast.error("Không tìm thấy cổng hoạt động của Agent để ghép cặp.");
      return;
    }
    if (!pairingStationCode.trim() || !pairingCode.trim()) {
      toast.error("Vui lòng điền mã trạm và mã ghép cặp.");
      return;
    }

    setIsPairing(true);
    try {
      const info = await agentClient.pairAgent(localAgentInfo.port, pairingStationCode.trim(), pairingCode.trim());
      setLocalAgentInfo(info);
      toast.success("Ghép cặp trạm thành công!");
      fetchStations();
      setPairingCode("");
      setPairingStationCode("");
    } catch (err: any) {
      toast.error(err.message || "Lỗi ghép cặp.");
    } finally {
      setIsPairing(false);
    }
  };

  const handleCreatePairingCode = async () => {
    if (!dialogStationCode.trim() || !dialogStationName.trim()) {
      toast.error("Vui lòng nhập đầy đủ mã và tên trạm.");
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
      toast.success("Sinh mã ghép cặp thành công!");
    } catch (err: any) {
      toast.error("Lỗi sinh mã: " + (err.response?.data?.message || err.message));
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
      toast.success("Đã thu hồi quyền trạm làm việc.");
      setShowRevokeDialog(false);
      setRevokeStationId(null);
      setRevokeDescription("");
      fetchStations();
      // Quét lại để cập nhật trạng thái Agent cục bộ nếu chính trạm này bị revoke
      scanLocalAgent();
    } catch (err: any) {
      toast.error("Lỗi thu hồi trạm: " + (err.response?.data?.message || err.message));
    } finally {
      setIsRevoking(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "active":
        return <Badge className="bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Hoạt động</Badge>;
      case "revoked":
        return <Badge className="bg-rose-500/20 text-rose-400 border border-rose-500/30">Đã thu hồi</Badge>;
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
        return <span className="text-emerald-400 font-semibold">Đã ghép cặp thành công</span>;
      case "unpaired":
        return <span className="text-amber-400 font-semibold">Tìm thấy Agent (Chưa ghép cặp)</span>;
      case "certificate_error":
        return <span className="text-rose-400 font-semibold">Lỗi chứng chỉ SSL WSS</span>;
      case "port_unavailable":
        return <span className="text-zinc-400">Không phát hiện Agent cục bộ</span>;
      case "connecting":
      default:
        return <span className="text-indigo-400">Đang quét cổng (9000-9005)...</span>;
    }
  };

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans bg-zinc-950 min-h-screen">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-zinc-800 pb-4">
        <div className="flex items-center gap-3">
          <Monitor className="h-6 w-6 text-indigo-400" />
          <div>
            <h1 className="text-2xl font-bold">Local Agent Foundation</h1>
            <p className="text-zinc-400 text-sm mt-0.5">
              Quản lý trạm làm việc, thiết lập ghép cặp WebSocket bảo mật cục bộ.
            </p>
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
            Tạo Mã Ghép Cặp
          </Button>
          <Button onClick={scanLocalAgent} variant="outline" className="border-zinc-700 hover:bg-zinc-800 text-zinc-300">
            <RefreshCw className={`h-4 w-4 mr-2 ${isScanning ? "animate-spin" : ""}`} />
            Quét Lại Agent
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Side: Local Connection Widget */}
        <div className="lg:col-span-1 flex flex-col gap-6">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-4">
              <CardTitle className="text-lg flex items-center gap-2">
                {getLocalAgentStatusIcon(localAgentInfo.status)}
                Kết Nối Cục Bộ (Localhost)
              </CardTitle>
              <CardDescription className="text-zinc-400 text-xs">
                Kiểm tra phần mềm kết nối cân/in tem nhãn đang chạy tại máy của bạn.
              </CardDescription>
            </CardHeader>
            <CardContent className="pt-4 flex flex-col gap-4">
              <div className="flex justify-between items-center bg-zinc-950 p-3 rounded-lg border border-zinc-800">
                <span className="text-zinc-400 text-sm">Trạng thái:</span>
                <span className="text-sm">{getLocalAgentStatusText(localAgentInfo.status)}</span>
              </div>

              {localAgentInfo.port && (
                <div className="flex justify-between items-center bg-zinc-950 p-3 rounded-lg border border-zinc-800 text-xs">
                  <span className="text-zinc-400">Cổng hoạt động:</span>
                  <code className="text-indigo-300">{localAgentInfo.port}</code>
                </div>
              )}

              {localAgentInfo.stationCode && (
                <div className="flex justify-between items-center bg-zinc-950 p-3 rounded-lg border border-zinc-800 text-xs">
                  <span className="text-zinc-400">Mã trạm:</span>
                  <code className="text-indigo-300">{localAgentInfo.stationCode}</code>
                </div>
              )}

              {localAgentInfo.status === "unpaired" && (
                <div className="flex flex-col gap-3 mt-2 border-t border-zinc-800 pt-4">
                  <span className="text-xs font-semibold text-zinc-300">Ghép cặp Agent cục bộ này:</span>
                  <Input
                    placeholder="Mã trạm (VD: STATION-PACK-01)"
                    value={pairingStationCode}
                    onChange={(e) => setPairingStationCode(e.target.value)}
                    className="bg-zinc-950 border-zinc-800 text-white text-xs placeholder:text-zinc-600 focus:border-indigo-500"
                  />
                  <Input
                    placeholder="Mã ghép cặp 6 số"
                    value={pairingCode}
                    onChange={(e) => setPairingCode(e.target.value)}
                    className="bg-zinc-950 border-zinc-800 text-white text-xs placeholder:text-zinc-600 focus:border-indigo-500"
                  />
                  <Button 
                    onClick={handlePairAgent} 
                    disabled={isPairing} 
                    className="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium w-full mt-1"
                  >
                    {isPairing ? "Đang ghép cặp..." : "Ghép Cặp Thiết Bị"}
                  </Button>
                </div>
              )}

              {localAgentInfo.status === "certificate_error" && (
                <div className="bg-rose-950/20 border border-rose-900/30 p-3 rounded-lg text-xs text-rose-400 flex flex-col gap-2">
                  <p>Trình duyệt đã chặn kết nối WebSocket Secure do thiếu hoặc sai chứng chỉ SSL localhost.</p>
                  <a 
                    href={`https://127.0.0.1:${localAgentInfo.port || 9000}/ws`} 
                    target="_blank" 
                    rel="noreferrer"
                    className="underline hover:text-rose-300 font-medium"
                  >
                    Click vào đây để mở tab xác thực cert cục bộ →
                  </a>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Right Side: Station management list */}
        <div className="lg:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-4 flex flex-row items-center justify-between">
              <div>
                <CardTitle className="text-lg">Danh sách Trạm Làm Việc</CardTitle>
                <CardDescription className="text-zinc-400 text-xs">
                  Danh sách các trạm thủ kho đã được cấp khóa xác thực trên toàn hệ thống.
                </CardDescription>
              </div>
              <div className="w-64">
                <Input
                  placeholder="Tìm kiếm trạm..."
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
                    <TableHead className="text-zinc-400 text-xs font-medium">Mã Trạm</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">Tên Trạm</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">Trạng thái</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">Tên Máy Cục Bộ</TableHead>
                    <TableHead className="text-zinc-400 text-xs font-medium">Heartbeat cuối</TableHead>
                    <TableHead className="text-right text-zinc-400 text-xs font-medium pr-4">Hành động</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {isLoading ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-8 text-zinc-500 text-xs">
                        Đang tải danh sách trạm...
                      </TableCell>
                    </TableRow>
                  ) : stations.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-8 text-zinc-500 text-xs">
                        Không có trạm làm việc nào được ghép cặp.
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
                              Thu hồi
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

      {/* Dialog: Generate Pairing Code */}
      <Dialog open={showPairingDialog} onOpenChange={setShowPairingDialog}>
        <DialogContent className="bg-zinc-900 border-zinc-800 text-white">
          <DialogHeader>
            <DialogTitle>Tạo Mã Ghép Cặp Mới</DialogTitle>
            <DialogDescription className="text-zinc-400 text-xs">
              Mã ghép cặp dùng để bắt tay xác thực giữa Local Agent cục bộ và Cloud API. Mã chỉ có hiệu lực trong 3 phút.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 my-2">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">Mã trạm (Station Code)</label>
              <Input
                placeholder="Ví dụ: STATION-PACK-01"
                value={dialogStationCode}
                onChange={(e) => setDialogStationCode(e.target.value)}
                disabled={!!generatedCode}
                className="bg-zinc-950 border-zinc-800 text-white placeholder:text-zinc-700 focus:border-indigo-500"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">Tên trạm làm việc</label>
              <Input
                placeholder="Ví dụ: Trạm đóng gói số 1 - Kho A"
                value={dialogStationName}
                onChange={(e) => setDialogStationName(e.target.value)}
                disabled={!!generatedCode}
                className="bg-zinc-950 border-zinc-800 text-white placeholder:text-zinc-700 focus:border-indigo-500"
              />
            </div>

            {generatedCode && (
              <div className="bg-zinc-950 border border-zinc-800 p-4 rounded-lg flex flex-col items-center gap-2 mt-2">
                <span className="text-xs text-zinc-400">Mã ghép cặp trạm:</span>
                <span className="text-3xl font-extrabold tracking-widest text-indigo-400">{generatedCode}</span>
                <span className="text-2xs text-rose-400 mt-1">
                  Hiệu lực đến: {codeExpiresAt ? new Date(codeExpiresAt).toLocaleTimeString() : "-"}
                </span>
              </div>
            )}
          </div>

          <DialogFooter>
            {!generatedCode ? (
              <>
                <Button variant="ghost" onClick={() => setShowPairingDialog(false)} className="text-zinc-400">Hủy</Button>
                <Button onClick={handleCreatePairingCode} disabled={isGeneratingCode} className="bg-indigo-600 hover:bg-indigo-500 text-white">
                  {isGeneratingCode ? "Đang tạo..." : "Tạo Mã"}
                </Button>
              </>
            ) : (
              <Button onClick={() => setShowPairingDialog(false)} className="bg-zinc-800 hover:bg-zinc-700 text-white w-full">Đóng</Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Dialog: Revoke Station */}
      <Dialog open={showRevokeDialog} onOpenChange={setShowRevokeDialog}>
        <DialogContent className="bg-zinc-900 border-zinc-800 text-white">
          <DialogHeader>
            <DialogTitle className="text-rose-400">Thu Hồi Quyền Trạm Làm Việc</DialogTitle>
            <DialogDescription className="text-zinc-400 text-xs">
              Hành động này sẽ vô hiệu hóa khóa xác thực (Token) của trạm ngay lập tức. Local Agent tương ứng sẽ bị ngắt kết nối và chuyển về trạng thái chưa ghép cặp. Hành động không thể hoàn tác.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 my-2">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">Lý do thu hồi</label>
              <select
                value={revokeReason}
                onChange={(e) => setRevokeReason(e.target.value)}
                className="bg-zinc-950 border border-zinc-800 text-white rounded px-3 py-2 text-xs focus:border-indigo-500"
              >
                <option value="DECOMMISSIONED">Ngừng hoạt động trạm</option>
                <option value="SECURITY_BREACH">Nghi ngờ lộ Token / Vi phạm bảo mật</option>
                <option value="REPLACED">Thay thế phần cứng máy tính</option>
                <option value="OTHER">Lý do khác</option>
              </select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-400">Mô tả chi tiết</label>
              <Input
                placeholder="Nhập ghi chú chi tiết lý do thu hồi..."
                value={revokeDescription}
                onChange={(e) => setRevokeDescription(e.target.value)}
                className="bg-zinc-950 border-zinc-800 text-white placeholder:text-zinc-700 focus:border-indigo-500"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setShowRevokeDialog(false)} className="text-zinc-400">Hủy</Button>
            <Button onClick={handleRevokeStation} disabled={isRevoking} className="bg-rose-600 hover:bg-rose-500 text-white">
              {isRevoking ? "Đang xử lý..." : "Xác Nhận Thu Hồi"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
