"use client";

import { PageShell } from "@/components/layout/page-shell";

import * as React from "react";
import { useTranslations } from "next-intl";
import { taskInterleavingApi, TaskRecommendationListItemDto, TaskRecommendationDetailResponse, TaskInterleavingKpiResponse } from "@/lib/task-interleaving-api";
import { RecommendationKpis } from "./components/recommendation-kpis";
import { RecommendationTable } from "./components/recommendation-table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { RefreshCw, Search, ShieldAlert, Ban } from "lucide-react";
import { showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { isFeatureDisabledError, isUnauthorizedError } from "@/lib/http-error";

type PageState = "loading" | "ready" | "empty" | "error" | "unauthorized" | "featureDisabled";

const STATUS_KEYS = ["ALL", "Open", "Accepted", "Rejected", "Expired", "Superseded", "NoCandidate"] as const;
const OPERATION_KEYS = ["ALL", "Picking", "Putaway", "Replenishment", "CycleCount", "Packing", "Receiving"] as const;

export default function TaskInterleavingPage() {
  const t = useTranslations("Admin.taskInterleaving");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [items, setItems] = React.useState<TaskRecommendationListItemDto[]>([]);
  const [kpis, setKpis] = React.useState<TaskInterleavingKpiResponse | null>(null);
  const [pageState, setPageState] = React.useState<PageState>("loading");
  const [errorMessage, setErrorMessage] = React.useState<string>("");
  const [detailId, setDetailId] = React.useState<string | null>(null);
  const [detail, setDetail] = React.useState<TaskRecommendationDetailResponse | null>(null);
  const [detailLoading, setDetailLoading] = React.useState(false);

  const [status, setStatus] = React.useState<string>("ALL");
  const [operationType, setOperationType] = React.useState<string>("ALL");
  const [userId, setUserId] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [total, setTotal] = React.useState(0);

  const loadData = React.useCallback(async () => {
    setPageState("loading");
    setErrorMessage("");
    try {
      const kpiParams = {
        userId: userId ? userId : undefined,
        operationType: operationType !== "ALL" ? operationType : undefined,
      };
      const listParams = {
        status: status !== "ALL" ? status : undefined,
        userId: userId ? userId : undefined,
        operationType: operationType !== "ALL" ? operationType : undefined,
        page,
        pageSize: 10,
      };

      const [kpiRes, listRes] = await Promise.all([
        taskInterleavingApi.getKpi(kpiParams),
        taskInterleavingApi.listRecommendations(listParams),
      ]);

      setKpis(kpiRes);
      setItems(listRes.items);
      setTotal(listRes.total);
      setPageState(listRes.items.length === 0 ? "empty" : "ready");
    } catch (err) {
      if (isFeatureDisabledError(err)) {
        setPageState("featureDisabled");
        return;
      }
      if (isUnauthorizedError(err)) {
        setPageState("unauthorized");
        return;
      }
      const { codeLabel, message } = resolveApiError(err, tErrors);
      const msg = message || t("errors.loadFailed");
      setErrorMessage(msg);
      setPageState("error");
      showApiErrorToast(codeLabel, msg);
    }
  }, [status, operationType, userId, page, t, tErrors]);

  React.useEffect(() => {
    queueMicrotask(() => void loadData());
  }, [loadData]);

  const handleViewDetail = async (id: string) => {
    setDetailId(id);
    setDetailLoading(true);
    try {
      const res = await taskInterleavingApi.getRecommendation(id);
      setDetail(res);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.detailFailed"));
      setDetailId(null);
    } finally {
      setDetailLoading(false);
    }
  };

  if (pageState === "featureDisabled") {
    return (
      <div className="flex flex-col items-center justify-center gap-4 p-12 min-h-[50vh]">
        <Ban className="size-12 text-muted-foreground" />
        <h2 className="text-xl font-semibold">{t("featureDisabledTitle")}</h2>
        <p className="text-sm text-muted-foreground text-center max-w-md">
          {t("featureDisabledDesc")}
        </p>
        <Button id="task-interleaving-refresh-button" variant="outline" onClick={loadData}>
          {tc("retry")}
        </Button>
      </div>
    );
  }

  if (pageState === "unauthorized") {
    return (
      <div className="flex flex-col items-center justify-center gap-4 p-12 min-h-[50vh]">
        <ShieldAlert className="size-12 text-muted-foreground" />
        <h2 className="text-xl font-semibold">{t("unauthorizedTitle")}</h2>
        <p className="text-sm text-muted-foreground text-center max-w-md">
          {t("unauthorizedDesc")}
        </p>
      </div>
    );
  }

  const loading = pageState === "loading";
  const totalPages = Math.ceil(total / 10);

  return (
    <PageShell className="gap-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{t("title")}</h1>
          <p className="text-muted-foreground">
            {t("subtitle")}
          </p>
        </div>
        <Button
          id="task-interleaving-refresh-button"
          onClick={loadData}
          disabled={loading}
          data-icon="inline-start"
        >
          <RefreshCw className="size-4" />
          {tc("refresh")}
        </Button>
      </div>

      {pageState === "error" && (
        <div className="rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">
          {errorMessage || t("unexpectedError")}
        </div>
      )}

      <RecommendationKpis kpis={kpis} />

      <div className="flex flex-wrap items-center gap-4 bg-card p-4 rounded-md border">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("status")}</span>
          <Select value={status} onValueChange={(val) => { setStatus(val ?? "ALL"); setPage(1); }}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder={t("allStatus")} />
            </SelectTrigger>
            <SelectContent>
              {STATUS_KEYS.map((key) => (
                <SelectItem key={key} value={key}>
                  {key === "ALL" ? t("allStatus") : t(`recommendationStatus.${key}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("operation")}</span>
          <Select value={operationType} onValueChange={(val) => { setOperationType(val ?? "ALL"); setPage(1); }}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder={t("allOperations")} />
            </SelectTrigger>
            <SelectContent>
              {OPERATION_KEYS.map((key) => (
                <SelectItem key={key} value={key}>
                  {t(`operations.${key}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("userId")}</span>
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 size-4 text-muted-foreground" />
            <Input
              type="text"
              placeholder={t("searchUserPlaceholder")}
              value={userId}
              onChange={(e) => { setUserId(e.target.value); setPage(1); }}
              className="pl-8 w-[250px]"
            />
          </div>
        </div>
      </div>

      {pageState === "empty" ? (
        <div className="flex flex-col items-center justify-center gap-2 border border-dashed rounded-md py-16 text-muted-foreground">
          <p className="font-medium text-sm">{t("emptyTitle")}</p>
          <p className="text-xs">{t("emptyHint")}</p>
        </div>
      ) : (
        <RecommendationTable items={items} onViewDetail={handleViewDetail} />
      )}

      {total > 10 && (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
          >
            {tc("previous")}
          </Button>
          <span className="text-sm font-mono">
            {tc("pageOf", { page, totalPages, total })}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
          >
            {tc("next")}
          </Button>
        </div>
      )}

      <Dialog open={detailId !== null} onOpenChange={(open) => { if (!open) setDetailId(null); }}>
        <DialogContent className="sm:max-w-4xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("detailTitle")}</DialogTitle>
            <DialogDescription>
              {t("detailDesc")}
            </DialogDescription>
          </DialogHeader>

          {detailLoading && (
            <div className="flex items-center justify-center py-12">
              <RefreshCw className="size-8 animate-spin text-muted-foreground" />
            </div>
          )}

          {!detailLoading && detail && (
            <div className="flex flex-col gap-6 py-4">
              <div className="grid grid-cols-2 gap-4 md:grid-cols-4 text-sm bg-muted p-4 rounded-md">
                <div>
                  <span className="text-muted-foreground block text-xs">{t("detailRecommendationId")}</span>
                  <span className="font-mono text-xs">{detail.id}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block text-xs">{t("detailUserId")}</span>
                  <span className="font-mono text-xs">{detail.userId}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block text-xs">{t("detailStatus")}</span>
                  <span className="font-medium">{detail.status}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block text-xs">{t("detailTraceId")}</span>
                  <span className="font-mono text-xs">{detail.traceId ?? "--"}</span>
                </div>
              </div>

              <div>
                <h3 className="text-sm font-semibold mb-3">{t("evaluatedCandidates")}</h3>
                <div className="border rounded-md overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-muted text-muted-foreground text-xs uppercase">
                      <tr>
                        <th className="p-3 text-left">{t("colTaskType")}</th>
                        <th className="p-3 text-left">{t("colTaskId")}</th>
                        <th className="p-3 text-left">{t("colOperation")}</th>
                        <th className="p-3 text-right">{t("colDistanceScore")}</th>
                        <th className="p-3 text-right">{t("colAgeScore")}</th>
                        <th className="p-3 text-right">{t("colPriority")}</th>
                        <th className="p-3 text-right">{t("colContinuity")}</th>
                        <th className="p-3 text-right">{t("colPenalty")}</th>
                        <th className="p-3 text-right font-bold">{t("colTotalScore")}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {detail.candidates.map((c) => (
                        <tr
                          key={c.taskId}
                          className={detail.selectedTaskId === c.taskId ? "bg-indigo-50/50 dark:bg-indigo-950/20 font-medium" : ""}
                        >
                          <td className="p-3 text-xs">{c.taskType}</td>
                          <td className="p-3 font-mono text-xs">{c.taskId.substring(0, 8)}...</td>
                          <td className="p-3 text-xs">{c.operationType}</td>
                          <td className="p-3 text-right font-mono text-xs">{c.explanation.distanceScore.toFixed(1)}</td>
                          <td className="p-3 text-right font-mono text-xs">{c.explanation.ageScore.toFixed(1)}</td>
                          <td className="p-3 text-right font-mono text-xs">{c.explanation.priorityScore.toFixed(1)}</td>
                          <td className="p-3 text-right font-mono text-xs">{c.explanation.continuityScore.toFixed(1)}</td>
                          <td className="p-3 text-right font-mono text-xs text-rose-500">{c.explanation.penaltyScore > 0 ? `-${c.explanation.penaltyScore.toFixed(1)}` : "0.0"}</td>
                          <td className="p-3 text-right font-mono font-semibold">{c.score.toFixed(1)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </PageShell>
  );
}
