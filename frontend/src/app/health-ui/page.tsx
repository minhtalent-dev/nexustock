"use client";

import { useEffect, useState } from "react";
import axios from "axios";
import { Activity, Database, RefreshCw, Server, CheckCircle2, AlertTriangle, XCircle, Heart } from "lucide-react";
import { Button } from "@/components/ui/button";
import BreadcrumbNav from "@/components/breadcrumb-nav";

interface HealthSummary {
  status: string;
  version: string;
  environment: string;
  services: {
    api: string;
    database: string;
    redis: string;
  };
  traceId: string;
}

export default function HealthUi() {
  const [data, setData] = useState<HealthSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [mounted, setMounted] = useState(false);

  const fetchHealth = async () => {
    try {
      setError(null);
      const apiUrl = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5024";
      const response = await axios.get<HealthSummary>(`${apiUrl}/api/system/health-summary`, {
        timeout: 5000
      });
      setData(response.data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to fetch health status from API";
      setError(message);
      setData(null);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    setMounted(true);
    const timeout = window.setTimeout(fetchHealth, 0);
    const interval = window.setInterval(fetchHealth, 10000);

    return () => {
      window.clearTimeout(timeout);
      window.clearInterval(interval);
    };
  }, []);

  const handleRefresh = () => {
    setRefreshing(true);
    fetchHealth();
  };

  const getStatusIcon = (status: string) => {
    switch (status?.toLowerCase()) {
      case "healthy":
      case "enabled":
        return <CheckCircle2 className="h-5 w-5 text-emerald-500 animate-pulse" />;
      case "pending":
      case "unhealthy":
        return <AlertTriangle className="h-5 w-5 text-yellow-500" />;
      case "disabled":
        return <XCircle className="h-5 w-5 text-zinc-500" />;
      default:
        return <XCircle className="h-5 w-5 text-red-500" />;
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status?.toLowerCase()) {
      case "healthy":
      case "enabled":
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
            Healthy
          </span>
        );
      case "pending":
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-yellow-500/10 text-yellow-400 border border-yellow-500/20">
            Pending
          </span>
        );
      case "disabled":
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-zinc-800 text-zinc-400 border border-zinc-700">
            Disabled
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-500/10 text-red-400 border border-red-500/20">
            Unhealthy
          </span>
        );
    }
  };

  return (
    <main className="flex-1 bg-[#0a0a0a] text-zinc-100 flex flex-col items-center justify-center p-6 md:p-12 font-sans selection:bg-emerald-500 selection:text-black">
      <div className="w-full max-w-4xl flex flex-col gap-8">
        <BreadcrumbNav />
        
        {/* Top Header */}
        <header className="flex justify-between items-center border-b border-zinc-800/80 pb-6">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-emerald-500/10 border border-emerald-500/25 flex items-center justify-center">
              <Activity className="h-5 w-5 text-emerald-500" />
            </div>
            <div>
              <h1 className="text-2xl font-bold tracking-tight text-white">System health status</h1>
              <p className="text-sm text-zinc-400">Nexustock modular monolith dashboard</p>
            </div>
          </div>
          <Button
            onClick={handleRefresh}
            disabled={refreshing || loading}
            variant="outline"
          >
            <RefreshCw className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`} />
            Refresh
          </Button>
        </header>

        {/* Overview Status Grid */}
        <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
          
          {/* API Web Host */}
          <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col justify-between min-h-[140px] hover:border-zinc-700 transition-colors">
            <div className="flex justify-between items-start">
              <div className="h-10 w-10 rounded-lg bg-blue-500/10 border border-blue-500/20 flex items-center justify-center">
                <Server className="h-5 w-5 text-blue-400" />
              </div>
              {getStatusBadge(error ? "unhealthy" : (data?.services.api || "healthy"))}
            </div>
            <div className="mt-4">
              <h3 className="text-sm font-medium text-zinc-400">API Web Host</h3>
              <p className="text-xs text-zinc-500 mt-1">Composition root & Middleware</p>
            </div>
          </div>

          {/* Database Server */}
          <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col justify-between min-h-[140px] hover:border-zinc-700 transition-colors">
            <div className="flex justify-between items-start">
              <div className="h-10 w-10 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center">
                <Database className="h-5 w-5 text-emerald-400" />
              </div>
              {getStatusBadge(error ? "unhealthy" : (data?.services.database || "pending"))}
            </div>
            <div className="mt-4">
              <h3 className="text-sm font-medium text-zinc-400">PostgreSQL Database</h3>
              <p className="text-xs text-zinc-500 mt-1">Main operational storage</p>
            </div>
          </div>

          {/* Cache Store */}
          <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col justify-between min-h-[140px] hover:border-zinc-700 transition-colors">
            <div className="flex justify-between items-start">
              <div className="h-10 w-10 rounded-lg bg-red-500/10 border border-red-500/20 flex items-center justify-center">
                <Heart className="h-5 w-5 text-red-400" />
              </div>
              {getStatusBadge(error ? "unhealthy" : (data?.services.redis || "disabled"))}
            </div>
            <div className="mt-4">
              <h3 className="text-sm font-medium text-zinc-400">Redis cache</h3>
              <p className="text-xs text-zinc-500 mt-1">Distributed caching (Optional)</p>
            </div>
          </div>

        </section>

        {/* Server Info & Environment */}
        <section className="bg-[#111] border border-zinc-800/60 rounded-xl p-6 flex flex-col gap-4">
          <h2 className="text-lg font-semibold text-white">Metadata details</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            
            <div className="flex justify-between py-2 border-b border-zinc-800/40">
              <span className="text-zinc-400">Environment</span>
              <span className="font-mono text-zinc-300 capitalize">{error ? "Unknown" : (data?.environment || "Development")}</span>
            </div>

            <div className="flex justify-between py-2 border-b border-zinc-800/40">
              <span className="text-zinc-400">API version</span>
              <span className="font-mono text-zinc-300">{error ? "—" : (data?.version || "0.1.0")}</span>
            </div>

            <div className="flex justify-between py-2 border-b border-zinc-800/40 md:border-none">
              <span className="text-zinc-400">Liveness check (/health/live)</span>
              <span className="font-mono text-emerald-400 flex items-center gap-1.5">
                {getStatusIcon(error ? "unhealthy" : "healthy")} Alive
              </span>
            </div>

            <div className="flex justify-between py-2 border-b border-zinc-800/40 md:border-none">
              <span className="text-zinc-400">Readiness check (/health/ready)</span>
              <span className="font-mono text-yellow-400 flex items-center gap-1.5">
                {getStatusIcon(error ? "unhealthy" : "pending")} Active
              </span>
            </div>

          </div>

          {/* Trace ID */}
          <div className="mt-4 pt-4 border-t border-zinc-800/60 flex flex-col sm:flex-row sm:justify-between gap-2 text-xs text-zinc-500">
            <span>Trace identifier: <span className="font-mono text-zinc-400">{error ? "N/A" : (data?.traceId || "—")}</span></span>
            <span>Last checked: {mounted ? new Date().toLocaleTimeString() : "—"}</span>
          </div>

        </section>

        {/* Global Error Banner */}
        {error && (
          <section className="bg-red-500/10 border border-red-500/25 rounded-xl p-4 flex gap-3 items-center">
            <XCircle className="h-5 w-5 text-red-500 shrink-0" />
            <div>
              <h4 className="text-sm font-semibold text-red-400">API connection error</h4>
              <p className="text-xs text-red-300/80 mt-0.5">{error}</p>
            </div>
          </section>
        )}

      </div>
    </main>
  );
}
