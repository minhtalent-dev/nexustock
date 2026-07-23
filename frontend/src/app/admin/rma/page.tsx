"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { RefreshCw, Undo2, CheckCircle2, FlaskConical, AlertTriangle, PackageSearch } from "lucide-react";

interface RmaItem {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  qtyExpected: number;
  qtyReceived: number;
  serialNo: string | null;
  reasonCode: string;
}

interface RmaRequest {
  id: string;
  rmaNo: string;
  customerId: string;
  customerName: string;
  referenceNo: string | null;
  status: string;
  createdAt: string;
  createdBy: string;
  items: RmaItem[];
}

export default function RmaPage() {
  const t = useTranslations("Admin.rma");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [rmas, setRmas] = useState<RmaRequest[]>([]);
  const [selectedRma, setSelectedRma] = useState<RmaRequest | null>(null);
  const [loading, setLoading] = useState(false);
  const [processing, setProcessing] = useState(false);

  const fetchRmas = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<RmaRequest[]>("/rma");
      setRmas(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchRmas());
  }, [fetchRmas]);

  const handleProcessQc = async (rmaId: string, item: RmaItem, disposition: string) => {
    setProcessing(true);
    try {
      const payload = {
        results: [
          {
            rmaItemId: item.id,
            qcStatus: "PASS",
            disposition: disposition,
            qty: item.qtyReceived,
            notes: "Xử lý nhanh qua Dashboard Admin"
          }
        ]
      };
      await api.post(`/rma/${rmaId}/qc`, payload);
      showSuccess(t("toastQcSuccess", { disposition }));
      fetchRmas();
      if (selectedRma?.id === rmaId) {
         const updated = await api.get<RmaRequest>(`/rma/${rmaId}`);
         setSelectedRma(updated.data);
      }
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.qcFailed"));
    } finally {
      setProcessing(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "OPEN": return <Badge className="bg-blue-500 hover:bg-blue-600">{t("statusOpen")}</Badge>;
      case "RECEIVED": return <Badge className="bg-amber-500 hover:bg-amber-600">{t("statusReceived")}</Badge>;
      case "QC_COMPLETED": return <Badge className="bg-emerald-500 hover:bg-emerald-600">{t("statusQcCompleted")}</Badge>;
      case "CLOSED": return <Badge className="bg-zinc-500 hover:bg-zinc-600">{t("statusClosed")}</Badge>;
      default: return <Badge variant="outline">{status}</Badge>;
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Undo2 className="h-6 w-6 text-orange-500" />
            {t("title")}
          </h1>
          <p className="text-xs text-muted-foreground mt-1">
            {t("subtitle")}
          </p>
        </div>
        <Button
          onClick={fetchRmas}
          variant="outline"
          className="border-border hover:bg-muted text-muted-foreground h-9 px-4 flex items-center gap-2"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
          {tc("refresh")}
        </Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <Card className="bg-card border-border text-foreground">
            <CardContent className="p-0">
              {loading && rmas.length === 0 ? (
                <div className="text-center py-12 text-muted-foreground text-xs font-mono">{tc("loading")}</div>
              ) : (
                <Table className="text-xs">
                  <TableHeader className="border-b border-border">
                    <TableRow className="border-b border-border hover:bg-muted/50">
                      <TableHead className="text-muted-foreground">{t("colRmaNo")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colCustomer")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colCreatedAt")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colStatus")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rmas.map((r) => (
                      <TableRow 
                        key={r.id} 
                        onClick={() => setSelectedRma(r)}
                        className={`border-b border-border/50 hover:bg-muted/30 cursor-pointer ${
                          selectedRma?.id === r.id ? "bg-muted/80" : ""
                        }`}
                      >
                        <TableCell className="font-bold text-foreground font-mono">{r.rmaNo}</TableCell>
                        <TableCell className="text-muted-foreground">{r.customerName}</TableCell>
                        <TableCell className="text-muted-foreground">{new Date(r.createdAt).toLocaleDateString()}</TableCell>
                        <TableCell>{getStatusBadge(r.status)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="lg:col-span-1">
          {selectedRma ? (
            <Card className="bg-card border-border text-foreground sticky top-6">
              <CardHeader className="border-b border-border pb-4">
                <CardTitle className="text-sm font-semibold flex items-center justify-between">
                  {t("detailTitle", { rmaNo: selectedRma.rmaNo })}
                  {getStatusBadge(selectedRma.status)}
                </CardTitle>
                <div className="text-[10px] text-muted-foreground mt-1">
                   {t("reference")}: {selectedRma.referenceNo || tc("notAvailable")} | {t("createdBy")}: {selectedRma.createdBy}
                </div>
              </CardHeader>
              <CardContent className="p-4 flex flex-col gap-4">
                <h4 className="text-[10px] uppercase font-bold text-muted-foreground tracking-wider">{t("productList")}</h4>
                <div className="space-y-3">
                  {selectedRma.items.map(item => (
                    <div key={item.id} className="bg-muted/50 rounded p-3 border border-border/50 flex flex-col gap-2">
                      <div className="flex justify-between items-start">
                        <div>
                          <p className="text-[11px] font-bold text-foreground">{item.itemCode}</p>
                          <p className="text-[10px] text-muted-foreground">{item.itemName}</p>
                        </div>
                        <div className="text-right">
                          <p className="text-[10px] text-muted-foreground">{t("expected")}: <span className="text-muted-foreground font-bold">{item.qtyExpected}</span></p>
                          <p className="text-[10px] text-muted-foreground">{t("received")}: <span className="text-blue-400 font-bold">{item.qtyReceived}</span></p>
                        </div>
                      </div>
                      
                      {selectedRma.status === "RECEIVED" && (
                        <div className="flex gap-2 mt-2 pt-2 border-t border-border">
                          <Button 
                            disabled={processing}
                            onClick={() => handleProcessQc(selectedRma.id, item, "RESTOCK")}
                            className="bg-emerald-600 hover:bg-emerald-500 text-[10px] h-7 px-2 flex-1"
                          >
                            <CheckCircle2 className="h-3 w-3 mr-1" /> {t("restock")}
                          </Button>
                          <Button 
                            disabled={processing}
                            onClick={() => handleProcessQc(selectedRma.id, item, "SCRAP")}
                            className="bg-red-600 hover:bg-red-500 text-[10px] h-7 px-2 flex-1"
                          >
                            <AlertTriangle className="h-3 w-3 mr-1" /> {t("scrap")}
                          </Button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
                
                {selectedRma.status === "QC_COMPLETED" && (
                  <div className="mt-4 p-4 bg-emerald-900/20 border border-emerald-800/30 rounded-lg flex flex-col items-center text-center gap-2">
                    <FlaskConical className="h-8 w-8 text-emerald-500 opacity-50" />
                    <p className="text-xs text-emerald-200">{t("qcCompleted")}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          ) : (
            <div className="bg-card border border-border rounded-lg p-16 text-center text-muted-foreground text-xs flex flex-col items-center justify-center gap-4">
              <PackageSearch className="h-10 w-10 opacity-20" />
              {t("selectHint")}
            </div>
          )}
        </div>
      </div>
    </PageShell>
  );
}
