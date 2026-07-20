"use client";

import * as React from "react";
import { laborApi, LaborKpiResponse, LaborKpiChartResponse } from "@/lib/labor-api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { showError } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { RefreshCw, Clock, CheckCircle, BarChart3, AlertCircle } from "lucide-react";
import {
  LaborThroughputTrendChart,
  LaborTasksPerHourTrendChart,
  LaborOperationMixChart,
  LaborUserProductivityChart,
  LaborZoneProductivityGrid,
} from "./components/labor-charts";

export default function LaborDashboardPage() {
  const [loading, setLoading] = React.useState(false);
  const [kpi, setKpi] = React.useState<LaborKpiResponse | null>(null);
  const [charts, setCharts] = React.useState<LaborKpiChartResponse | null>(null);

  // Filters state
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
      showError(getHttpErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [userId, shiftId, zoneId, operationType, fromDate, toDate]);

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
    if (seconds <= 0) return "0h 0m";
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return `${h}h ${m}m`;
  };

  const summary = kpi?.summary;

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground">Labor Tracking Dashboard</h1>
          <p className="text-muted-foreground text-sm">Monitor warehouse operational speed, productivity, and idle metrics.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={loadData} disabled={loading} className="gap-2">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Filter Card */}
      <Card className="bg-card/50 backdrop-blur-md border border-accent/20">
        <CardHeader className="py-3">
          <CardTitle className="text-sm font-semibold flex items-center gap-2">
            <BarChart3 className="h-4 w-4 text-primary" /> Filter Metrics
          </CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-6 items-end pb-4">
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">User ID</label>
            <Input
              type="text"
              placeholder="Filter User..."
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              className="h-9"
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">Shift ID</label>
            <Input
              type="text"
              placeholder="Filter Shift..."
              value={shiftId}
              onChange={(e) => setShiftId(e.target.value)}
              className="h-9"
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">Zone ID</label>
            <Input
              type="text"
              placeholder="Filter Zone..."
              value={zoneId}
              onChange={(e) => setZoneId(e.target.value)}
              className="h-9"
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">Operation</label>
            <Select value={operationType} onValueChange={setOperationType}>
              <SelectTrigger className="h-9">
                <SelectValue placeholder="All Operations" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ALL">All Operations</SelectItem>
                <SelectItem value="PICKING">Picking</SelectItem>
                <SelectItem value="PUTAWAY">Putaway</SelectItem>
                <SelectItem value="PACKING">Packing</SelectItem>
                <SelectItem value="RECEIVING">Receiving</SelectItem>
                <SelectItem value="COUNTING">Counting</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold text-muted-foreground">From Date</label>
            <Input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="h-9 text-xs"
            />
          </div>
          <div className="space-y-2 flex gap-2 w-full">
            <div className="flex-1">
              <label className="text-xs font-semibold text-muted-foreground">To Date</label>
              <Input
                type="date"
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="h-9 text-xs"
              />
            </div>
            <Button variant="outline" size="icon" onClick={handleResetFilters} className="h-9 w-9 self-end" title="Reset Filters">
              ×
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* KPI Cards Grid */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-6">
        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Completed Tasks</CardTitle>
            <CheckCircle className="h-4 w-4 text-emerald-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{summary?.completedTaskCount ?? 0}</div>
            <p className="text-[10px] text-muted-foreground">Total items processed</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Active Time</CardTitle>
            <Clock className="h-4 w-4 text-primary" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatDuration(summary?.activeSeconds ?? 0)}</div>
            <p className="text-[10px] text-muted-foreground">Excluding paused sessions</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Paused Time</CardTitle>
            <Clock className="h-4 w-4 text-amber-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatDuration(summary?.pausedSeconds ?? 0)}</div>
            <p className="text-[10px] text-muted-foreground">Temporary worker breaks</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Avg Time/Task</CardTitle>
            <Clock className="h-4 w-4 text-sky-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{(summary?.averageSecondsPerTask ?? 0).toFixed(0)}s</div>
            <p className="text-[10px] text-muted-foreground">Performance per process unit</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Tasks/Hour (TPH)</CardTitle>
            <BarChart3 className="h-4 w-4 text-purple-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{(summary?.tasksPerHour ?? 0).toFixed(1)}</div>
            <p className="text-[10px] text-muted-foreground">Average throughput rate</p>
          </CardContent>
        </Card>

        <Card className="hover:scale-[1.01] transition-transform">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-bold text-muted-foreground uppercase tracking-wider">Idle Time</CardTitle>
            <AlertCircle className="h-4 w-4 text-red-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatDuration(summary?.idleSeconds ?? 0)}</div>
            <p className="text-[10px] text-muted-foreground">Non-job allocation gaps</p>
          </CardContent>
        </Card>
      </div>

      {/* Charts Grid */}
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
