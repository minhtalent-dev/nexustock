"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { ClipboardCheck, Plus, RefreshCw } from "lucide-react";

interface Stocktake {
  id: string;
  stocktakeNo: string;
  status: string;
  zoneName: string;
  totalVarianceAmount: number;
  currentApprovalLevel: number;
  createdAt: string;
  createdBy: string;
}

export default function StocktakesPage() {
  const t = useTranslations("Admin.stocktakes");
  const tErrors = useTranslations("Errors");

  const [stocktakes, setStocktakes] = useState<Stocktake[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchStocktakes = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<Stocktake[]>("/stocktakes");
      setStocktakes(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchStocktakes());
  }, [fetchStocktakes]);

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Draft":
        return <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-700">{t("statusDraft")}</span>;
      case "Counting":
        return <span className="rounded-full bg-blue-100 px-2 py-1 text-xs font-semibold text-blue-700">{t("statusCounting")}</span>;
      case "Pending_L1_Approve":
        return <span className="rounded-full bg-yellow-100 px-2 py-1 text-xs font-semibold text-yellow-700">{t("statusPendingL1")}</span>;
      case "Pending_L2_Approve":
        return <span className="rounded-full bg-orange-100 px-2 py-1 text-xs font-semibold text-orange-700">{t("statusPendingL2")}</span>;
      case "Pending_L3_Approve":
        return <span className="rounded-full bg-red-100 px-2 py-1 text-xs font-semibold text-red-700">{t("statusPendingL3")}</span>;
      case "Approved":
        return <span className="rounded-full bg-green-100 px-2 py-1 text-xs font-semibold text-green-700">{t("statusApproved")}</span>;
      case "Cancelled":
        return <span className="rounded-full bg-red-100 px-2 py-1 text-xs font-semibold text-red-700">{t("statusCancelled")}</span>;
      default:
        return <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-700">{status}</span>;
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <ClipboardCheck className="h-6 w-6 text-primary" />
          {t("title")}
        </h1>
        <div className="flex gap-2">
          <Button asChild>
            <Link href="/admin/inventory/stocktakes/new" className="gap-2">
              <Plus className="h-4 w-4" />
              {t("createButton")}
            </Link>
          </Button>
          <Button onClick={fetchStocktakes} variant="outline" className="gap-2">
            <RefreshCw className="h-4 w-4" />
            {t("refresh")}
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("listTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-8 text-center text-muted-foreground">{t("loading")}</div>
          ) : stocktakes.length === 0 ? (
            <div className="py-8 text-center text-muted-foreground">{t("empty")}</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("colStocktakeNo")}</TableHead>
                  <TableHead>{t("colZone")}</TableHead>
                  <TableHead>{t("colVariance")}</TableHead>
                  <TableHead>{t("colStatus")}</TableHead>
                  <TableHead>{t("colCreatedBy")}</TableHead>
                  <TableHead>{t("colCreatedAt")}</TableHead>
                  <TableHead className="text-right">{t("colActions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {stocktakes.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="font-semibold">{s.stocktakeNo}</TableCell>
                    <TableCell>{s.zoneName}</TableCell>
                    <TableCell className="font-mono">
                      {s.totalVarianceAmount.toLocaleString()} {t("currencySuffix")}
                    </TableCell>
                    <TableCell>{getStatusBadge(s.status)}</TableCell>
                    <TableCell>{s.createdBy}</TableCell>
                    <TableCell>{new Date(s.createdAt).toLocaleString()}</TableCell>
                    <TableCell className="text-right">
                      <Button asChild size="sm" variant="outline">
                        <Link href={`/admin/inventory/stocktakes/${s.id}`}>{t("detailBtn")}</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
