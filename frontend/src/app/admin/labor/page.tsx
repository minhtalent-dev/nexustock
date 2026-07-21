"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import { laborApi, LaborKpiResponse, LaborKpiChartResponse } from "@/lib/labor-api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { RefreshCw, Clock, CheckCircle, BarChart3, AlertCircle } from "lucide-react";
import {
  LaborThroughputTrendChart,
  LaborTasksPerHourTrendChart,
  LaborOperationMixChart,
  LaborUserProductivityChart,
  LaborZoneProductivityGrid,
} from "./components/labor-charts";

const OPERATION_FILTER_KEYS = ["ALL", "PICKING", "PUTAWAY", "PACKING", "RECEIVING", "COUNTING"] as const;

export default function LaborDashboardPage() {
  const t = useTranslations("Admin.labor");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [loading, setLoading] = React.useState(false);
  const [kpi, setKpi] = React.useState<LaborKpiResponse | null>(null);
  const [charts, setCharts] = React.useState<LaborKpiChartResponse | null>(null);

  const [userId, setUserId] = React.useState("");
  const [shiftId, setShiftId] = React.useState("");
  const [zoneId, setZoneId] = React.useState("");
  const [operationType, setOperationType] = React.useState("ALL");
  const [fromDate, setFromDate] = React.useState("");
  const [toDate, setToDate] = React.useState("");

  const loadData = React.useCallback(async () => {
    setLoading(true);
    try {
      const queryParams = {
        userId: userId.trim() || undefined,
        shiftId: shiftId.trim() || undefined,
        zoneId: zoneId.trim() || undefined,
        operationType: operationType === "ALL" ? undefined : operationType,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
      };

      const [kpiRes, chartsRes] = await Promise.all([
        laborApi.getKpi(queryParams),
        laborApi.getKpiCharts(queryParams),
      ]);

      setKpi(kpiRes);
      setCharts(chartsRes);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [userId, shiftId, zoneId, operationType, fromDate, toDate, t, tErrors]);

  React.useEffect(() => {
    queueMicrotask(() => void loadData());
  }, [loadData]);

  const handleResetFilters = () => {
    setUserId("");
    setShiftId("");
    setZoneId("");
    setOperationType("ALL");
    setFromDate("");
    setToDate("");
  };

  const formatDuration = (seconds: number) => {
    if (seconds <= 0) return t("durationZero");
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return t("durationFormat", { hours: h, minutes: m });
  };

  const summary = kpi?.summary;

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground">{t("dashboardTitle")}</h1>
          <p className="text-muted-foreground text-sm">{t("dashboardSubtitle")}</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={loadData} disabled={loading} className="gap-2">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            {tc("refresh")}
          </Button>
        </div>
      </div>

      <Card className="bg-card/50 backdrop-blur-md border border-accent/20">
        <CardHeader className="py-3">
          <CardTitle className="text-sm font-semibold flex items-center gap-2">
            <BarChart3 className="h-4 w-4 text-primary" /> {t("filterTitle")}
          </CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-6 items-end pb-4">
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">{t("userId")}</label>
            <Input
              type="text"
              placeholder={t("filterUserPlaceholder")}
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              className="h-9"
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">{t("shiftId")}</label>
            <Input
              type="text"
              placeholder={t("filterShiftPlaceholder")}
              value={shiftId}
              onChange={(e) => setShiftId(e.target.value)}
              className="h-9"
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">{t("zoneId")}</label>
            <Input
              type="text"
              placeholder={t("filterZonePlaceholder")}
              value={zoneId}
              onChange={(e) => setZoneId(e.target.value)}
              className="h-9"
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">{t("operation")}</label>
            <Select value={operationType} onValueChange={setOperationType}>
              <SelectTrigger className="h-9">
                <SelectValue placeholder={t("allOperations")} />
              </SelectTrigger>
              <SelectContent>
                {OPERATION_FILTER_KEYS.map((key) => (
                  <SelectItem key={key} value={key}>
                    {t(`operations.${key}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">{t("fromDate")}</label>
            <Input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="h-9 text-xs"
            />
          </div>
          <div className="space-y-2 flex gap-2 w-full">
            <div className="flex-1">
              <label className="text-xs font-semibold text-muted-foreground">{t("toDate")}</label>
              <Input
                type="date"
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="h-9 text-xs"
              />
            </div>
            <Button variant="outline" size="icon" onClick={handleResetFilters} className="h-9 w-9 self-end" title={t("resetFilters")}>
              ×
            </Button>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-6">
        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">{t("completedTasks")}</CardTitle>
            <CheckCircle className="h-4 w-4 text-emerald-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{summary?.completedTaskCount ?? 0}</div>
            <p className="text-[10px] text-muted-foreground">{t("completedTasksHint")}</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">{t("activeTime")}</CardTitle>
            <Clock className="h-4 w-4 text-primary" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatDuration(summary?.activeSeconds ?? 0)}</div>
            <p className="text-[10px] text-muted-foreground">{t("activeTimeHint")}</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">{t("pausedTime")}</CardTitle>
            <Clock className="h-4 w-4 text-amber-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatDuration(summary?.pausedSeconds ?? 0)}</div>
            <p className="text-[10px] text-muted-foreground">{t("pausedTimeHint")}</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">{t("avgTimePerTask")}</CardTitle>
            <Clock className="h-4 w-4 text-sky-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{(summary?.averageSecondsPerTask ?? 0).toFixed(0)}s</div>
            <p className="text-[10px] text-muted-foreground">{t("avgTimePerTaskHint")}</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">{t("tasksPerHour")}</CardTitle>
            <BarChart3 className="h-4 w-4 text-purple-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{(summary?.tasksPerHour ?? 0).toFixed(1)}</div>
            <p className="text-[10px] text-muted-foreground">{t("tasksPerHourHint")}</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">{t("idleTime")}</CardTitle>
            <AlertCircle className="h-4 w-4 text-red-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatDuration(summary?.idleSeconds ?? 0)}</div>
            <p className="text-[10px] text-muted-foreground">{t("idleTimeHint")}</p>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 md:grid-cols-6">
        <LaborThroughputTrendChart data={charts?.throughputTrend ?? []} loading={loading} />
        <LaborTasksPerHourTrendChart data={charts?.tasksPerHourTrend ?? []} loading={loading} />
        <LaborOperationMixChart data={charts?.operationMix ?? []} loading={loading} />
        <LaborUserProductivityChart data={charts?.userProductivityRanking ?? []} loading={loading} />
        <LaborZoneProductivityGrid data={charts?.zoneProductivity ?? []} loading={loading} />
      </div>
    </div>
  );
}
