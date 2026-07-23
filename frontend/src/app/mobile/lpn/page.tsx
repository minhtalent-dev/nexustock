"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useState } from "react";
import { useTranslations } from "next-intl";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import api from "@/lib/api";
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
  const t = useTranslations("Mobile.lpn");
  const tErrors = useTranslations("Errors");
  const [lpn, setLpn] = useState<Lpn | null>(null);
  const [lpnItems, setLpnItems] = useState<LpnItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [currentStep, setCurrentStep] = useState<"SCAN_LPN" | "SCAN_TARGET_LOC" | "CONFIRM">("SCAN_LPN");
  const [targetLocation, setTargetLocation] = useState<StorageLocation | null>(null);
  const [targetLocationCode, setTargetLocationCode] = useState("");

  const showApiErr = (err: unknown, fallback: string) => {
    const { codeLabel, message } = resolveApiError(err, tErrors);
    showApiErrorToast(codeLabel, message || fallback);
  };

  const handleScanLpn = async (barcode: string) => {
    setLoading(true);
    try {
      const lpnsRes = await api.get<Lpn[]>("/lpns");
      const matchedLpn = lpnsRes.data.find((l) => l.lpnNo.toUpperCase() === barcode.toUpperCase());

      if (!matchedLpn) {
        showError(t("toast.lpnNotFound", { code: barcode }));
        return;
      }

      setLpn(matchedLpn);
      const itemsRes = await api.get<{ items: LpnItem[] }>("/inventory/balances", {
        params: { lpnId: matchedLpn.id },
      });
      setLpnItems(itemsRes.data?.items || []);
      showSuccess(t("toast.lpnOk"));
      setCurrentStep("SCAN_TARGET_LOC");
    } catch (err: unknown) {
      showApiErr(err, t("toast.lpnBad"));
    } finally {
      setLoading(false);
    }
  };

  const handleScanTargetLocation = async (barcode: string) => {
    setLoading(true);
    try {
      const locsRes = await api.get<{ items: StorageLocation[] }>("/master-data/storage-locations");
      const matchedLoc = (locsRes.data.items || []).find(
        (l) => l.code.toUpperCase() === barcode.toUpperCase()
      );

      if (!matchedLoc) {
        showError(t("toast.locNotFound", { code: barcode }));
        return;
      }

      if (lpn && matchedLoc.id === lpn.locationId) {
        showError(t("toast.sameLoc"));
        return;
      }

      setTargetLocation(matchedLoc);
      setTargetLocationCode(barcode);
      showSuccess(t("toast.locOk"));
      setCurrentStep("CONFIRM");
    } catch (err: unknown) {
      showApiErr(err, t("toast.locBad"));
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmMove = async () => {
    if (!lpn || !targetLocation) return;

    setLoading(true);
    try {
      await api.post(`/lpns/${lpn.id}/move`, {
        targetLocationId: targetLocation.id,
      });
      showSuccess(t("toast.moveOk", { lpn: lpn.lpnNo }));
      setLpn(null);
      setLpnItems([]);
      setTargetLocation(null);
      setTargetLocationCode("");
      setCurrentStep("SCAN_LPN");
    } catch (err: unknown) {
      showApiErr(err, t("toast.moveFailed"));
    } finally {
      setLoading(false);
    }
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
            <Layers className="h-5 w-5 text-emerald-500" />
            {t("page.title")}
          </h2>
        </div>

        {currentStep === "SCAN_LPN" && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <Box className="h-12 w-12 text-muted-foreground mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-foreground">{t("states.readyTitle")}</h3>
              <p className="text-xs text-muted-foreground">{t("states.readyHint")}</p>
            </div>
            <ScanInput
              id="lpnBarcodeScan"
              label={t("fields.scanLpn")}
              onScan={handleScanLpn}
              placeholder={t("fields.scanLpnPlaceholder")}
            />
          </div>
        )}

        {lpn && (
          <Card className="border-border bg-card/40">
            <CardHeader className="pb-2 border-b border-border/80">
              <CardTitle className="text-xs font-semibold text-foreground">
                {t("labels.pallet", { lpn: lpn.lpnNo })}
              </CardTitle>
            </CardHeader>
            <CardContent className="p-4 space-y-4">
              <div className="bg-background/60 p-3 rounded text-xs space-y-1.5 border border-border max-h-[150px] overflow-y-auto">
                <span className="text-[10px] text-muted-foreground block border-b border-border pb-1">
                  {t("labels.itemsOnPallet")}
                </span>
                {lpnItems.length === 0 ? (
                  <div className="text-muted-foreground italic text-center py-2">{t("labels.emptyPallet")}</div>
                ) : (
                  lpnItems.map((item, idx) => (
                    <div key={idx} className="flex justify-between text-[11px] text-foreground">
                      <span>{t("labels.itemLot", { code: item.itemCode, lot: item.lotNo })}</span>
                      <span className="font-bold text-foreground">{item.qtyOnHand}</span>
                    </div>
                  ))
                )}
              </div>

              {currentStep === "SCAN_TARGET_LOC" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-muted p-3 rounded text-center border border-amber-500/20">
                    <span className="text-xs text-muted-foreground block">{t("labels.step2")}</span>
                  </div>
                  <ScanInput
                    id="targetLocationScan"
                    label={t("fields.scanTarget")}
                    onScan={handleScanTargetLocation}
                    placeholder={t("fields.scanTargetPlaceholder")}
                  />
                </div>
              )}

              {currentStep === "CONFIRM" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-muted p-4 rounded text-center border border-emerald-500/20 space-y-2">
                    <span className="text-xs text-muted-foreground block">{t("labels.confirmTitle")}</span>
                    <span className="text-lg font-bold font-mono text-emerald-400 block">{lpn.lpnNo}</span>
                    <div className="flex items-center justify-center gap-3 text-xs text-foreground pt-1">
                      <span className="font-mono text-muted-foreground">{t("labels.oldLocation")}</span>
                      <ArrowRight className="h-3.5 w-3.5 text-emerald-500" />
                      <span className="font-mono text-emerald-400 font-bold">{targetLocationCode}</span>
                    </div>
                  </div>

                  <Button
                    onClick={handleConfirmMove}
                    disabled={loading}
                    className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold py-4 rounded-lg shadow-lg"
                  >
                    {loading ? t("actions.processing") : t("actions.confirm")}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </MobileShell>
    </PageShell>
  );
}
