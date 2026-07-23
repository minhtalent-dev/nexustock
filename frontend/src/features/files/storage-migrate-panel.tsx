"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { showError, showSuccess, showWarning } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import {
  cancelMigrateJob,
  dryRunMigrate,
  getActiveMigrateJob,
  getMigrateErrors,
  getMigrateJob,
  purgeMigrateSource,
  resumeMigrateJob,
  startMigrateJob,
  type MigrateDryRun,
  type MigrateJob,
  type MigrateJobError,
} from "@/features/files/storage-migrate-api";

const SOURCE_OPTIONS = ["LOCAL", "AWS_S3", "AZURE_BLOB", "GCS", "CLOUDFLARE_R2", "ALL"];

function isTerminal(status: string) {
  return ["COMPLETED", "COMPLETED_WITH_ERRORS", "FAILED", "CANCELLED"].includes(status);
}

function isActive(status: string) {
  return ["PENDING", "RUNNING", "PAUSED"].includes(status);
}

type Props = {
  activeProvider: string;
};

export function StorageMigratePanel({ activeProvider }: Props) {
  const t = useTranslations("Admin.storage.migrate");
  const [source, setSource] = useState("LOCAL");
  const [dryRun, setDryRun] = useState<MigrateDryRun | null>(null);
  const [job, setJob] = useState<MigrateJob | null>(null);
  const [errors, setErrors] = useState<MigrateJobError[]>([]);
  const [purgeConfirm, setPurgeConfirm] = useState("");
  const [busy, setBusy] = useState(false);

  const refreshErrors = useCallback(async (jobId: string) => {
    try {
      setErrors(await getMigrateErrors(jobId));
    } catch {
      /* ignore */
    }
  }, []);

  useEffect(() => {
    void (async () => {
      try {
        const active = await getActiveMigrateJob();
        if (active) {
          setJob(active);
          if (active.failCount > 0) await refreshErrors(active.jobId);
        }
      } catch {
        /* ignore hydrate */
      }
    })();
  }, [refreshErrors]);

  useEffect(() => {
    if (!job || isTerminal(job.status)) return;
    const timer = setInterval(() => {
      void (async () => {
        try {
          const next = await getMigrateJob(job.jobId);
          setJob(next);
          if (next.failCount > 0) await refreshErrors(next.jobId);
        } catch {
          /* ignore poll */
        }
      })();
    }, 2000);
    return () => clearInterval(timer);
  }, [job, refreshErrors]);

  const onDryRun = async () => {
    setBusy(true);
    try {
      const res = await dryRunMigrate({
        sourceProvider: source === "ALL" ? null : source,
        targetProvider: activeProvider,
      });
      setDryRun(res);
      showSuccess(t("toast.dryRunOk", { count: res.jobTotal }));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.dryRunFailed")));
    } finally {
      setBusy(false);
    }
  };

  const onStart = async () => {
    if (!window.confirm(t("confirmStart", { count: dryRun?.jobTotal ?? "?", target: activeProvider }))) return;
    setBusy(true);
    try {
      const created = await startMigrateJob({
        sourceProvider: source === "ALL" ? null : source,
        targetProvider: activeProvider,
        deleteSourceAfter: false,
      });
      setJob(created);
      showSuccess(t("toast.startOk"));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.startFailed")));
    } finally {
      setBusy(false);
    }
  };

  const onCancel = async () => {
    if (!job) return;
    setBusy(true);
    try {
      setJob(await cancelMigrateJob(job.jobId));
      showWarning(t("toast.cancelOk"));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.cancelFailed")));
    } finally {
      setBusy(false);
    }
  };

  const onResume = async () => {
    if (!job) return;
    setBusy(true);
    try {
      setJob(await resumeMigrateJob(job.jobId));
      showSuccess(t("toast.resumeOk"));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.resumeFailed")));
    } finally {
      setBusy(false);
    }
  };

  const onPurge = async () => {
    if (!job || purgeConfirm !== "DELETE") {
      showWarning(t("purge.typeDelete"));
      return;
    }
    setBusy(true);
    try {
      setJob(await purgeMigrateSource(job.jobId));
      setPurgeConfirm("");
      showSuccess(t("toast.purgeOk"));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.purgeFailed")));
    } finally {
      setBusy(false);
    }
  };

  const progress =
    job && job.totalCount > 0
      ? Math.min(100, Math.round(((job.successCount + job.skipCount + job.failCount) / job.totalCount) * 100))
      : 0;

  return (
    <div className="space-y-4 rounded-lg border border-border bg-card p-6" data-testid="storage-migrate-panel">
      <div>
        <h2 className="text-base font-semibold">{t("title")}</h2>
        <p className="text-sm text-muted-foreground">{t("subtitle")}</p>
      </div>

      <label className="flex flex-col gap-2 text-sm">
        <span>{t("fields.source")}</span>
        <select
          className="rounded-md border border-input bg-background px-3 py-2"
          value={source}
          onChange={(e) => setSource(e.target.value)}
          disabled={busy || (!!job && isActive(job.status))}
        >
          {SOURCE_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {s === "ALL" ? t("fields.sourceAll") : s}
            </option>
          ))}
        </select>
      </label>

      <div className="text-sm text-muted-foreground">
        {t("fields.target")}: <span className="font-medium text-foreground">{activeProvider}</span>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" disabled={busy} onClick={() => void onDryRun()}>
          {t("actions.dryRun")}
        </Button>
        <Button type="button" disabled={busy || (!!job && isActive(job.status)) || source === activeProvider} onClick={() => void onStart()}>
          {t("actions.start")}
        </Button>
        {job && isActive(job.status) ? (
          <Button type="button" variant="destructive" disabled={busy} onClick={() => void onCancel()}>
            {t("actions.cancel")}
          </Button>
        ) : null}
        {job && (job.status === "PAUSED" || job.status === "FAILED" || job.status === "CANCELLED") ? (
          <Button type="button" variant="outline" disabled={busy} onClick={() => void onResume()}>
            {t("actions.resume")}
          </Button>
        ) : null}
      </div>

      {dryRun ? (
        <div className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm">
          {t("dryRunResult", {
            eligible: dryRun.eligibleCount,
            jobTotal: dryRun.jobTotal,
            already: dryRun.alreadyOnTarget,
            truncated: dryRun.truncated ? t("truncatedYes") : t("truncatedNo"),
            testOk: dryRun.targetTestOk ? t("testOk") : t("testStale"),
          })}
        </div>
      ) : null}

      {job ? (
        <div className="space-y-2">
          <div className="flex items-center justify-between text-sm">
            <span>
              {t("status")}: <strong>{job.status}</strong>
            </span>
            <span>
              {job.successCount}/{job.totalCount} · skip {job.skipCount} · fail {job.failCount}
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-muted">
            <div className="h-full bg-emerald-600 transition-all" style={{ width: `${progress}%` }} />
          </div>
          {job.errorSummary ? <p className="text-sm text-destructive">{job.errorSummary}</p> : null}
        </div>
      ) : null}

      {job && (job.status === "COMPLETED" || job.status === "COMPLETED_WITH_ERRORS") && job.sourceProvider ? (
        <div className="space-y-2 border-t border-border pt-4">
          <p className="text-sm text-muted-foreground">{t("purge.hint")}</p>
          <Input
            value={purgeConfirm}
            onChange={(e) => setPurgeConfirm(e.target.value)}
            placeholder="DELETE"
            aria-label={t("purge.typeDelete")}
          />
          <Button type="button" variant="destructive" disabled={busy || purgeConfirm !== "DELETE"} onClick={() => void onPurge()}>
            {t("actions.purge")}
          </Button>
        </div>
      ) : null}

      {errors.length > 0 ? (
        <div className="space-y-1 border-t border-border pt-4">
          <h3 className="text-sm font-medium">{t("errorsTitle")}</h3>
          <ul className="max-h-40 space-y-1 overflow-auto text-xs text-muted-foreground">
            {errors.map((e) => (
              <li key={`${e.attachmentId}-${e.createdAt}`}>
                {e.attachmentId.slice(0, 8)}… — {e.message}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
