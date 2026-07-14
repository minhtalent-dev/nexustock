"use client";

import { useState } from "react";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import api from "@/lib/api";
import { ArrowLeft, Box, ClipboardCheck, ArrowRight, RefreshCw } from "lucide-react";
import Link from "next/link";

interface MobileTask {
  id: string;
  referenceType: string;
  referenceId: string;
  step: string;
  locationId: string;
  assignedUser: string;
  status: string;
}

interface ReplenishmentTaskDetail {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  sourceLocationId: string;
  sourceLocationCode: string;
  targetLocationId: string;
  targetLocationCode: string;
  lotNo: string;
  requestedQty: number;
}

export default function MobileReplenishmentPage() {
  const [mobileTask, setMobileTask] = useState<MobileTask | null>(null);
  const [taskDetail, setTaskDetail] = useState<ReplenishmentTaskDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentStep, setCurrentStep] = useState<"CLAIM" | "SCAN_SOURCE_LOC" | "SCAN_LOT" | "SCAN_TARGET_LOC" | "INPUT_QTY">("CLAIM");

  const [userLocation, setUserLocation] = useState("");
  const [actualQty, setActualQty] = useState<number>(0);
  const [operatorName, setOperatorName] = useState("");

  const handleClaimNextTask = async () => {
    setLoading(true);
    try {
      // 1. Claim next task từ Mobile task pool
      const res = await api.get<{ task: MobileTask; message: string }>("/mobile/tasks/next", {
        params: { currentLocationCode: userLocation }
      });

      if (res.data.task) {
        const claimedTask = res.data.task;

        if (claimedTask.referenceType !== "REPLENISHMENT") {
          // Chỉ nhận nhiệm vụ Replenishment
          showError("Nhiệm vụ nhận được không phải là Bổ sung hàng. Vui lòng thử lại.");
          await api.post(`/mobile/tasks/${claimedTask.id}/complete`); // Giải phóng hoặc trả lại
          return;
        }

        setMobileTask(claimedTask);

        // 2. Fetch chi tiết ReplenishmentTask tương ứng
        // Vì API GET /api/replenishment/tasks trả về tất cả, ta lọc theo ID
        const tasksRes = await api.get<any[]>("/replenishment/tasks");
        const detail = tasksRes.data.find((t) => t.id === claimedTask.referenceId);

        if (detail) {
          // Load location codes để hiển thị trực quan
          const locsRes = await api.get<any[]>("/masterdata/locations");
          const sourceLoc = locsRes.data.find((l) => l.id === detail.sourceLocationId);
          const targetLoc = locsRes.data.find((l) => l.id === detail.targetLocationId);

          // Load sản phẩm
          const prodsRes = await api.get<any[]>("/masterdata/products");
          const prod = prodsRes.data.find((p) => p.id === detail.itemId);

          setTaskDetail({
            id: detail.id,
            itemId: detail.itemId,
            itemCode: prod?.code || "",
            itemName: prod?.name || "",
            sourceLocationId: detail.sourceLocationId,
            sourceLocationCode: sourceLoc?.code || detail.sourceLocationId.substring(0, 8),
            targetLocationId: detail.targetLocationId,
            targetLocationCode: targetLoc?.code || detail.targetLocationId.substring(0, 8),
            lotNo: detail.lotNo,
            requestedQty: detail.requestedQty
          });

          setActualQty(detail.requestedQty);
          setCurrentStep("SCAN_SOURCE_LOC");
          showSuccess("Đã nhận việc bổ sung hàng thành công!");
        } else {
          showError("Không tìm thấy chi tiết nhiệm vụ bổ sung.");
          setMobileTask(null);
        }
      } else {
        showError(res.data.message || "Không còn nhiệm vụ bổ sung nào sẵn sàng.");
      }
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể lấy nhiệm vụ mới.");
    } finally {
      setLoading(false);
    }
  };

  const handleScanSourceLocation = async (barcode: string) => {
    if (!taskDetail) return;
    if (barcode.toUpperCase() !== taskDetail.sourceLocationCode.toUpperCase()) {
      showError(`Mã vị trí không khớp! Vui lòng quét đúng kệ nguồn: ${taskDetail.sourceLocationCode}`);
      return;
    }

    setLoading(true);
    try {
      await api.post("/mobile/scan/validate", { barcode, context: "LOCATION" });
      setUserLocation(barcode);
      showSuccess("Đã xác nhận kệ nguồn thành công!");
      setCurrentStep("SCAN_LOT");
    } catch (err: any) {
      showError(err.response?.data?.message || "Vị trí kệ nguồn không hợp lệ!");
    } finally {
      setLoading(false);
    }
  };

  const handleScanLot = async (barcode: string) => {
    if (!taskDetail) return;
    if (barcode !== taskDetail.lotNo) {
      showError(`Mã số lô sản phẩm không khớp! Vui lòng quét đúng số lô: ${taskDetail.lotNo}`);
      return;
    }

    setLoading(true);
    try {
      await api.post("/mobile/scan/validate", { barcode, context: "LOT" });
      showSuccess("Đã xác nhận số lô sản phẩm thành công!");
      setCurrentStep("SCAN_TARGET_LOC");
    } catch (err: any) {
      showError(err.response?.data?.message || "Mã số lô sản phẩm không hợp lệ!");
    } finally {
      setLoading(false);
    }
  };

  const handleScanTargetLocation = async (barcode: string) => {
    if (!taskDetail) return;
    if (barcode.toUpperCase() !== taskDetail.targetLocationCode.toUpperCase()) {
      showError(`Mã vị trí không khớp! Vui lòng quét đúng kệ đích: ${taskDetail.targetLocationCode}`);
      return;
    }

    setLoading(true);
    try {
      await api.post("/mobile/scan/validate", { barcode, context: "LOCATION" });
      showSuccess("Đã xác nhận kệ đích Pick Face thành công!");
      setCurrentStep("INPUT_QTY");
    } catch (err: any) {
      showError(err.response?.data?.message || "Vị trí kệ đích không hợp lệ!");
    } finally {
      setLoading(false);
    }
  };

  const handleCompleteTask = async () => {
    if (!mobileTask || !taskDetail) return;
    if (actualQty < 0) {
      showError("Số lượng thực tế phải lớn hơn hoặc bằng 0.");
      return;
    }

    setLoading(true);
    try {
      const payload = {
        actualQty,
        operatorName: operatorName || "Handheld User"
      };
      // Gọi qua API Complete của Replenishment Controller để chạy toàn bộ logic nghiệp vụ
      await api.post(`/replenishment/tasks/${taskDetail.id}/complete`, payload);

      showSuccess("Hoàn thành nhiệm vụ bổ sung hàng thành công!");
      setMobileTask(null);
      setTaskDetail(null);
      setCurrentStep("CLAIM");
    } catch (err: any) {
      showError(err.response?.data?.message || "Gặp lỗi khi hoàn thành nhiệm vụ bổ sung.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <MobileShell>
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" asChild className="text-slate-300">
            <Link href="/mobile">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <h2 className="text-lg font-bold flex items-center gap-2 text-slate-100">
            <RefreshCw className="h-5 w-5 text-emerald-500" />
            Bổ sung Pick Face
          </h2>
        </div>

        {currentStep === "CLAIM" && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <Box className="h-12 w-12 text-slate-600 mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-slate-200">Sẵn sàng nhận nhiệm vụ</h3>
              <p className="text-xs text-slate-400">Hệ thống sẽ giao nhiệm vụ bổ sung kệ Pick Face thiếu hụt gần vị trí của bạn nhất</p>
            </div>

            <div className="space-y-2">
              <label htmlFor="userLoc" className="text-xs text-slate-400 block">Khai báo vị trí hiện tại (Tùy chọn):</label>
              <input
                id="userLoc"
                type="text"
                value={userLocation}
                onChange={(e) => setUserLocation(e.target.value.toUpperCase())}
                placeholder="Ví dụ: LOC-A-01"
                className="w-full bg-slate-800 border border-slate-700 rounded p-2 text-white font-mono text-sm text-center"
              />
            </div>

            <Button onClick={handleClaimNextTask} disabled={loading} className="w-full bg-emerald-600 hover:bg-emerald-700 text-white py-6 text-base font-bold rounded-lg shadow-lg gap-2">
              <ArrowRight className="h-5 w-5" />
              {loading ? "Đang nhận việc..." : "Nhận việc tiếp theo"}
            </Button>
          </div>
        )}

        {mobileTask && taskDetail && (
          <Card className="border-slate-800 bg-slate-800/40">
            <CardHeader className="pb-2 border-b border-slate-800/80">
              <CardTitle className="text-xs font-semibold text-slate-200">Chi tiết nhiệm vụ bổ sung</CardTitle>
            </CardHeader>
            <CardContent className="p-4 space-y-4">
              <div className="bg-slate-900/60 p-3 rounded text-xs space-y-1.5 border border-slate-800">
                <div>Sản phẩm: <span className="text-white font-bold">{taskDetail.itemCode} - {taskDetail.itemName}</span></div>
                <div>Số lô (Lot): <span className="text-zinc-200 font-bold">{taskDetail.lotNo}</span></div>
                <div className="grid grid-cols-2 gap-2 mt-1 pt-1.5 border-t border-slate-800/60">
                  <div>Kệ Bulk nguồn: <span className="text-amber-500 font-bold font-mono block text-sm">{taskDetail.sourceLocationCode}</span></div>
                  <div>Kệ Pick đích: <span className="text-emerald-500 font-bold font-mono block text-sm">{taskDetail.targetLocationCode}</span></div>
                </div>
              </div>

              {currentStep === "SCAN_SOURCE_LOC" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-850 p-3 rounded text-center border border-amber-500/20">
                    <span className="text-xs text-slate-400 block">Bước 1: Di chuyển đến kệ Bulk nguồn và quét mã:</span>
                    <span className="text-lg font-bold font-mono text-amber-500">{taskDetail.sourceLocationCode}</span>
                  </div>
                  <ScanInput id="sourceLocScan" label="Quét mã vị trí kệ nguồn" onScan={handleScanSourceLocation} placeholder="Quét kệ nguồn..." />
                </div>
              )}

              {currentStep === "SCAN_LOT" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-850 p-3 rounded text-center border border-amber-500/20">
                    <span className="text-xs text-slate-400 block">Bước 2: Quét mã số lô sản phẩm cần lấy:</span>
                    <span className="text-base font-bold font-mono text-white">{taskDetail.lotNo}</span>
                  </div>
                  <ScanInput id="lotScan" label="Quét mã vạch số lô" onScan={handleScanLot} placeholder="Quét số lô sản phẩm..." />
                </div>
              )}

              {currentStep === "SCAN_TARGET_LOC" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-850 p-3 rounded text-center border border-emerald-500/20">
                    <span className="text-xs text-slate-400 block">Bước 3: Mang hàng đến kệ Pick Face đích và quét mã:</span>
                    <span className="text-lg font-bold font-mono text-emerald-500">{taskDetail.targetLocationCode}</span>
                  </div>
                  <ScanInput id="targetLocScan" label="Quét mã vị trí kệ đích" onScan={handleScanTargetLocation} placeholder="Quét kệ đích..." />
                </div>
              )}

              {currentStep === "INPUT_QTY" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-850 p-3 rounded text-center border border-emerald-500/20">
                    <span className="text-xs text-slate-400 block">Bước 4: Nhập số lượng thực tế dịch chuyển:</span>
                    <span className="text-2xl font-bold font-mono text-emerald-400">{taskDetail.requestedQty} sản phẩm</span>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs text-slate-400 block">Số lượng thực tế dịch chuyển:</label>
                    <input
                      type="number"
                      value={actualQty}
                      onChange={(e) => setActualQty(parseFloat(e.target.value) || 0)}
                      className="w-full bg-slate-800 border border-slate-700 rounded p-2 text-white font-mono text-base text-center font-bold"
                    />
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs text-slate-400 block">Tên người thực hiện:</label>
                    <input
                      type="text"
                      placeholder="Nhập tên của bạn..."
                      value={operatorName}
                      onChange={(e) => setOperatorName(e.target.value)}
                      className="w-full bg-slate-800 border border-slate-700 rounded p-2 text-white text-sm"
                    />
                  </div>

                  <Button onClick={handleCompleteTask} disabled={loading} className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold py-4 rounded-lg shadow-lg">
                    Xác nhận hoàn thành bổ sung
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
