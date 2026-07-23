"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { getObservabilitySummary, getTimeline, getAlerts } from "@/features/observability/api";
import { ObservabilitySummary, ActivityTimelineEntry, OperationalAlert } from "@/features/observability/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { AlertTriangle, RefreshCw, Layers, ArrowRight } from "lucide-react";

export default function ObservabilityDashboardPage() {
  const t = useTranslations("Admin.observability");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

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
      } catch (err) {
        const { codeLabel, message } = resolveApiError(err, tErrors);
        showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [period, refreshTrigger, t, tErrors]);

  const loadData = () => {
    setRefreshTrigger((prev) => prev + 1);
  };

  const getMetricIcon = (key: string) => {
    if (key.startsWith("webhook")) return <RefreshCw className="h-4 w-4 text-emerald-400" />;
    if (key.startsWith("exception")) return <AlertTriangle className="h-4 w-4 text-amber-500" />;
    return <Layers className="h-4 w-4 text-blue-400" />;
  };

  const formatValue = (val: number, key: string) => {
    if (key.includes("Rate")) return `${val}%`;
    if (key.includes("Minutes")) return `${val} min`;
    return val.toLocaleString();
  };

  return (
    <PageShell className="gap-6">
      <div className="p-6 space-y-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground">{t("title")}</h1>
          <p className="text-muted-foreground text-sm mt-1">{t("subtitle")}</p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant={period === "today" ? "default" : "outline"}
            size="sm"
            onClick={() => setPeriod("today")}
            className="rounded-lg"
          >
            {t("periodToday")}
          </Button>
          <Button
            variant={period === "week" ? "default" : "outline"}
            size="sm"
            onClick={() => setPeriod("week")}
            className="rounded-lg"
          >
            {t("periodWeek")}
          </Button>
          <Button variant="outline" size="sm" onClick={loadData} className="ml-2 rounded-lg">
            {tc("refresh")}
          </Button>
        </div>
      </div>

      {summary && summary.activeAlerts > 0 && (
        <div className="flex items-center justify-between p-4 bg-red-950/30 border border-red-900/50 rounded-xl">
          <div className="flex items-center gap-3">
            <AlertTriangle className="h-5 w-5 text-red-500 animate-pulse" />
            <div>
              <p className="text-sm font-semibold text-red-200">
                {t("alertBarTitle", { count: summary.activeAlerts })}
              </p>
              <p className="text-xs text-red-400 mt-0.5">{t("alertBarSubtitle")}</p>
            </div>
          </div>
          <Link href="/admin/observability/alerts">
            <Button size="sm" variant="destructive" className="gap-2 rounded-lg">
              {t("viewAlerts")} <ArrowRight className="h-3.5 w-3.5" />
            </Button>
          </Link>
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {loading && !summary ? (
          Array.from({ length: 8 }).map((_, idx) => (
            <Card key={idx} className="border-border/80 bg-background/40 animate-pulse h-28" />
          ))
        ) : (
          summary?.cards.map((card) => (
            <Card key={card.metricKey} className="border-border/80 bg-[#0f0f11]/60 hover:bg-[#151518]/70 transition-all duration-200 group rounded-xl">
              <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
                <span className="text-xs font-medium text-muted-foreground group-hover:text-muted-foreground transition-colors">
                  {card.label}
                </span>
                {getMetricIcon(card.metricKey)}
              </CardHeader>
              <CardContent>
                {card.trend === "unavailable" ? (
                  <span className="text-sm text-muted-foreground font-medium italic">{tc("unavailable")}</span>
                ) : (
                  <div className="flex items-baseline gap-2">
                    <span className="text-2xl font-bold tracking-tight text-foreground">
                      {formatValue(card.value, card.metricKey)}
                    </span>
                    {card.trend === "stale" && (
                      <Badge variant="outline" className="text-[10px] text-amber-400 border-amber-400/20 bg-amber-400/5">
                        {t("stale")}
                      </Badge>
                    )}
                  </div>
                )}
                <p className="text-[10px] text-muted-foreground mt-1 font-mono">{card.metricKey}</p>
              </CardContent>
            </Card>
          ))
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card className="border-border/80 bg-[#0f0f11]/40 rounded-xl">
          <CardHeader className="flex flex-row items-center justify-between border-b border-border/80 pb-4">
            <div>
              <CardTitle className="text-lg font-semibold text-foreground">{t("activeAlertsTitle")}</CardTitle>
              <p className="text-xs text-muted-foreground mt-0.5">{t("activeAlertsSubtitle")}</p>
            </div>
            <Link href="/admin/observability/alerts">
              <Button size="xs" variant="ghost" className="text-xs text-emerald-400 hover:text-emerald-300 gap-1 p-0 hover:bg-transparent">
                {t("viewAll")} <ArrowRight className="h-3 w-3" />
              </Button>
            </Link>
          </CardHeader>
          <CardContent className="pt-4">
            {loading ? (
              <p className="text-sm text-muted-foreground">{tc("loading")}</p>
            ) : alerts.length === 0 ? (
              <p className="text-sm text-muted-foreground italic py-4">{t("noActiveAlerts")}</p>
            ) : (
              <div className="space-y-3">
                {alerts.map((a) => (
                  <div key={a.id} className="p-3 border border-border bg-background/20 rounded-lg flex items-start justify-between gap-3">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <Badge variant={a.severity === "critical" ? "destructive" : "secondary"}>
                          {a.severity}
                        </Badge>
                        <span className="font-semibold text-foreground text-sm">{a.title}</span>
                      </div>
                      <p className="text-xs text-muted-foreground leading-relaxed">{a.message}</p>
                      <div className="text-[10px] text-muted-foreground flex items-center gap-2">
                        <span>{tc("module")}: {a.sourceModule}</span>
                        <span>•</span>
                        <span>{new Date(a.createdAt).toLocaleString()}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="border-border/80 bg-[#0f0f11]/40 rounded-xl">
          <CardHeader className="flex flex-row items-center justify-between border-b border-border/80 pb-4">
            <div>
              <CardTitle className="text-lg font-semibold text-foreground">{t("recentActivityTitle")}</CardTitle>
              <p className="text-xs text-muted-foreground mt-0.5">{t("recentActivitySubtitle")}</p>
            </div>
            <Link href="/admin/observability/timeline">
              <Button size="xs" variant="ghost" className="text-xs text-emerald-400 hover:text-emerald-300 gap-1 p-0 hover:bg-transparent">
                {t("viewAll")} <ArrowRight className="h-3 w-3" />
              </Button>
            </Link>
          </CardHeader>
          <CardContent className="pt-4">
            {loading ? (
              <p className="text-sm text-muted-foreground">{tc("loading")}</p>
            ) : timeline.length === 0 ? (
              <p className="text-sm text-muted-foreground italic py-4">{t("noRecentActivity")}</p>
            ) : (
              <div className="space-y-4">
                {timeline.map((entry) => (
                  <div key={entry.id} className="relative pl-6 border-l-2 border-border last:border-0 pb-2">
                    <div className="absolute left-[-5px] top-1.5 h-2.5 w-2.5 rounded-full bg-emerald-500" />
                    <div className="space-y-1">
                      <div className="flex items-center justify-between">
                        <span className="font-semibold text-foreground text-sm">{entry.title}</span>
                        <span className="text-[10px] text-muted-foreground">
                          {new Date(entry.createdAt).toLocaleTimeString()}
                        </span>
                      </div>
                      {entry.description && <p className="text-xs text-muted-foreground">{entry.description}</p>}
                      <div className="text-[10px] text-muted-foreground flex gap-2">
                        <span className="font-mono">{entry.entityType}</span>
                        <span>•</span>
                        <span className="font-mono">{t("tracePrefix")}: {entry.traceId.slice(0, 10)}...</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {summary && (
        <div className="text-[10px] text-muted-foreground font-mono text-right">
          {tc("traceId")}: {summary.traceId}
        </div>
      )}
    </div>
    </PageShell>
  );
}
