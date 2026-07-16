"use client";

import { useCallback, useEffect, useState, useRef, use } from "react";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, LayoutGrid, ScanBarcode, CheckCircle2 } from "lucide-react";

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

interface WaveDetailResponse {
  id: string;
  waveNo: string;
  status: string;
  createdAt: string;
  items: WaveItemDetail[];
}

interface SortResponse {
  shipmentId: string;
  shipmentNo: string;
  recommendedSlotNumber: number;
  itemName: string;
  itemCode: string;
  qtySorted: number;
  qtyExpected: number;
  isSlotComplete: boolean;
}

interface WindowWithWebkitAudio extends Window {
  webkitAudioContext?: typeof AudioContext;
}

export default function PutWallPage({ params }: { params: Promise<{ id: string }> }) {
  const resolvedParams = use(params);
  const waveId = resolvedParams.id;
  const [wave, setWave] = useState<WaveDetailResponse | null>(null);
  const [barcode, setBarcode] = useState("");
  const [loading, setLoading] = useState(false);
  const [flashingSlot, setFlashingSlot] = useState<number | null>(null);
  const [lastSortedInfo, setLastSortedInfo] = useState<SortResponse | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Tạo âm thanh beep bằng Web Audio API
  const playBeep = useCallback((type: "success" | "complete" | "error") => {
    try {
      const AudioContextClass = window.AudioContext || (window as WindowWithWebkitAudio).webkitAudioContext;
      if (!AudioContextClass) return;
      const audioCtx = new AudioContextClass();
      const oscillator = audioCtx.createOscillator();
      const gainNode = audioCtx.createGain();

      oscillator.connect(gainNode);
      gainNode.connect(audioCtx.destination);

      if (type === "complete") {
        // Âm thanh hoàn thành ô (tone cao hơn, kéo dài)
        oscillator.frequency.setValueAtTime(880, audioCtx.currentTime); // A5
        gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
        oscillator.start();
        oscillator.stop(audioCtx.currentTime + 0.3);
      } else if (type === "success") {
        // Âm thanh quét thành công thường
        oscillator.frequency.setValueAtTime(523.25, audioCtx.currentTime); // C5
        gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
        oscillator.start();
        oscillator.stop(audioCtx.currentTime + 0.15);
      } else {
        // Âm thanh lỗi
        oscillator.type = "sawtooth";
        oscillator.frequency.setValueAtTime(150, audioCtx.currentTime);
        gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
        oscillator.start();
        oscillator.stop(audioCtx.currentTime + 0.4);
      }
    } catch (e) {
      console.warn("Không thể phát âm thanh", e);
    }
  }, []);

  const fetchWaveDetails = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WaveDetailResponse>(`/waves/${waveId}`);
      setWave(res.data);
    } catch {
      showError("Không thể tải chi tiết Put-Wall.");
    } finally {
      setLoading(false);
    }
  }, [waveId]);

  useEffect(() => {
    queueMicrotask(() => void fetchWaveDetails());
    // Tự động focus vào ô quét barcode
    inputRef.current?.focus();
  }, [fetchWaveDetails]);

  const handleSortSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!barcode.trim()) return;

    try {
      const res = await api.post<SortResponse>(`/waves/${waveId}/sort`, {
        barcodeOrSerial: barcode.trim()
      });
      const data = res.data;

      // Trực quan hóa gợi ý Put-Wall bằng flash effect
      setFlashingSlot(data.recommendedSlotNumber);
      setLastSortedInfo(data);
      
      if (data.isSlotComplete) {
        playBeep("complete");
        showSuccess(`Slot ${data.recommendedSlotNumber} hoàn thành!`);
      } else {
        playBeep("success");
      }

      setBarcode("");
      fetchWaveDetails();

      // Tắt flash effect sau 3 giây
      setTimeout(() => {
        setFlashingSlot(null);
      }, 3000);
    } catch (err: unknown) {
      playBeep("error");
      showError(getHttpErrorMessage(err, "Lỗi khi quét phân loại sản phẩm."));
    } finally {
      if (inputRef.current) inputRef.current.focus();
    }
  };

  if (loading && !wave) {
    return <div className="text-center py-12 text-zinc-500 text-xs font-mono">Đang tải...</div>;
  }

  if (!wave) {
    return <div className="text-center py-12 text-zinc-500 text-xs font-mono">Không tìm thấy đợt Wave.</div>;
  }

  // Nhóm wave items theo Shipment
  const shipmentGroup = wave.items.reduce((acc, curr) => {
    if (!acc[curr.shipmentId]) {
      acc[curr.shipmentId] = {
        shipmentNo: curr.shipmentNo,
        slot: curr.recommendedSlotNumber || 0,
        items: []
      };
    }
    acc[curr.shipmentId].items.push(curr);
    return acc;
  }, {} as Record<string, { shipmentNo: string; slot: number; items: WaveItemDetail[] }>);

  // Sắp xếp các ô Put-Wall theo Slot tăng dần
  const slotsList = Object.values(shipmentGroup).sort((a, b) => a.slot - b.slot);

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <Link href={`/admin/waves/${waveId}`}>
            <Button variant="outline" className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 w-9 p-0">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-3">
              <LayoutGrid className="h-6 w-6 text-amber-500" />
              Màn Hình Put-Wall Phân Loại Động
            </h1>
            <p className="text-xs text-zinc-400 mt-1">
              Đợt Wave: {wave.waveNo} | Quét Barcode/Serial sản phẩm để hiển thị ô nhấp nháy.
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Panel quét Barcode/Serial */}
        <div className="lg:col-span-1">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-3">
              <CardTitle className="text-xs font-semibold text-zinc-400 flex items-center gap-2">
                <ScanBarcode className="h-4 w-4 text-indigo-400" />
                Quét phân loại sản phẩm
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4 flex flex-col gap-4">
              <form onSubmit={handleSortSubmit} className="flex flex-col gap-2">
                <label className="text-[10px] text-zinc-500 font-mono">BARCODE / MÃ SERIAL</label>
                <Input
                  ref={inputRef}
                  value={barcode}
                  onChange={(e) => setBarcode(e.target.value)}
                  placeholder="Quét mã vạch sản phẩm..."
                  className="bg-zinc-950 border-zinc-800 text-zinc-200 h-10 text-xs focus-visible:ring-indigo-600 focus-visible:ring-offset-0"
                />
                <Button type="submit" className="bg-indigo-600 hover:bg-indigo-500 text-white h-9 text-xs mt-2">
                  Xác nhận quét
                </Button>
              </form>

              {lastSortedInfo && (
                <div className="border border-indigo-950/60 bg-indigo-950/10 rounded p-3 flex flex-col gap-2 text-xs">
                  <div className="font-bold text-indigo-300">Kết quả quét cuối:</div>
                  <div>Sản phẩm: <span className="text-zinc-200 font-bold">{lastSortedInfo.itemName}</span></div>
                  <div>Gợi ý ô: <Badge className="bg-amber-600 text-white ml-1 font-mono">Slot {lastSortedInfo.recommendedSlotNumber}</Badge></div>
                  <div>Đơn xuất: <span className="font-mono text-zinc-300">{lastSortedInfo.shipmentNo}</span></div>
                  <div className="flex justify-between items-center mt-1 pt-1 border-t border-indigo-950/40">
                    <span>Tiến độ ô:</span>
                    <span className="font-bold text-emerald-400">{lastSortedInfo.qtySorted} / {lastSortedInfo.qtyExpected}</span>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Khung ô Put-Wall động */}
        <div className="lg:col-span-3">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="border-b border-zinc-800 pb-3">
              <CardTitle className="text-xs font-semibold text-zinc-400">Trạng thái kệ phân chia vật lý (Put-Wall Slots)</CardTitle>
            </CardHeader>
            <CardContent className="pt-6">
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
                {slotsList.map((slotObj) => {
                  const sortedCount = slotObj.items.reduce((sum, i) => sum + i.qtySorted, 0);
                  const pickedCount = slotObj.items.reduce((sum, i) => sum + i.qtyPicked, 0);
                  const isComplete = sortedCount > 0 && sortedCount === pickedCount;
                  const isEmpty = sortedCount === 0;

                  // CSS Classes cho các trạng thái ô Put-Wall
                  let slotColorClasses = "bg-zinc-950 border-zinc-800 hover:border-zinc-700 text-zinc-400";
                  if (isComplete) {
                    slotColorClasses = "bg-emerald-950/30 border-emerald-500/50 text-emerald-400 hover:border-emerald-500 shadow-lg shadow-emerald-500/10";
                  } else if (!isEmpty) {
                    slotColorClasses = "bg-blue-950/20 border-blue-500/40 text-blue-400 hover:border-blue-500";
                  }

                  // Flash effect nhấp nháy màu cam nếu là ô gợi ý
                  const isFlashing = flashingSlot === slotObj.slot;
                  if (isFlashing) {
                    slotColorClasses = "bg-amber-500 text-black border-amber-400 font-bold scale-105 transition-transform duration-100 animate-pulse";
                  }

                  return (
                    <div
                      key={slotObj.shipmentNo}
                      className={`border rounded-lg p-4 flex flex-col items-center justify-between gap-3 min-h-[140px] text-center transition-all ${slotColorClasses}`}
                    >
                      <div className="flex flex-col items-center gap-1 w-full">
                        <span className={`text-[10px] uppercase font-mono ${isFlashing ? "text-black" : "text-zinc-500"}`}>
                          Ô phân loại
                        </span>
                        <span className="text-xl font-black font-mono tracking-wider">
                          SLOT {slotObj.slot}
                        </span>
                      </div>

                      <div className="flex flex-col items-center gap-1 w-full">
                        <span className="text-[10px] font-mono tracking-tight opacity-90 truncate max-w-full">
                          {slotObj.shipmentNo}
                        </span>
                        <span className="text-xs font-bold font-mono">
                          {sortedCount} / {pickedCount}
                        </span>
                      </div>

                      <div className="w-full flex justify-center mt-1">
                        {isComplete ? (
                          <span className="flex items-center gap-1 text-[10px] font-semibold text-emerald-400 uppercase tracking-widest bg-emerald-950/80 px-2 py-0.5 rounded border border-emerald-800/40">
                            <CheckCircle2 className="h-3 w-3" /> OK
                          </span>
                        ) : isEmpty ? (
                          <span className="text-[10px] uppercase tracking-widest text-zinc-600 bg-zinc-900/60 px-2 py-0.5 rounded border border-zinc-800/30">
                            Trống
                          </span>
                        ) : (
                          <span className="text-[10px] uppercase tracking-widest text-blue-400 bg-blue-950/80 px-2 py-0.5 rounded border border-blue-900/40 animate-pulse">
                            Đang xếp
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
