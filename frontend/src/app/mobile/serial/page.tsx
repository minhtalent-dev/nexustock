"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import api from "@/lib/api";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { showError, showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
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
  const t = useTranslations("Mobile.serial");
  const tErrors = useTranslations("Errors");
  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [selectedLocation, setSelectedLocation] = useState<StorageLocation | null>(null);
  const [scannedSerials, setScannedSerials] = useState<string[]>([]);
  const [currentStep, setCurrentStep] = useState<"SCAN_PRODUCT" | "SCAN_LOCATION" | "SCAN_SERIAL">("SCAN_PRODUCT");

  const showApiErr = (err: unknown, fallback: string) => {
    const { codeLabel, message } = resolveApiError(err, tErrors);
    showApiErrorToast(codeLabel, message || fallback);
  };

  const fetchMetadata = useCallback(async () => {
    try {
      const prodRes = await api.get<{ items: Product[] }>("/master-data/products");
      setProducts(prodRes.data.items || []);
      const locRes = await api.get<{ items: StorageLocation[] }>("/master-data/storage-locations");
      setLocations(locRes.data.items || []);
    } catch {
      showError(t("toast.metaFailed"));
    }
  }, [t]);

  useEffect(() => {
    queueMicrotask(() => void fetchMetadata());
  }, [fetchMetadata]);

  const handleScanProduct = (barcode: string) => {
    const matched = products.find((p) => p.code.toUpperCase() === barcode.toUpperCase());
    if (!matched) {
      showError(t("toast.productNotFound", { code: barcode }));
      return;
    }
    if (!matched.isSerialTracked) {
      showError(t("toast.notSerialManaged", { code: matched.code }));
      return;
    }
    setSelectedProduct(matched);
    showSuccess(t("toast.productOk", { code: matched.code }));
    setCurrentStep("SCAN_LOCATION");
  };

  const handleScanLocation = (barcode: string) => {
    const matched = locations.find((l) => l.code.toUpperCase() === barcode.toUpperCase());
    if (!matched) {
      showError(t("toast.locNotFound", { code: barcode }));
      return;
    }
    setSelectedLocation(matched);
    showSuccess(t("toast.locOk", { code: matched.code }));
    setCurrentStep("SCAN_SERIAL");
  };

  const handleScanSerial = async (serialNo: string) => {
    if (!selectedProduct || !selectedLocation) return;

    if (scannedSerials.includes(serialNo)) {
      showError(t("toast.dupSerial"));
      return;
    }

    try {
      await api.post("/serials/receive", {
        itemId: selectedProduct.id,
        locationId: selectedLocation.id,
        serialNo,
      });
      setScannedSerials((prev) => [serialNo, ...prev]);
      showSuccess(t("toast.serialOk", { serial: serialNo }));
    } catch (err: unknown) {
      showApiErr(err, t("toast.serialFailed"));
    }
  };

  const handleReset = () => {
    setSelectedProduct(null);
    setSelectedLocation(null);
    setScannedSerials([]);
    setCurrentStep("SCAN_PRODUCT");
  };

  return (
    <PageShell className="gap-6">
      <MobileShell>
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <Link href="/mobile" className="text-muted-foreground hover:text-foreground p-2">
            <ArrowLeft className="h-4 w-4" />
          </Link>
          <h2 className="text-lg font-bold flex items-center gap-2 text-foreground">
            <Smartphone className="h-5 w-5 text-emerald-500" />
            {t("page.title")}
          </h2>
        </div>

        {currentStep === "SCAN_PRODUCT" && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <Box className="h-12 w-12 text-muted-foreground mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-foreground">{t("states.productTitle")}</h3>
              <p className="text-xs text-muted-foreground">{t("states.productHint")}</p>
            </div>
            <ScanInput
              id="productBarcodeScan"
              label={t("fields.scanProduct")}
              onScan={handleScanProduct}
              placeholder={t("fields.scanProductPlaceholder")}
            />
          </div>
        )}

        {currentStep === "SCAN_LOCATION" && selectedProduct && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <MapPin className="h-12 w-12 text-muted-foreground mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-foreground">{t("states.locationTitle")}</h3>
              <p className="text-xs text-muted-foreground">{t("states.locationHint", { code: selectedProduct.code })}</p>
            </div>
            <ScanInput
              id="locationBarcodeScan"
              label={t("fields.scanLocation")}
              onScan={handleScanLocation}
              placeholder={t("fields.scanLocationPlaceholder")}
            />
          </div>
        )}

        {currentStep === "SCAN_SERIAL" && selectedProduct && selectedLocation && (
          <div className="space-y-4">
            <Card className="border-border bg-card/40">
              <CardContent className="p-4 space-y-2 text-xs">
                <p className="text-muted-foreground">
                  {t("labels.product")}{" "}
                  <span className="font-mono font-bold text-foreground">{selectedProduct.code}</span>
                </p>
                <p className="text-muted-foreground">
                  {t("labels.location")}{" "}
                  <span className="font-mono font-bold text-foreground">{selectedLocation.code}</span>
                </p>
                <p className="text-muted-foreground">
                  <span className="font-mono font-bold text-emerald-400">
                    {t("labels.scanned", { count: scannedSerials.length })}
                  </span>
                </p>
              </CardContent>
            </Card>

            <ScanInput
              id="serialBarcodeScan"
              label={t("fields.scanSerial")}
              onScan={handleScanSerial}
              placeholder={t("fields.scanSerialPlaceholder")}
            />

            <div className="bg-background/60 p-3 rounded text-xs space-y-1.5 border border-border max-h-[200px] overflow-y-auto">
              <span className="text-[10px] text-muted-foreground block border-b border-border pb-1">
                {t("labels.serialList")}
              </span>
              {scannedSerials.length === 0 ? (
                <div className="text-muted-foreground italic text-center py-2">{t("labels.emptySerials")}</div>
              ) : (
                scannedSerials.map((s, idx) => (
                  <div key={idx} className="flex justify-between text-[11px] text-foreground">
                    <span className="font-mono">{s}</span>
                    <CheckCircle className="h-3 w-3 text-emerald-500" />
                  </div>
                ))
              )}
            </div>

            <Button onClick={handleReset} variant="outline" className="border-border text-muted-foreground w-full h-10">
              {t("actions.reset")}
            </Button>
          </div>
        )}
      </div>
    </MobileShell>
    </PageShell>
  );
}
