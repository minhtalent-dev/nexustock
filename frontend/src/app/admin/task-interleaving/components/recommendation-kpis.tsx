import * as React from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { TaskInterleavingKpiResponse } from "@/lib/task-interleaving-api";
import { CheckCircle2, XCircle, Ban, TrendingUp, Hourglass, ShieldAlert, Layers } from "lucide-react";

type KpisProps = {
  kpis: TaskInterleavingKpiResponse | null;
};

export function RecommendationKpis({ kpis }: KpisProps) {
  const formatPercent = (val: number) => `${(val * 100).toFixed(1)}%`;

  return (
    <div className="grid gap-4 md:grid-cols-4 lg:grid-cols-7">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Accept Rate</CardTitle>
          <CheckCircle2 className="size-4 text-emerald-500" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? formatPercent(kpis.acceptRate) : "--"}</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Reject Rate</CardTitle>
          <XCircle className="size-4 text-rose-500" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? formatPercent(kpis.rejectRate) : "--"}</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">No Task Rate</CardTitle>
          <Ban className="size-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? formatPercent(kpis.noCandidateRate) : "--"}</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Avg Score</CardTitle>
          <TrendingUp className="size-4 text-indigo-500" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? kpis.averageSelectedScore.toFixed(1) : "--"}</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Avg Decision</CardTitle>
          <Hourglass className="size-4 text-amber-500" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? `${kpis.averageDecisionSeconds.toFixed(1)}s` : "--"}</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Conflict Rate</CardTitle>
          <ShieldAlert className="size-4 text-amber-600" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? formatPercent(kpis.conflictRate) : "--"}</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Same Zone</CardTitle>
          <Layers className="size-4 text-blue-500" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{kpis ? formatPercent(kpis.sameZoneSuggestionRate) : "--"}</div>
        </CardContent>
      </Card>
    </div>
  );
}
