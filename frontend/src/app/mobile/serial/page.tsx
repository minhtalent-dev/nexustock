"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import api from "@/lib/api";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, Box, CheckCircle, Smartphone, MapPin } from "lucide-react";

interface Product {
  id: string;
  code: string;
  name: string;
  isSerialTracked: boolean;
}

interface StorageLocation {
  id: string;
  code: string;
}

export default function MobileSerialPage() {
  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);
  
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [selectedLocation, setSelectedLocation] = useState<StorageLocation | null>(null);
  const [scannedSerials, setScannedSerials] = useState<string[]>([]);
  
  const [currentStep, setCurrentStep] = useState<"SCAN_PRODUCT" | "SCAN_LOCATION" | "SCAN_SERIAL">("SCAN_PRODUCT");

  const fetchMetadata = useCallback(async () => {
    try {
      const prodRes = await api.get<{ items: Product[] }>("/master-data/products");
      setProducts(prodRes.data.items || []);
      
      const locRes = await api.get<{ items: StorageLocation[] }>("/master-data/storage-locations");
      setLocations(locRes.data.items || []);
    } catch {
      showError("Không thể tải metadata từ hệ thống.");
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => void fetchMetadata());
  }, [fetchMetadata]);

  const handleScanProduct = (barcode: string) => {
    const matched = products.find(p => p.code.toUpperCase() === barcode.toUpperCase());
    if (!matched) {
      showError(`Không tìm thấy sản phẩm có mã: ${barcode}`);
      return;
    }
    if (!matched.isSerialTracked) {
      showError(`Sản phẩm [${matched.code}] không cấu hình quản lý Serial.`);
      return;
    }
    setSelectedProduct(matched);
    showSuccess(`Chọn sản phẩm: ${matched.code}`);
    setCurrentStep("SCAN_LOCATION");
  };

  const handleScanLocation = (barcode: string) => {
    const matched = locations.find(l => l.code.toUpperCase() === barcode.toUpperCase());
    if (!matched) {
      showError(`Không tìm thấy kệ có mã: ${barcode}`);
      return;
    }
    setSelectedLocation(matched);
    showSuccess(`Chọn kệ cất: ${matched.code}`);
    setCurrentStep("SCAN_SERIAL");
  };

  const handleScanSerial = async (serialNo: string) => {
    if (!selectedProduct || !selectedLocation) return;
    
    if (scannedSerials.includes(serialNo)) {
      showError("Mã serial này đã được quét ở lượt này.");
      return;
    }

    try {
      await api.post("/serials/receive", {
        itemId: selectedProduct.id,
        locationId: selectedLocation.id,
        serialNo: serialNo
      });
      setScannedSerials(prev => [serialNo, ...prev]);
      showSuccess(`Đã nhận thành công mã serial: ${serialNo}`);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi khi nhận mã serial."));
    }
  };

  const handleReset = () => {
    setSelectedProduct(null);
    setSelectedLocation(null);
    setScannedSerials([]);
    setCurrentStep("SCAN_PRODUCT");
  };

  return (
    <MobileShell>
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <Link href="/mobile" className="text-slate-300 hover:text-white p-2">
            <ArrowLeft className="h-4 w-4" />
          </Link>
          <h2 className="text-lg font-bold flex items-center gap-2 text-slate-100">
            <Smartphone className="h-5 w-5 text-emerald-500" />
            Nhận mã Serial
          </h2>
        </div>

        {currentStep === "SCAN_PRODUCT" && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <Box className="h-12 w-12 text-slate-600 mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-slate-200">Quét mã sản phẩm</h3>
              <p className="text-xs text-slate-400">Vui lòng quét mã barcode sản phẩm để bắt đầu nhận serial</p>
            </div>
            <ScanInput id="productBarcodeScan" label="Quét sản phẩm" onScan={handleScanProduct} placeholder="Quét sản phẩm..." />
          </div>
        )}

        {currentStep === "SCAN_LOCATION" && selectedProduct && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <MapPin className="h-12 w-12 text-slate-600 mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-slate-200">Quét vị trí kệ cất</h3>
              <p className="text-xs text-slate-400">Sản phẩm: <span className="font-bold text-emerald-400">{selectedProduct.code}</span></p>
            </div>
            <ScanInput id="locationBarcodeScan" label="Quét kệ cất" onScan={handleScanLocation} placeholder="Quét kệ..." />
          </div>
        )}

        {currentStep === "SCAN_SERIAL" && selectedProduct && selectedLocation && (
          <div className="space-y-4">
            <Card className="border-slate-800 bg-slate-800/40">
              <CardContent className="p-4 space-y-2 text-xs">
                <p className="text-slate-300">Sản phẩm: <span className="font-mono font-bold text-white">{selectedProduct.code}</span></p>
                <p className="text-slate-300">Kệ cất: <span className="font-mono font-bold text-white">{selectedLocation.code}</span></p>
                <p className="text-slate-300">Đã quét: <span className="font-mono font-bold text-emerald-400">{scannedSerials.length} mã</span></p>
              </CardContent>
            </Card>

            <ScanInput id="serialBarcodeScan" label="Quét mã Serial" onScan={handleScanSerial} placeholder="Quét mã serial sản phẩm..." />

            <div className="bg-slate-900/60 p-3 rounded text-xs space-y-1.5 border border-slate-800 max-h-[200px] overflow-y-auto">
              <span className="text-[10px] text-slate-500 block border-b border-slate-800 pb-1">Mã serial đã quét ở lượt này:</span>
              {scannedSerials.length === 0 ? (
                <div className="text-slate-400 italic text-center py-2">Chưa quét mã nào</div>
              ) : (
                scannedSerials.map((s, idx) => (
                  <div key={idx} className="flex justify-between text-[11px] text-slate-200">
                    <span className="font-mono">{s}</span>
                    <CheckCircle className="h-3 w-3 text-emerald-500" />
                  </div>
                ))
              )}
            </div>

            <Button onClick={handleReset} variant="outline" className="border-slate-700 text-slate-300 w-full h-10">
              Hoàn tất & Chuyển sản phẩm khác
            </Button>
          </div>
        )}
      </div>
    </MobileShell>
  );
}
