"use client";

import { useState } from "react";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import api from "@/lib/api";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, Box, ArrowRight, Layers } from "lucide-react";
import Link from "next/link";

interface Lpn {
  id: string;
  lpnNo: string;
  locationId: string;
  status: string;
  createdAt: string;
}

interface LpnItem {
  id: string;
  itemCode: string;
  itemName: string;
  lotNo: string;
  qtyOnHand: number;
}

interface StorageLocation {
  id: string;
  code: string;
}

export default function MobileLpnPage() {
  const [lpn, setLpn] = useState<Lpn | null>(null);
  const [lpnItems, setLpnItems] = useState<LpnItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [currentStep, setCurrentStep] = useState<"SCAN_LPN" | "SCAN_TARGET_LOC" | "CONFIRM">("SCAN_LPN");

  const [targetLocation, setTargetLocation] = useState<StorageLocation | null>(null);
  const [targetLocationCode, setTargetLocationCode] = useState("");

  const handleScanLpn = async (barcode: string) => {
    setLoading(true);
    try {
      // 1. Tìm LPN theo mã quét
      const lpnsRes = await api.get<Lpn[]>("/lpns");
      const matchedLpn = lpnsRes.data.find(
        (l) => l.lpnNo.toUpperCase() === barcode.toUpperCase()
      );

      if (!matchedLpn) {
        showError(`Không tìm thấy Pallet / LPN có mã: ${barcode}`);
        return;
      }

      setLpn(matchedLpn);

      // 2. Load các items trên LPN
      const itemsRes = await api.get<{ items: LpnItem[] }>("/inventory/balances", {
        params: { lpnId: matchedLpn.id }
      });
      setLpnItems(itemsRes.data?.items || []);

      showSuccess("Quét mã LPN thành công!");
      setCurrentStep("SCAN_TARGET_LOC");
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi kiểm tra mã LPN."));
    } finally {
      setLoading(false);
    }
  };

  const handleScanTargetLocation = async (barcode: string) => {
    setLoading(true);
    try {
      // 1. Kiểm tra vị trí kệ đích có hợp lệ không
      const locsRes = await api.get<{ items: StorageLocation[] }>("/master-data/storage-locations");
      const matchedLoc = (locsRes.data.items || []).find(
        (l) => l.code.toUpperCase() === barcode.toUpperCase()
      );

      if (!matchedLoc) {
        showError(`Không tìm thấy vị trí kệ: ${barcode}`);
        return;
      }

      if (lpn && matchedLoc.id === lpn.locationId) {
        showError("Kệ đích phải khác vị trí hiện tại của LPN.");
        return;
      }

      setTargetLocation(matchedLoc);
      setTargetLocationCode(barcode);
      showSuccess("Quét kệ đích thành công!");
      setCurrentStep("CONFIRM");
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi kiểm tra kệ đích."));
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmMove = async () => {
    if (!lpn || !targetLocation) return;

    setLoading(true);
    try {
      await api.post(`/lpns/${lpn.id}/move`, {
        targetLocationId: targetLocation.id
      });

      showSuccess(`Đã di chuyển pallet ${lpn.lpnNo} thành công!`);
      // Reset
      setLpn(null);
      setLpnItems([]);
      setTargetLocation(null);
      setTargetLocationCode("");
      setCurrentStep("SCAN_LPN");
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi dịch chuyển pallet."));
    } finally {
      setLoading(false);
    }
  };

  return (
    <MobileShell>
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <Link href="/mobile" className="text-slate-300 hover:text-white p-2">
            <ArrowLeft className="h-4 w-4" />
          </Link>
          <h2 className="text-lg font-bold flex items-center gap-2 text-slate-100">
            <Layers className="h-5 w-5 text-emerald-500" />
            Di chuyển Pallet LPN
          </h2>
        </div>

        {currentStep === "SCAN_LPN" && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <Box className="h-12 w-12 text-slate-600 mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-slate-200">Quét Pallet / LPN</h3>
              <p className="text-xs text-slate-400">Vui lòng quét mã barcode LPN để bắt đầu dịch chuyển nguyên khối</p>
            </div>
            <ScanInput id="lpnBarcodeScan" label="Quét mã LPN" onScan={handleScanLpn} placeholder="Quét LPN..." />
          </div>
        )}

        {lpn && (
          <Card className="border-slate-800 bg-slate-800/40">
            <CardHeader className="pb-2 border-b border-slate-800/80">
              <CardTitle className="text-xs font-semibold text-slate-200">Pallet: {lpn.lpnNo}</CardTitle>
            </CardHeader>
            <CardContent className="p-4 space-y-4">
              {/* LPN Items list */}
              <div className="bg-slate-900/60 p-3 rounded text-xs space-y-1.5 border border-slate-800 max-h-[150px] overflow-y-auto">
                <span className="text-[10px] text-slate-500 block border-b border-slate-800 pb-1">Mặt hàng trên pallet:</span>
                {lpnItems.length === 0 ? (
                  <div className="text-slate-400 italic text-center py-2">Pallet trống</div>
                ) : (
                  lpnItems.map((item, idx) => (
                    <div key={idx} className="flex justify-between text-[11px] text-slate-200">
                      <span>{item.itemCode} (Lô: {item.lotNo})</span>
                      <span className="font-bold text-white">{item.qtyOnHand}</span>
                    </div>
                  ))
                )}
              </div>

              {currentStep === "SCAN_TARGET_LOC" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-850 p-3 rounded text-center border border-amber-500/20">
                    <span className="text-xs text-slate-400 block">Bước 2: Quét mã vị trí kệ đích:</span>
                  </div>
                  <ScanInput id="targetLocationScan" label="Quét kệ đích" onScan={handleScanTargetLocation} placeholder="Quét kệ đích..." />
                </div>
              )}

              {currentStep === "CONFIRM" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-850 p-4 rounded text-center border border-emerald-500/20 space-y-2">
                    <span className="text-xs text-slate-400 block">Xác nhận di chuyển pallet:</span>
                    <span className="text-lg font-bold font-mono text-emerald-400 block">{lpn.lpnNo}</span>
                    <div className="flex items-center justify-center gap-3 text-xs text-white pt-1">
                      <span className="font-mono text-zinc-400">Vị trí cũ</span>
                      <ArrowRight className="h-3.5 w-3.5 text-emerald-500" />
                      <span className="font-mono text-emerald-400 font-bold">{targetLocationCode}</span>
                    </div>
                  </div>

                  <Button onClick={handleConfirmMove} disabled={loading} className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold py-4 rounded-lg shadow-lg">
                    {loading ? "Đang xử lý..." : "Xác nhận di chuyển"}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </MobileShell>
  );
}
