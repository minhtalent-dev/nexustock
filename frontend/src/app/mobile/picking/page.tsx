"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import MobileShell from "@/components/mobile/mobile-shell";
import ScanInput from "@/components/mobile/scan-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import api from "@/lib/api";
import { ArrowLeft, Box, ClipboardCheck, ArrowRight } from "lucide-react";
import Link from "next/link";

interface Task {
  id: string;
  referenceType: string;
  referenceId: string;
  step: string;
  locationId: string;
  assignedUser: string;
  status: string;
}

export default function PickingPage() {
  const t = useTranslations("Mobile.picking");
  const tErrors = useTranslations("Errors");
  const [task, setTask] = useState<Task | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentStep, setCurrentStep] = useState<"CLAIM" | "SCAN_LOC" | "SCAN_LOT" | "INPUT_QTY" | "COMPLETE">("CLAIM");
  const [userLocation, setUserLocation] = useState("");

  const showApiErr = (err: unknown, fallback: string) => {
    const { codeLabel, message } = resolveApiError(err, tErrors);
    showApiErrorToast(codeLabel, message || fallback);
  };

  const handleClaimNextTask = async () => {
    setLoading(true);
    try {
      const res = await api.get<{ task: Task; message: string }>("/mobile/tasks/next", {
        params: { currentLocationCode: userLocation },
      });
      if (res.data.task) {
        setTask(res.data.task);
        setCurrentStep("SCAN_LOC");
        showSuccess(res.data.message);
      } else {
        showError(res.data.message || t("toast.noTask"));
      }
    } catch (err: unknown) {
      showApiErr(err, t("toast.claimFailed"));
    } finally {
      setLoading(false);
    }
  };

  const handleScanLocation = async (barcode: string) => {
    setLoading(true);
    try {
      await api.post("/mobile/scan/validate", { barcode, context: "LOCATION" });
      setUserLocation(barcode);
      showSuccess(t("toast.locOk"));
      setCurrentStep("SCAN_LOT");
    } catch (err: unknown) {
      showApiErr(err, t("toast.locBad"));
    } finally {
      setLoading(false);
    }
  };

  const handleScanLot = async (barcode: string) => {
    setLoading(true);
    try {
      await api.post("/mobile/scan/validate", { barcode, context: "LOT" });
      showSuccess(t("toast.lotOk"));
      setCurrentStep("INPUT_QTY");
    } catch (err: unknown) {
      showApiErr(err, t("toast.lotBad"));
    } finally {
      setLoading(false);
    }
  };

  const handleCompleteTask = async () => {
    if (!task) return;
    setLoading(true);
    try {
      await api.post(`/mobile/tasks/${task.id}/complete`);
      showSuccess(t("toast.completeOk"));
      setTask(null);
      setCurrentStep("CLAIM");
    } catch (err: unknown) {
      showApiErr(err, t("toast.completeFailed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <MobileShell>
      <div className="space-y-4">
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
            <ClipboardCheck className="h-5 w-5 text-orange-500" />
            {t("page.title")}
          </h2>
        </div>

        {currentStep === "CLAIM" && (
          <div className="space-y-4 py-8">
            <div className="text-center space-y-2">
              <Box className="h-12 w-12 text-slate-600 mx-auto animate-bounce" />
              <h3 className="text-base font-semibold text-slate-200">{t("states.readyTitle")}</h3>
              <p className="text-xs text-slate-400">{t("states.readyHint")}</p>
            </div>

            <div className="space-y-2">
              <label htmlFor="userLoc" className="text-xs text-slate-400 block">
                {t("fields.userLocation")}
              </label>
              <input
                id="userLoc"
                type="text"
                value={userLocation}
                onChange={(e) => setUserLocation(e.target.value.toUpperCase())}
                placeholder={t("fields.userLocationPlaceholder")}
                className="w-full bg-slate-800 border border-slate-700 rounded p-2 text-white font-mono text-sm text-center"
              />
            </div>

            <Button
              onClick={handleClaimNextTask}
              disabled={loading}
              className="w-full bg-orange-600 hover:bg-orange-700 text-white py-6 text-base font-bold rounded-lg shadow-lg gap-2"
            >
              <ArrowRight className="h-5 w-5" />
              {loading ? t("actions.claiming") : t("actions.claim")}
            </Button>
          </div>
        )}

        {task && (
          <Card className="border-slate-800 bg-slate-800/40">
            <CardHeader className="pb-2 border-b border-slate-800/80">
              <CardTitle className="text-sm font-semibold text-slate-200">{t("labels.currentTask")}</CardTitle>
            </CardHeader>
            <CardContent className="p-4 space-y-4">
              <div className="grid grid-cols-2 gap-2 text-xs font-mono text-slate-300">
                <div>
                  {t("labels.type")} <span className="text-orange-500 font-bold">{task.referenceType}</span>
                </div>
                <div>
                  {t("labels.orderId")}{" "}
                  <span className="text-white">{task.referenceId.substring(0, 8)}</span>
                </div>
                <div className="col-span-2">
                  {t("labels.step")} <span className="text-white font-bold">{task.step}</span>
                </div>
              </div>

              {currentStep === "SCAN_LOC" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-800/80 p-3 rounded text-center border border-orange-500/25">
                    <span className="text-xs text-slate-400 block">{t("fields.moveHint")}</span>
                    <span className="text-lg font-bold font-mono text-orange-500">LOC-A-01</span>
                  </div>
                  <ScanInput
                    id="locScan"
                    label={t("fields.scanLoc")}
                    onScan={handleScanLocation}
                    placeholder={t("fields.scanLocPlaceholder")}
                  />
                </div>
              )}

              {currentStep === "SCAN_LOT" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-800/80 p-3 rounded text-center border border-orange-500/25">
                    <span className="text-xs text-slate-400 block">{t("fields.lotHint")}</span>
                    <span className="text-base font-bold font-mono text-white">LOT-CC-1783908121977</span>
                  </div>
                  <ScanInput
                    id="lotScan"
                    label={t("fields.scanLot")}
                    onScan={handleScanLot}
                    placeholder={t("fields.scanLotPlaceholder")}
                  />
                </div>
              )}

              {currentStep === "INPUT_QTY" && (
                <div className="space-y-4 pt-2">
                  <div className="bg-slate-800/80 p-3 rounded text-center border border-orange-500/25">
                    <span className="text-xs text-slate-400 block">{t("fields.qtyHint")}</span>
                    <span className="text-2xl font-bold font-mono text-emerald-400">
                      {t("fields.qtyUnit", { count: 10 })}
                    </span>
                  </div>

                  <Button
                    onClick={handleCompleteTask}
                    disabled={loading}
                    className="w-full bg-green-600 hover:bg-green-700 text-white font-bold py-4 rounded-lg"
                  >
                    {t("actions.complete")}
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
