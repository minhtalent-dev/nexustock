"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { OpsExportButtons } from "@/components/ops-export-buttons";
import { RefreshCw, Search, Zap } from "lucide-react";

interface CandidateDto {
  id: string;
  itemId: string;
  lotId: string;
  waveItemId: string;
  qtyAvailable: number;
  qtyRequested: number;
  qtyMatched: number;
  matchScore: number;
  status: string;
  createdAt: string;
}

const STATUS_COLORS: Record<string, string> = {
  Pending: "bg-yellow-100 text-yellow-800",
  Accepted: "bg-green-100 text-green-800",
  Rejected: "bg-red-100 text-red-800",
  Expired: "bg-gray-100 text-gray-600",
  Executing: "bg-blue-100 text-blue-800",
};

export default function CrossDockingPage() {
  const router = useRouter();
  const t = useTranslations("Admin.crossDocking");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [candidates, setCandidates] = useState<CandidateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const pageSize = 20;

  const [evaluateOpen, setEvaluateOpen] = useState(false);
  const [lotIdInput, setLotIdInput] = useState("");
  const [evaluating, setEvaluating] = useState(false);

  const fetchCandidates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string | number> = { page, pageSize };
      if (statusFilter && statusFilter !== "all") params.status = statusFilter;
      const res = await api.get("/cross-docking/candidates", { params });
      setCandidates(res.data.items ?? []);
      setTotal(res.data.total ?? 0);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      const errorMessage = message || t("errors.loadFailed");
      setError(errorMessage);
      showApiErrorToast(codeLabel, errorMessage);
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchCandidates());
  }, [fetchCandidates]);

  const handleEvaluate = async () => {
    if (!lotIdInput.trim()) return;
    setEvaluating(true);
    try {
      const res = await api.post("/cross-docking/evaluate", { lotId: lotIdInput.trim() });
      const count = res.data.candidates?.length ?? 0;
      showSuccess(t("toastEvaluateSuccess", { count }));
      setEvaluateOpen(false);
      setLotIdInput("");
      fetchCandidates();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.evaluateFailed"));
    } finally {
      setEvaluating(false);
    }
  };

  const getStatusLabel = (status: string) => {
    switch (status) {
      case "Pending":
        return t("statusPending");
      case "Accepted":
        return t("statusAccepted");
      case "Rejected":
        return t("statusRejected");
      case "Expired":
        return t("statusExpired");
      case "Executing":
        return t("statusExecuting");
      default:
        return status;
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <PageShell className="gap-6">
      <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t("title")}</h1>
          <p className="text-sm text-muted-foreground">{t("subtitle")}</p>
        </div>
        <div className="flex gap-2">
          <OpsExportButtons type="CROSS_DOCK_CANDIDATES" />
          <Button variant="outline" size="sm" onClick={fetchCandidates}>
            <RefreshCw className="w-4 h-4 mr-1" /> {tc("refresh")}
          </Button>
          <Button size="sm" onClick={() => setEvaluateOpen(true)}>
            <Zap className="w-4 h-4 mr-1" /> {t("evaluateLot")}
          </Button>
        </div>
      </div>

      <div className="flex gap-3 items-center">
        <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v ?? "all"); setPage(1); }}>
          <SelectTrigger className="w-40">
            <SelectValue placeholder={t("statusPlaceholder")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("allStatuses")}</SelectItem>
            <SelectItem value="Pending">{t("statusPending")}</SelectItem>
            <SelectItem value="Accepted">{t("statusAccepted")}</SelectItem>
            <SelectItem value="Rejected">{t("statusRejected")}</SelectItem>
            <SelectItem value="Expired">{t("statusExpired")}</SelectItem>
          </SelectContent>
        </Select>
        <span className="text-sm text-muted-foreground">{t("totalCount", { total })}</span>
      </div>

      <Card>
        <CardContent className="p-0">
          {error ? (
            <div className="p-6 text-center text-red-600">{error}</div>
          ) : loading ? (
            <div className="p-4 space-y-2">
              {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : candidates.length === 0 ? (
            <div className="p-12 text-center text-muted-foreground">
              <Search className="w-8 h-8 mx-auto mb-2 opacity-40" />
              {t("empty")}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("colItemId")}</TableHead>
                  <TableHead>{t("colLotId")}</TableHead>
                  <TableHead className="text-right">{t("colQtyMatched")}</TableHead>
                  <TableHead className="text-right">{t("colScore")}</TableHead>
                  <TableHead>{tc("status")}</TableHead>
                  <TableHead>{t("colCreated")}</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {candidates.map((c) => (
                  <TableRow key={c.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/admin/cross-docking/${c.id}`)}>
                    <TableCell className="font-mono text-xs">{c.itemId}</TableCell>
                    <TableCell className="font-mono text-xs">{c.lotId}</TableCell>
                    <TableCell className="text-right">{c.qtyMatched}</TableCell>
                    <TableCell className="text-right">{c.matchScore}%</TableCell>
                    <TableCell>
                      <Badge className={STATUS_COLORS[c.status] ?? ""}>{getStatusLabel(c.status)}</Badge>
                    </TableCell>
                    <TableCell className="text-xs">{new Date(c.createdAt).toLocaleString()}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" onClick={(e) => { e.stopPropagation(); router.push(`/admin/cross-docking/${c.id}`); }}>
                        {t("view")}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {totalPages > 1 && (
        <div className="flex justify-center gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            {tc("previous")}
          </Button>
          <span className="text-sm self-center">{t("pageInfo", { page, totalPages })}</span>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            {tc("next")}
          </Button>
        </div>
      )}

      <Dialog open={evaluateOpen} onOpenChange={setEvaluateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("evaluateDialogTitle")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <p className="text-sm text-muted-foreground">{t("evaluateDialogHint")}</p>
            <Input
              placeholder={t("lotIdPlaceholder")}
              value={lotIdInput}
              onChange={(e) => setLotIdInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleEvaluate()}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEvaluateOpen(false)}>{tc("cancel")}</Button>
            <Button disabled={evaluating || !lotIdInput.trim()} onClick={handleEvaluate}>
              {evaluating ? t("evaluating") : t("evaluate")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
    </PageShell>
  );
}
