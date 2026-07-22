"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { showError, showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import api from "@/lib/api";
import { ArrowLeft, Move, RefreshCw } from "lucide-react";
import Link from "next/link";

interface OfflineMove {
  clientOperationId: string;
  stepType: string;
  payload: string;
}

export default function MovementPage() {
  const t = useTranslations("Mobile.movement");
  const tErrors = useTranslations("Errors");
  const [loading, setLoading] = useState(false);
  const [isOnline, setIsOnline] = useState(() => navigator.onLine);
  const [offlineQueue, setOfflineQueue] = useState<OfflineMove[]>(() => {
    const stored = localStorage.getItem("nexustock_offline_movements");
    return stored ? (JSON.parse(stored) as OfflineMove[]) : [];
  });

  const [fromLoc, setFromLoc] = useState("");
  const [lotNo, setLotNo] = useState("");
  const [toLoc, setToLoc] = useState("");
  const [qty, setQty] = useState("");
  const [currentStep, setCurrentStep] = useState<"SCAN_FROM" | "SCAN_LOT" | "INPUT_QTY" | "SCAN_TO" | "CONFIRM">("SCAN_FROM");

  const showApiErr = (err: unknown, fallback: string) => {
    const { codeLabel, message } = resolveApiError(err, tErrors);
    showApiErrorToast(codeLabel, message || fallback);
  };

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
      showSuccess(t("toast.fromOk"));
      setCurrentStep("SCAN_LOT");
    } catch (err: unknown) {
      showApiErr(err, t("toast.fromBad"));
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
      showSuccess(t("toast.lotOk"));
      setCurrentStep("INPUT_QTY");
    } catch (err: unknown) {
      showApiErr(err, t("toast.lotBad"));
    } finally {
      setLoading(false);
    }
  };

  const handleInputQty = () => {
    const parsed = parseFloat(qty);
    if (isNaN(parsed) || parsed <= 0) {
      showError(t("toast.qtyBad"));
      return;
    }
    setCurrentStep("SCAN_TO");
  };

  const handleScanTo = async (barcode: string) => {
    if (barcode === fromLoc) {
      showError(t("toast.sameLoc"));
      return;
    }
    setLoading(true);
    try {
      if (isOnline) {
        await api.post("/mobile/scan/validate", { barcode, context: "LOCATION" });
      }
      setToLoc(barcode);
      showSuccess(t("toast.toOk"));
      setCurrentStep("CONFIRM");
    } catch (err: unknown) {
      showApiErr(err, t("toast.toBad"));
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmMovement = async () => {
    const parsedQty = parseFloat(qty);
    const payloadData = {
      itemId: "00000000-0000-0000-0000-000000000001",
      lotNo,
      fromLocationCode: fromLoc,
      toLocationCode: toLoc,
      qty: parsedQty,
    };
    const clientOperationId = `OP-MOVE-${crypto.randomUUID()}`;

    if (!isOnline) {
      const newOp: OfflineMove = {
        clientOperationId,
        stepType: "MOVE",
        payload: JSON.stringify(payloadData),
      };
      saveOfflineQueue([...offlineQueue, newOp]);
      showSuccess(t("toast.offlineSaved"));
      resetForm();
      return;
    }

    setLoading(true);
    try {
      await api.post("/mobile/offline-sync", {
        operations: [{ clientOperationId, stepType: "MOVE", payload: JSON.stringify(payloadData) }],
      });
      showSuccess(t("toast.moveOk"));
      resetForm();
    } catch (err: unknown) {
      showApiErr(err, t("toast.moveFailed"));
    } finally {
      setLoading(false);
    }
  };

  const handleSyncOfflineQueue = async () => {
    if (offlineQueue.length === 0) return;
    setLoading(true);
    try {
      await api.post("/mobile/offline-sync", { operations: offlineQueue });
      showSuccess(t("toast.syncOk"));
      saveOfflineQueue([]);
    } catch (err: unknown) {
      showApiErr(err, t("toast.syncFailed"));
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

  const dash = "—";

  return (
    <MobileShell>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              className="text-slate-300"
              render={<Link href="/mobile" />}
              nativeButton={false}
            >
              <ArrowLeft className="h-4 w-4" />
            </Button>
            <h2 className="text-lg font-bold flex items-center gap-2 text-slate-100">
              <Move className="h-5 w-5 text-blue-500" />
              {t("page.title")}
            </h2>
          </div>

          {offlineQueue.length > 0 && isOnline && (
            <Button
              onClick={handleSyncOfflineQueue}
              disabled={loading}
              size="sm"
              variant="outline"
              className="border-yellow-500 text-yellow-500 hover:bg-yellow-500/10 gap-2"
            >
              <RefreshCw className="h-3.5 w-3.5 animate-spin" />
              {t("actions.syncQueue")} ({offlineQueue.length})
            </Button>
          )}
        </div>

        <Card className="border-slate-800 bg-slate-800/40">
          <CardContent className="p-4 space-y-4">
            <div className="space-y-2 text-xs font-mono text-slate-400 border-b border-slate-800/60 pb-3">
              <div>
                {t("labels.fromLoc")}{" "}
                <span className="text-white font-bold">{fromLoc || dash}</span>
              </div>
              <div>
                {t("labels.lotNo")} <span className="text-white font-bold">{lotNo || dash}</span>
              </div>
              <div>
                {t("labels.qty")} <span className="text-white font-bold">{qty || dash}</span>
              </div>
              <div>
                {t("labels.toLoc")} <span className="text-white font-bold">{toLoc || dash}</span>
              </div>
            </div>

            {currentStep === "SCAN_FROM" && (
              <ScanInput
                id="fromLocScan"
                label={t("fields.scanFrom")}
                onScan={handleScanFrom}
                placeholder={t("fields.scanFromPlaceholder")}
              />
            )}

            {currentStep === "SCAN_LOT" && (
              <ScanInput
                id="lotScan"
                label={t("fields.scanLot")}
                onScan={handleScanLot}
                placeholder={t("fields.scanLotPlaceholder")}
              />
            )}

            {currentStep === "INPUT_QTY" && (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="moveQty" className="text-sm font-semibold text-slate-300">
                    {t("fields.qty")}
                  </Label>
                  <Input
                    id="moveQty"
                    type="number"
                    step="any"
                    value={qty}
                    onChange={(e) => setQty(e.target.value)}
                    placeholder={t("fields.qtyPlaceholder")}
                    className="bg-slate-800 border-slate-700 text-white font-mono text-lg"
                  />
                </div>
                <Button onClick={handleInputQty} className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold">
                  {t("actions.next")}
                </Button>
              </div>
            )}

            {currentStep === "SCAN_TO" && (
              <ScanInput
                id="toLocScan"
                label={t("fields.scanTo")}
                onScan={handleScanTo}
                placeholder={t("fields.scanToPlaceholder")}
              />
            )}

            {currentStep === "CONFIRM" && (
              <div className="space-y-4 pt-2">
                <div className="bg-slate-850 p-4 rounded text-sm space-y-2 text-slate-200 border border-slate-700">
                  <div className="text-center font-bold text-base text-blue-400 mb-2">{t("labels.confirmTitle")}</div>
                  <div>
                    {t("labels.fromShelf")} <span className="font-bold text-white font-mono">{fromLoc}</span>
                  </div>
                  <div>
                    {t("labels.toShelf")} <span className="font-bold text-white font-mono">{toLoc}</span>
                  </div>
                  <div>
                    {t("labels.lot")} <span className="font-bold text-white font-mono">{lotNo}</span>
                  </div>
                  <div>
                    {t("labels.quantity")} <span className="font-bold text-emerald-400 font-mono">{qty}</span>
                  </div>
                </div>

                <div className="flex gap-2">
                  <Button onClick={resetForm} variant="outline" className="flex-1 border-slate-700 text-slate-300">
                    {t("actions.reset")}
                  </Button>
                  <Button
                    onClick={handleConfirmMovement}
                    disabled={loading}
                    className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white font-bold"
                  >
                    {loading ? t("actions.processing") : isOnline ? t("actions.confirm") : t("actions.saveOffline")}
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
