"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getObservabilitySummary, getTimeline, getAlerts } from "@/features/observability/api";
import { ObservabilitySummary, ActivityTimelineEntry, OperationalAlert } from "@/features/observability/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { showError } from "@/lib/toast";
import { AlertTriangle, RefreshCw, Layers, ArrowRight } from "lucide-react";

export default function ObservabilityDashboardPage() {
  const [summary, setSummary] = useState<ObservabilitySummary | null>(null);
  const [timeline, setTimeline] = useState<ActivityTimelineEntry[]>([]);
  const [alerts, setAlerts] = useState<OperationalAlert[]>([]);
  const [loading, setLoading] = useState(false);
  const [period, setPeriod] = useState<"today" | "week">("today");
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      try {
        const now = new Date();
        const fromDate = new Date();
        if (period === "today") {
          fromDate.setHours(0, 0, 0, 0);
        } else {
          fromDate.setDate(now.getDate() - 7);
        }

        const fromIso = fromDate.toISOString();
        const toIso = now.toISOString();

        const sumData = await getObservabilitySummary({ from: fromIso, to: toIso });
        if (active) setSummary(sumData);

        const timelineData = await getTimeline({ page: 1, pageSize: 5 });
        if (active) setTimeline(timelineData.items);

        const alertsData = await getAlerts({ status: "open", page: 1, pageSize: 5 });
        if (active) setAlerts(alertsData.items);
      } catch {
        showError("Không thể tải thông tin giám sát vận hành.");
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [period, refreshTrigger]);

  const loadData = () => {
    setRefreshTrigger(prev => prev + 1);
  };

  const getMetricIcon = (key: string) => {
    if (key.startsWith("webhook")) return <RefreshCw className="h-4 w-4 text-emerald-400" />;
    if (key.startsWith("exception")) return <AlertTriangle className="h-4 w-4 text-amber-500" />;
    return <Layers className="h-4 w-4 text-blue-400" />;
  };

  const formatValue = (val: number, key: string) => {
    if (key.includes("Rate")) return `${val}%`;
    if (key.includes("Minutes")) return `${val} min`;
    return val.toLocaleString("vi-VN");
  };

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-white">Giám sát vận hành</h1>
          <p className="text-zinc-400 text-sm mt-1">Theo dõi sức khỏe, dòng thời gian nghiệp vụ và chỉ số KPI thời gian thực.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant={period === "today" ? "default" : "outline"}
            size="sm"
            onClick={() => setPeriod("today")}
            className="rounded-lg"
          >
            Hôm nay
          </Button>
          <Button
            variant={period === "week" ? "default" : "outline"}
            size="sm"
            onClick={() => setPeriod("week")}
            className="rounded-lg"
          >
            7 ngày qua
          </Button>
          <Button variant="outline" size="sm" onClick={loadData} className="ml-2 rounded-lg">
            Refresh
          </Button>
        </div>
      </div>

      {/* Alert Bar */}
      {summary && summary.activeAlerts > 0 && (
        <div className="flex items-center justify-between p-4 bg-red-950/30 border border-red-900/50 rounded-xl">
          <div className="flex items-center gap-3">
            <AlertTriangle className="h-5 w-5 text-red-500 animate-pulse" />
            <div>
              <p className="text-sm font-semibold text-red-200">
                Phát hiện {summary.activeAlerts} cảnh báo vận hành chưa xử lý
              </p>
              <p className="text-xs text-red-400 mt-0.5">Yêu cầu quản trị viên kiểm tra ngay lập tức.</p>
            </div>
          </div>
          <Link href="/admin/observability/alerts">
            <Button size="sm" variant="destructive" className="gap-2 rounded-lg">
              Xem cảnh báo <ArrowRight className="h-3.5 w-3.5" />
            </Button>
          </Link>
        </div>
      )}

      {/* KPI Cards Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {loading && !summary ? (
          Array.from({ length: 8 }).map((_, idx) => (
            <Card key={idx} className="border-zinc-800/80 bg-zinc-950/40 animate-pulse h-28" />
          ))
        ) : (
          summary?.cards.map((card) => (
            <Card key={card.metricKey} className="border-zinc-800/80 bg-[#0f0f11]/60 hover:bg-[#151518]/70 transition-all duration-200 group rounded-xl">
              <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
                <span className="text-xs font-medium text-zinc-400 group-hover:text-zinc-300 transition-colors">
                  {card.label}
                </span>
                {getMetricIcon(card.metricKey)}
              </CardHeader>
              <CardContent>
                {card.trend === "unavailable" ? (
                  <span className="text-sm text-zinc-500 font-medium italic">Không khả dụng</span>
                ) : (
                  <div className="flex items-baseline gap-2">
                    <span className="text-2xl font-bold tracking-tight text-white">
                      {formatValue(card.value, card.metricKey)}
                    </span>
                    {card.trend === "stale" && (
                      <Badge variant="outline" className="text-[10px] text-amber-400 border-amber-400/20 bg-amber-400/5">
                        stale
                      </Badge>
                    )}
                  </div>
                )}
                <p className="text-[10px] text-zinc-500 mt-1 font-mono">
                  {card.metricKey}
                </p>
              </CardContent>
            </Card>
          ))
        )}
      </div>

      {/* Two columns: Active Alerts & Recent Timeline */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Active Alerts */}
        <Card className="border-zinc-800/80 bg-[#0f0f11]/40 rounded-xl">
          <CardHeader className="flex flex-row items-center justify-between border-b border-zinc-800/80 pb-4">
            <div>
              <CardTitle className="text-lg font-semibold text-white">Cảnh báo đang hoạt động</CardTitle>
              <p className="text-xs text-zinc-400 mt-0.5">Các cảnh báo nghiêm trọng trong kỳ.</p>
            </div>
            <Link href="/admin/observability/alerts">
              <Button size="xs" variant="ghost" className="text-xs text-emerald-400 hover:text-emerald-300 gap-1 p-0 hover:bg-transparent">
                Tất cả <ArrowRight className="h-3 w-3" />
              </Button>
            </Link>
          </CardHeader>
          <CardContent className="pt-4">
            {loading ? (
              <p className="text-sm text-zinc-500">Đang tải...</p>
            ) : alerts.length === 0 ? (
              <p className="text-sm text-zinc-500 italic py-4">Không có cảnh báo nào đang hoạt động.</p>
            ) : (
              <div className="space-y-3">
                {alerts.map((a) => (
                  <div key={a.id} className="p-3 border border-zinc-800 bg-zinc-950/20 rounded-lg flex items-start justify-between gap-3">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <Badge variant={a.severity === "critical" ? "destructive" : "secondary"}>
                          {a.severity}
                        </Badge>
                        <span className="font-semibold text-zinc-200 text-sm">{a.title}</span>
                      </div>
                      <p className="text-xs text-zinc-400 leading-relaxed">{a.message}</p>
                      <div className="text-[10px] text-zinc-500 flex items-center gap-2">
                        <span>Module: {a.sourceModule}</span>
                        <span>•</span>
                        <span>{new Date(a.createdAt).toLocaleString("vi-VN")}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Recent Timeline */}
        <Card className="border-zinc-800/80 bg-[#0f0f11]/40 rounded-xl">
          <CardHeader className="flex flex-row items-center justify-between border-b border-zinc-800/80 pb-4">
            <div>
              <CardTitle className="text-lg font-semibold text-white">Hoạt động gần đây</CardTitle>
              <p className="text-xs text-zinc-400 mt-0.5">Dòng thời gian hoạt động của hệ thống.</p>
            </div>
            <Link href="/admin/observability/timeline">
              <Button size="xs" variant="ghost" className="text-xs text-emerald-400 hover:text-emerald-300 gap-1 p-0 hover:bg-transparent">
                Tất cả <ArrowRight className="h-3 w-3" />
              </Button>
            </Link>
          </CardHeader>
          <CardContent className="pt-4">
            {loading ? (
              <p className="text-sm text-zinc-500">Đang tải...</p>
            ) : timeline.length === 0 ? (
              <p className="text-sm text-zinc-500 italic py-4">Không có hoạt động nào được ghi nhận.</p>
            ) : (
              <div className="space-y-4">
                {timeline.map((t) => (
                  <div key={t.id} className="relative pl-6 border-l-2 border-zinc-800 last:border-0 pb-2">
                    <div className="absolute left-[-5px] top-1.5 h-2.5 w-2.5 rounded-full bg-emerald-500" />
                    <div className="space-y-1">
                      <div className="flex items-center justify-between">
                        <span className="font-semibold text-zinc-200 text-sm">{t.title}</span>
                        <span className="text-[10px] text-zinc-500">
                          {new Date(t.createdAt).toLocaleTimeString("vi-VN")}
                        </span>
                      </div>
                      {t.description && <p className="text-xs text-zinc-400">{t.description}</p>}
                      <div className="text-[10px] text-zinc-500 flex gap-2">
                        <span className="font-mono">{t.entityType}</span>
                        <span>•</span>
                        <span className="font-mono">Trace: {t.traceId.slice(0, 10)}...</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Footer Trace ID reference */}
      {summary && (
        <div className="text-[10px] text-zinc-500 font-mono text-right">
          Trace ID: {summary.traceId}
        </div>
      )}
    </div>
  );
}
