"use client";

import * as React from "react";
import { taskInterleavingApi, TaskRecommendationListItemDto, TaskRecommendationDetailResponse, TaskInterleavingKpiResponse } from "@/lib/task-interleaving-api";
import { RecommendationKpis } from "./components/recommendation-kpis";
import { RecommendationTable } from "./components/recommendation-table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { RefreshCw, Search, ShieldAlert, Ban } from "lucide-react";
import { showError } from "@/lib/toast";
import { getHttpErrorMessage, isFeatureDisabledError, isUnauthorizedError } from "@/lib/http-error";

type PageState = "loading" | "ready" | "empty" | "error" | "unauthorized" | "featureDisabled";

export default function TaskInterleavingPage() {
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
      const msg = getHttpErrorMessage(err, "Failed to load task interleaving data.");
      setErrorMessage(msg);
      setPageState("error");
      showError(msg);
    }
  }, [status, operationType, userId, page]);

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
      showError(getHttpErrorMessage(err, "Failed to load recommendation detail."));
      setDetailId(null);
    } finally {
      setDetailLoading(false);
    }
  };

  if (pageState === "featureDisabled") {
    return (
      <div className="flex flex-col items-center justify-center gap-4 p-12 min-h-[50vh]">
        <Ban className="size-12 text-muted-foreground" />
        <h2 className="text-xl font-semibold">Feature disabled</h2>
        <p className="text-sm text-muted-foreground text-center max-w-md">
          Task interleaving is currently turned off. Enable the feature flag to view recommendations and KPIs.
        </p>
        <Button id="task-interleaving-refresh-button" variant="outline" onClick={loadData}>
          Retry
        </Button>
      </div>
    );
  }

  if (pageState === "unauthorized") {
    return (
      <div className="flex flex-col items-center justify-center gap-4 p-12 min-h-[50vh]">
        <ShieldAlert className="size-12 text-muted-foreground" />
        <h2 className="text-xl font-semibold">Unauthorized</h2>
        <p className="text-sm text-muted-foreground text-center max-w-md">
          You do not have permission to view task interleaving logs.
        </p>
      </div>
    );
  }

  const loading = pageState === "loading";

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Task Interleaving Logs</h1>
          <p className="text-muted-foreground">
            Monitor spatial optimization suggestions and labor task assignments.
          </p>
        </div>
        <Button
          id="task-interleaving-refresh-button"
          onClick={loadData}
          disabled={loading}
          data-icon="inline-start"
        >
          <RefreshCw className="size-4" />
          Refresh
        </Button>
      </div>

      {pageState === "error" && (
        <div className="rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">
          {errorMessage || "Unexpected error while loading data."}
        </div>
      )}

      <RecommendationKpis kpis={kpis} />

      <div className="flex flex-wrap items-center gap-4 bg-card p-4 rounded-md border">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">Status</span>
          <Select value={status} onValueChange={(val) => { setStatus(val); setPage(1); }}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="All status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Status</SelectItem>
              <SelectItem value="Open">Open</SelectItem>
              <SelectItem value="Accepted">Accepted</SelectItem>
              <SelectItem value="Rejected">Rejected</SelectItem>
              <SelectItem value="Expired">Expired</SelectItem>
              <SelectItem value="Superseded">Superseded</SelectItem>
              <SelectItem value="NoCandidate">No Candidate</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">Operation</span>
          <Select value={operationType} onValueChange={(val) => { setOperationType(val); setPage(1); }}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="All operations" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Operations</SelectItem>
              <SelectItem value="Picking">Picking</SelectItem>
              <SelectItem value="Putaway">Putaway</SelectItem>
              <SelectItem value="Replenishment">Replenishment</SelectItem>
              <SelectItem value="CycleCount">CycleCount</SelectItem>
              <SelectItem value="Packing">Packing</SelectItem>
              <SelectItem value="Receiving">Receiving</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">User ID</span>
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 size-4 text-muted-foreground" />
            <Input
              type="text"
              placeholder="Search User ID"
              value={userId}
              onChange={(e) => { setUserId(e.target.value); setPage(1); }}
              className="pl-8 w-[250px]"
            />
          </div>
        </div>
      </div>

      {pageState === "empty" ? (
        <div className="flex flex-col items-center justify-center gap-2 border border-dashed rounded-md py-16 text-muted-foreground">
          <p className="font-medium text-sm">No recommendations found</p>
          <p className="text-xs">Try adjusting filters or refresh later.</p>
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
            Previous
          </Button>
          <span className="text-sm font-mono">
            Page {page} of {Math.ceil(total / 10)}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => Math.min(Math.ceil(total / 10), p + 1))}
            disabled={page === Math.ceil(total / 10)}
          >
            Next
          </Button>
        </div>
      )}

      <Dialog open={detailId !== null} onOpenChange={(open) => { if (!open) setDetailId(null); }}>
        <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Recommendation Detail</DialogTitle>
            <DialogDescription>
              Details of candidate tasks evaluated and the final decision breakdown.
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
                  <span className="text-muted-foreground block text-xs">Recommendation ID</span>
                  <span className="font-mono text-xs">{detail.id}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block text-xs">User ID</span>
                  <span className="font-mono text-xs">{detail.userId}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block text-xs">Status</span>
                  <span className="font-medium">{detail.status}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block text-xs">Trace ID</span>
                  <span className="font-mono text-xs">{detail.traceId ?? "--"}</span>
                </div>
              </div>

              <div>
                <h3 className="text-sm font-semibold mb-3">Evaluated Candidates</h3>
                <div className="border rounded-md overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-muted text-muted-foreground text-xs uppercase">
                      <tr>
                        <th className="p-3 text-left">Task Type</th>
                        <th className="p-3 text-left">Task ID</th>
                        <th className="p-3 text-left">Operation</th>
                        <th className="p-3 text-right">Distance Score (45)</th>
                        <th className="p-3 text-right">Age Score (20)</th>
                        <th className="p-3 text-right">Priority (20)</th>
                        <th className="p-3 text-right">Continuity (15)</th>
                        <th className="p-3 text-right">Penalty</th>
                        <th className="p-3 text-right font-bold">Total Score</th>
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
    </div>
  );
}
