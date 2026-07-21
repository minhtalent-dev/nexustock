"use client";

import * as React from "react";
import { readinessApi, ReadinessProbeResponse, UatRunDto } from "@/lib/readiness-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { RefreshCw, ShieldAlert, Ban } from "lucide-react";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage, isFeatureDisabledError, isUnauthorizedError } from "@/lib/http-error";

type PageState = "loading" | "ready" | "error" | "unauthorized" | "featureDisabled";

function statusColor(status: string) {
  const s = status.toLowerCase();
  if (s === "up" || s === "ready" || s === "passed" || s === "signedoff") return "text-emerald-400";
  if (s === "skipped" || s === "degraded") return "text-amber-400";
  if (s === "down" || s === "notready" || s === "failed") return "text-red-400";
  return "text-muted-foreground";
}

export default function ReadinessPage() {
  const [pageState, setPageState] = React.useState<PageState>("loading");
  const [errorMessage, setErrorMessage] = React.useState("");
  const [probe, setProbe] = React.useState<ReadinessProbeResponse | null>(null);
  const [uatRuns, setUatRuns] = React.useState<UatRunDto[]>([]);
  const [scenario, setScenario] = React.useState("INBOUND");
  const [uatStatus, setUatStatus] = React.useState("Passed");
  const [drillScenario, setDrillScenario] = React.useState("DB_DOWN");
  const [rtoMinutes, setRtoMinutes] = React.useState("30");

  const loadData = React.useCallback(async () => {
    setPageState("loading");
    setErrorMessage("");
    try {
      const [probeRes, uatRes] = await Promise.all([
        readinessApi.getProbe(),
        readinessApi.listUatRuns(1, 20),
      ]);
      setProbe(probeRes);
      setUatRuns(uatRes.items);
      setPageState("ready");
    } catch (err) {
      if (isFeatureDisabledError(err)) {
        setPageState("featureDisabled");
        return;
      }
      if (isUnauthorizedError(err)) {
        setPageState("unauthorized");
        return;
      }
      const msg = getHttpErrorMessage(err, "Failed to load readiness data.");
      setErrorMessage(msg);
      setPageState("error");
      showError(msg);
    }
  }, []);

  React.useEffect(() => {
    queueMicrotask(() => void loadData());
  }, [loadData]);

  const handleCreateUat = async () => {
    try {
      await readinessApi.createUatRun({ scenarioCode: scenario, status: uatStatus });
      showSuccess("UAT run created.");
      await loadData();
    } catch (err) {
      showError(getHttpErrorMessage(err, "Failed to create UAT run."));
    }
  };

  const handleSignoff = async (id: string) => {
    try {
      await readinessApi.signoffUatRun(id);
      showSuccess("UAT signed off.");
      await loadData();
    } catch (err) {
      showError(getHttpErrorMessage(err, "Failed to sign off UAT."));
    }
  };

  const handleDrill = async () => {
    try {
      await readinessApi.createIncidentDrill({
        scenarioCode: drillScenario,
        rtoMinutes: Number(rtoMinutes) || 1,
        passed: true,
      });
      showSuccess("Incident drill recorded.");
    } catch (err) {
      showError(getHttpErrorMessage(err, "Failed to record drill."));
    }
  };

  if (pageState === "unauthorized") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 p-10 text-muted-foreground">
        <ShieldAlert className="h-10 w-10" />
        <p>Unauthorized — missing readiness.read</p>
      </div>
    );
  }

  if (pageState === "featureDisabled") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 p-10 text-muted-foreground">
        <Ban className="h-10 w-10" />
        <p>Readiness gate is disabled by feature flag.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Readiness</h1>
          <p className="text-sm text-muted-foreground">System probe, UAT runs, and incident drills.</p>
        </div>
        <Button data-testid="readiness-refresh-button" variant="outline" onClick={() => void loadData()}>
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </div>

      {pageState === "error" && <p className="text-sm text-red-400">{errorMessage}</p>}

      {probe && (
        <section className="space-y-3">
          <div className="flex items-center gap-2">
            <h2 className="text-lg font-medium">Overall</h2>
            <span className={`text-sm font-semibold ${statusColor(probe.overallStatus)}`}>{probe.overallStatus}</span>
          </div>
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {probe.components.map((c) => (
              <div key={c.name} className="rounded-lg border border-border/60 bg-card/40 p-4">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{c.name}</span>
                  <span className={`text-sm ${statusColor(c.status)}`}>{c.status}</span>
                </div>
                {c.detail ? <p className="mt-2 text-xs text-muted-foreground">{c.detail}</p> : null}
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="space-y-3">
        <h2 className="text-lg font-medium">UAT runs</h2>
        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1">
            <label className="text-xs text-muted-foreground">Scenario</label>
            <Select value={scenario} onValueChange={setScenario}>
              <SelectTrigger className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="INBOUND">INBOUND</SelectItem>
                <SelectItem value="QC">QC</SelectItem>
                <SelectItem value="PACK_SCALE">PACK_SCALE</SelectItem>
                <SelectItem value="PRINT_ERROR">PRINT_ERROR</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <label className="text-xs text-muted-foreground">Status</label>
            <Select value={uatStatus} onValueChange={setUatStatus}>
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Running">Running</SelectItem>
                <SelectItem value="Passed">Passed</SelectItem>
                <SelectItem value="Failed">Failed</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <Button onClick={() => void handleCreateUat()}>Create UAT</Button>
        </div>

        <div className="overflow-x-auto rounded-lg border border-border/60">
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-left">
              <tr>
                <th className="p-3">Scenario</th>
                <th className="p-3">Status</th>
                <th className="p-3">Signed off</th>
                <th className="p-3">Actions</th>
              </tr>
            </thead>
            <tbody>
              {uatRuns.map((run) => (
                <tr key={run.id} className="border-t border-border/40">
                  <td className="p-3">{run.scenarioCode}</td>
                  <td className={`p-3 ${statusColor(run.status)}`}>{run.status}</td>
                  <td className="p-3">{run.signedOffBy ?? "—"}</td>
                  <td className="p-3">
                    {run.status.toLowerCase() === "passed" ? (
                      <Button data-testid="uat-signoff-button" size="sm" variant="outline" onClick={() => void handleSignoff(run.id)}>
                        Sign off
                      </Button>
                    ) : (
                      "—"
                    )}
                  </td>
                </tr>
              ))}
              {uatRuns.length === 0 ? (
                <tr>
                  <td className="p-3 text-muted-foreground" colSpan={4}>
                    No UAT runs yet.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-medium">Incident drill</h2>
        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1">
            <label className="text-xs text-muted-foreground">Scenario</label>
            <Select value={drillScenario} onValueChange={setDrillScenario}>
              <SelectTrigger className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="DB_DOWN">DB_DOWN</SelectItem>
                <SelectItem value="AGENT_DOWN">AGENT_DOWN</SelectItem>
                <SelectItem value="SAP_DOWN">SAP_DOWN</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <label className="text-xs text-muted-foreground">RTO minutes</label>
            <Input className="w-28" value={rtoMinutes} onChange={(e) => setRtoMinutes(e.target.value)} />
          </div>
          <Button variant="secondary" onClick={() => void handleDrill()}>
            Record drill
          </Button>
        </div>
      </section>
    </div>
  );
}
