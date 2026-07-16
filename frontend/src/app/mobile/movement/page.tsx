"use client";

import { useEffect, useState } from "react";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { showError, showSuccess } from "@/lib/toast";
import api from "@/lib/api";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, Move, RefreshCw } from "lucide-react";
import Link from "next/link";

interface OfflineMove {
  clientOperationId: string;
  stepType: string;
  payload: string;
}

export default function MovementPage() {
  const [loading, setLoading] = useState(false);
  const [isOnline, setIsOnline] = useState(() => navigator.onLine);
  const [offlineQueue, setOfflineQueue] = useState<OfflineMove[]>(() => {
    const stored = localStorage.getItem("nexustock_offline_movements");
    return stored ? (JSON.parse(stored) as OfflineMove[]) : [];
  });

  // Form State
  const [fromLoc, setFromLoc] = useState("");
  const [lotNo, setLotNo] = useState("");
  const [toLoc, setToLoc] = useState("");
  const [qty, setQty] = useState("");
  
  const [currentStep, setCurrentStep] = useState<"SCAN_FROM" | "SCAN_LOT" | "INPUT_QTY" | "SCAN_TO" | "CONFIRM">("SCAN_FROM");

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  const saveOfflineQueue = (newQueue: OfflineMove[]) => {
    setOfflineQueue(newQueue);
    localStorage.setItem("nexustock_offline_movements", JSON.stringify(newQueue));
  };

  const handleScanFrom = async (barcode: string) => {
    setLoading(true);
    try {
      if (isOnline) {
        await api.post("/mobile/scan/validate", { barcode, context: "LOCATION" });
      }
      setFromLoc(barcode);
      showSuccess("Đã quét vị trí nguồn.");
      setCurrentStep("SCAN_LOT");
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Vị trí không hợp lệ hoặc bị khóa!"));
    } finally {
      setLoading(false);
    }
  };

  const handleScanLot = async (barcode: string) => {
    setLoading(true);
    try {
      if (isOnline) {
        await api.post("/mobile/scan/validate", { barcode, context: "LOT" });
      }
      setLotNo(barcode);
      showSuccess("Đã quét số lô.");
      setCurrentStep("INPUT_QTY");
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lô hàng không tồn tại!"));
    } finally {
      setLoading(false);
    }
  };

  const handleInputQty = () => {
    const parsed = parseFloat(qty);
    if (isNaN(parsed) || parsed <= 0) {
      showError("Số lượng dịch chuyển phải lớn hơn 0");
      return;
    }
    setCurrentStep("SCAN_TO");
  };

  const handleScanTo = async (barcode: string) => {
    if (barcode === fromLoc) {
      showError("Vị trí đích không được trùng vị trí nguồn");
      return;
    }
    setLoading(true);
    try {
      if (isOnline) {
        await api.post("/mobile/scan/validate", { barcode, context: "LOCATION" });
      }
      setToLoc(barcode);
      showSuccess("Đã quét vị trí đích.");
      setCurrentStep("CONFIRM");
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Vị trí đích không hợp lệ!"));
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmMovement = async () => {
    const parsedQty = parseFloat(qty);
    const payloadData = {
      itemId: "00000000-0000-0000-0000-000000000001", // Giả định ID vật tư mẫu cho phase scan core
      lotNo,
      fromLocationCode: fromLoc,
      toLocationCode: toLoc,
      qty: parsedQty
    };

    const clientOperationId = `OP-MOVE-${crypto.randomUUID()}`;

    if (!isOnline) {
      // Lưu offline
      const newOp: OfflineMove = {
        clientOperationId,
        stepType: "MOVE",
        payload: JSON.stringify(payloadData)
      };
      const updatedQueue = [...offlineQueue, newOp];
      saveOfflineQueue(updatedQueue);
      showSuccess("Mất mạng. Đã lưu nhiệm vụ vào hàng đợi ngoại tuyến thành công!");
      resetForm();
      return;
    }

    setLoading(true);
    try {
      // Gọi trực tiếp đồng bộ online
      await api.post("/mobile/offline-sync", {
        operations: [
          {
            clientOperationId,
            stepType: "MOVE",
            payload: JSON.stringify(payloadData)
          }
        ]
      });
      showSuccess("Thực hiện dịch chuyển kho thành công!");
      resetForm();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Dịch chuyển kho thất bại!"));
    } finally {
      setLoading(false);
    }
  };

  const handleSyncOfflineQueue = async () => {
    if (offlineQueue.length === 0) return;
    setLoading(true);
    try {
      await api.post("/mobile/offline-sync", { operations: offlineQueue });
      showSuccess("Đồng bộ toàn bộ dữ liệu offline thành công!");
      saveOfflineQueue([]);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi đồng bộ dữ liệu ngoại tuyến."));
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    setFromLoc("");
    setLotNo("");
    setToLoc("");
    setQty("");
    setCurrentStep("SCAN_FROM");
  };

  return (
    <MobileShell>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon" asChild className="text-slate-300">
              <Link href="/mobile">
                <ArrowLeft className="h-4 w-4" />
              </Link>
            </Button>
            <h2 className="text-lg font-bold flex items-center gap-2 text-slate-100">
              <Move className="h-5 w-5 text-blue-500" />
              Dịch chuyển kho (Movement)
            </h2>
          </div>

          {offlineQueue.length > 0 && isOnline && (
            <Button onClick={handleSyncOfflineQueue} disabled={loading} size="sm" variant="outline" className="border-yellow-500 text-yellow-500 hover:bg-yellow-500/10 gap-2">
              <RefreshCw className="h-3.5 w-3.5 animate-spin" />
              Đồng bộ ({offlineQueue.length})
            </Button>
          )}
        </div>

        <Card className="border-slate-800 bg-slate-800/40">
          <CardContent className="p-4 space-y-4">
            <div className="space-y-2 text-xs font-mono text-slate-400 border-b border-slate-800/60 pb-3">
              <div>Vị trí nguồn: <span className="text-white font-bold">{fromLoc || "—"}</span></div>
              <div>Số lô hàng: <span className="text-white font-bold">{lotNo || "—"}</span></div>
              <div>Số lượng chuyển: <span className="text-white font-bold">{qty || "—"}</span></div>
              <div>Vị trí đích: <span className="text-white font-bold">{toLoc || "—"}</span></div>
            </div>

            {currentStep === "SCAN_FROM" && (
              <ScanInput id="fromLocScan" label="Bước 1: Quét vị trí kệ nguồn" onScan={handleScanFrom} placeholder="Quét vị trí nguồn..." />
            )}

            {currentStep === "SCAN_LOT" && (
              <ScanInput id="lotScan" label="Bước 2: Quét mã số lô sản phẩm" onScan={handleScanLot} placeholder="Quét số lô cần chuyển..." />
            )}

            {currentStep === "INPUT_QTY" && (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="moveQty" className="text-sm font-semibold text-slate-300">Bước 3: Nhập số lượng dịch chuyển</Label>
                  <Input
                    id="moveQty"
                    type="number"
                    step="any"
                    value={qty}
                    onChange={(e) => setQty(e.target.value)}
                    placeholder="Nhập số lượng..."
                    className="bg-slate-800 border-slate-700 text-white font-mono text-lg"
                  />
                </div>
                <Button onClick={handleInputQty} className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold">
                  Tiếp theo
                </Button>
              </div>
            )}

            {currentStep === "SCAN_TO" && (
              <ScanInput id="toLocScan" label="Bước 4: Quét vị trí kệ đích" onScan={handleScanTo} placeholder="Quét vị trí đích..." />
            )}

            {currentStep === "CONFIRM" && (
              <div className="space-y-4 pt-2">
                <div className="bg-slate-850 p-4 rounded text-sm space-y-2 text-slate-200 border border-slate-700">
                  <div className="text-center font-bold text-base text-blue-400 mb-2">Xác nhận dịch chuyển</div>
                  <div>Từ kệ: <span className="font-bold text-white font-mono">{fromLoc}</span></div>
                  <div>Đến kệ: <span className="font-bold text-white font-mono">{toLoc}</span></div>
                  <div>Lô hàng: <span className="font-bold text-white font-mono">{lotNo}</span></div>
                  <div>Số lượng: <span className="font-bold text-emerald-400 font-mono">{qty}</span></div>
                </div>

                <div className="flex gap-2">
                  <Button onClick={resetForm} variant="outline" className="flex-1 border-slate-700 text-slate-300">
                    Làm lại
                  </Button>
                  <Button onClick={handleConfirmMovement} disabled={loading} className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white font-bold">
                    {loading ? "Đang xử lý..." : isOnline ? "Xác nhận chuyển" : "Lưu offline"}
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </MobileShell>
  );
}
