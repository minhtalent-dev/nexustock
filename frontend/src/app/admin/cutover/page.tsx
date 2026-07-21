"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import { readinessApi, CutoverLogDto, FreezeStatusResponse } from "@/lib/readiness-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { RefreshCw, ShieldAlert, Ban, Snowflake, Sun } from "lucide-react";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { isFeatureDisabledError, isUnauthorizedError } from "@/lib/http-error";

type PageState = "loading" | "ready" | "error" | "unauthorized" | "featureDisabled";

export default function CutoverPage() {
  const t = useTranslations("Admin.cutover");
  const tErrors = useTranslations("Errors");

  const [pageState, setPageState] = React.useState<PageState>("loading");
  const [errorMessage, setErrorMessage] = React.useState("");
  const [freeze, setFreeze] = React.useState<FreezeStatusResponse | null>(null);
  const [logs, setLogs] = React.useState<CutoverLogDto[]>([]);
  const [reason, setReason] = React.useState("");

  const loadData = React.useCallback(async () => {
    setPageState("loading");
    setErrorMessage("");
    try {
      const [status, logRes] = await Promise.all([
        readinessApi.getFreezeStatus(),
        readinessApi.listCutoverLogs(1, 50),
      ]);
      setFreeze(status);
      setLogs(logRes.items);
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
      const { codeLabel, message } = resolveApiError(err, tErrors);
      const msg = message || t("errors.loadFailed");
      setErrorMessage(msg);
      setPageState("error");
      showApiErrorToast(codeLabel, msg);
    }
  }, [t, tErrors]);

  React.useEffect(() => {
    queueMicrotask(() => void loadData());
  }, [loadData]);

  const toggleFreeze = async () => {
    try {
      if (freeze?.isFrozen) {
        await readinessApi.unfreeze(reason || undefined);
        showSuccess(t("toastUnfrozen"));
      } else {
        await readinessApi.freeze(reason || undefined);
        showSuccess(t("toastFrozen"));
      }
      setReason("");
      await loadData();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.toggleFailed"));
    }
  };

  if (pageState === "unauthorized") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 p-10 text-muted-foreground">
        <ShieldAlert className="h-10 w-10" />
        <p>{t("unauthorized")}</p>
      </div>
    );
  }

  if (pageState === "featureDisabled") {
    return (
      <div className="flex flex-col items-center justify-center gap-3 p-10 text-muted-foreground">
        <Ban className="h-10 w-10" />
        <p>{t("featureDisabled")}</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{t("title")}</h1>
          <p className="text-sm text-muted-foreground">{t("subtitle")}</p>
        </div>
        <Button variant="outline" onClick={() => void loadData()}>
          <RefreshCw className="mr-2 h-4 w-4" />
          {t("refresh")}
        </Button>
      </div>

      {pageState === "error" && <p className="text-sm text-red-400">{errorMessage}</p>}

      <section className="rounded-lg border border-border/60 bg-card/40 p-4 space-y-3">
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-sm text-muted-foreground">{t("freezeStatus")}</span>
          <span className={`font-semibold ${freeze?.isFrozen ? "text-amber-400" : "text-emerald-400"}`}>
            {freeze?.isFrozen ? t("frozen") : t("open")}
          </span>
          {freeze?.frozenBy ? <span className="text-xs text-muted-foreground">{t("frozenBy", { user: freeze.frozenBy })}</span> : null}
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1">
            <label className="text-xs text-muted-foreground">{t("reason")}</label>
            <Input className="w-72" value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t("reasonPlaceholder")} />
          </div>
          <Button
            data-testid="cutover-freeze-button"
            variant={freeze?.isFrozen ? "secondary" : "default"}
            onClick={() => void toggleFreeze()}
          >
            {freeze?.isFrozen ? (
              <>
                <Sun className="mr-2 h-4 w-4" />
                {t("unfreeze")}
              </>
            ) : (
              <>
                <Snowflake className="mr-2 h-4 w-4" />
                {t("freeze")}
              </>
            )}
          </Button>
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-medium">{t("logsTitle")}</h2>
        <div className="overflow-x-auto rounded-lg border border-border/60">
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-left">
              <tr>
                <th className="p-3">{t("colStep")}</th>
                <th className="p-3">{t("colStatus")}</th>
                <th className="p-3">{t("colActor")}</th>
                <th className="p-3">{t("colStarted")}</th>
                <th className="p-3">{t("colNote")}</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr key={log.id} className="border-t border-border/40">
                  <td className="p-3">{log.stepCode}</td>
                  <td className="p-3">{log.status}</td>
                  <td className="p-3">{log.actor}</td>
                  <td className="p-3">{log.startedAt ? new Date(log.startedAt).toLocaleString() : "—"}</td>
                  <td className="p-3">{log.note ?? "—"}</td>
                </tr>
              ))}
              {logs.length === 0 ? (
                <tr>
                  <td className="p-3 text-muted-foreground" colSpan={5}>
                    {t("emptyLogs")}
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
