"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import MobileShell from "@/components/mobile/mobile-shell";
import { NextTaskRecommendationResponse, taskInterleavingApi } from "@/lib/task-interleaving-api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { RefreshCw, Play, SkipForward, Ban } from "lucide-react";
import { showError, showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";

export default function MobileNextTaskPage() {
  const t = useTranslations("Mobile.tasks");
  const tErrors = useTranslations("Errors");
  const [recommendation, setRecommendation] = React.useState<NextTaskRecommendationResponse | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [acting, setActing] = React.useState(false);
  const [showRejectForm, setShowRejectForm] = React.useState(false);
  const [reasonCode, setReasonCode] = React.useState<string>("");
  const [note, setNote] = React.useState("");

  const showApiErr = React.useCallback(
    (err: unknown, fallback: string) => {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || fallback);
    },
    [tErrors]
  );

  const loadSuggestion = React.useCallback(async () => {
    setLoading(true);
    setShowRejectForm(false);
    setReasonCode("");
    setNote("");
    try {
      const res = await taskInterleavingApi.getNext({ maxCandidates: 5 });
      setRecommendation(res);
    } catch (err) {
      showApiErr(err, t("toast.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [t, showApiErr]);

  React.useEffect(() => {
    queueMicrotask(() => void loadSuggestion());
  }, [loadSuggestion]);

  const handleAccept = async () => {
    if (!recommendation || !recommendation.selected) return;
    setActing(true);
    try {
      const key = `idemp-${recommendation.recommendationId}`;
      await taskInterleavingApi.acceptRecommendation(recommendation.recommendationId, {
        idempotencyKey: key,
      });
      showSuccess(t("toast.acceptOk"));
      window.location.href = `/mobile/tasks/${recommendation.selected.taskId}`;
    } catch (err) {
      showApiErr(err, t("toast.loadFailed"));
      loadSuggestion();
    } finally {
      setActing(false);
    }
  };

  const handleReject = async () => {
    if (!recommendation) return;
    if (!reasonCode) {
      showError(t("toast.needReason"));
      return;
    }
    setActing(true);
    try {
      await taskInterleavingApi.rejectRecommendation(recommendation.recommendationId, {
        reasonCode,
        note: note ? note : undefined,
      });
      showSuccess(t("toast.skipOk"));
      loadSuggestion();
    } catch (err) {
      showApiErr(err, t("toast.loadFailed"));
    } finally {
      setActing(false);
      setShowRejectForm(false);
    }
  };

  if (loading) {
    return (
      <MobileShell>
        <div className="flex flex-col items-center justify-center min-h-[60vh] gap-4 p-6">
          <RefreshCw className="size-8 animate-spin text-primary" />
          <span className="text-sm font-medium">{t("states.loading")}</span>
        </div>
      </MobileShell>
    );
  }

  const hasSelected = recommendation && recommendation.selected;

  return (
    <MobileShell>
      <div className="flex flex-col gap-6">
        <div className="text-center">
          <h2 className="text-xl font-bold">{t("page.title")}</h2>
          <p className="text-xs text-slate-400">{t("page.subtitle")}</p>
        </div>

        {!hasSelected ? (
          <Card className="border-dashed border-slate-700 bg-slate-800/40">
            <CardContent className="flex flex-col items-center justify-center py-12 text-center gap-4">
              <Ban className="size-12 text-slate-500" />
              <div>
                <p className="font-semibold text-sm">{t("states.emptyTitle")}</p>
                <p className="text-xs text-slate-400">{t("states.emptyHint")}</p>
              </div>
              <Button
                id="task-interleaving-find-another-button"
                onClick={loadSuggestion}
                disabled={loading}
                className="w-full mt-4"
              >
                {t("actions.findAnother")}
              </Button>
            </CardContent>
          </Card>
        ) : (
          <div className="flex flex-col gap-4">
            <Card className="border-2 border-cyan-600 bg-slate-800/50">
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold uppercase tracking-wider text-cyan-400">
                    {recommendation!.selected!.operationType}
                  </span>
                  <span className="text-xs font-mono bg-cyan-500/10 text-cyan-400 px-2 py-0.5 rounded-full">
                    {t("labels.score", { score: recommendation!.selected!.score.toFixed(1) })}
                  </span>
                </div>
                <CardTitle className="text-lg mt-1 text-white">
                  {recommendation!.selected!.taskType}
                </CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div className="bg-slate-900 p-2 rounded">
                    <span className="text-slate-400 block">{t("labels.locationId")}</span>
                    <span className="font-mono font-medium block truncate">
                      {recommendation!.selected!.locationId?.substring(0, 8) ?? "--"}
                    </span>
                  </div>
                  <div className="bg-slate-900 p-2 rounded">
                    <span className="text-slate-400 block">{t("labels.zoneId")}</span>
                    <span className="font-mono font-medium block truncate">
                      {recommendation!.selected!.zoneId?.substring(0, 8) ?? "--"}
                    </span>
                  </div>
                </div>

                {!showRejectForm ? (
                  <div className="flex flex-col gap-2 mt-2">
                    <Button
                      id="task-interleaving-accept-button"
                      onClick={handleAccept}
                      disabled={acting}
                      className="w-full py-6 text-sm"
                      data-icon="inline-start"
                    >
                      <Play className="size-4" />
                      {t("actions.accept")}
                    </Button>
                    <Button
                      id="task-interleaving-reject-button"
                      variant="ghost"
                      onClick={() => setShowRejectForm(true)}
                      disabled={acting}
                      className="w-full py-6 text-xs text-slate-400"
                      data-icon="inline-start"
                    >
                      <SkipForward className="size-4" />
                      {t("actions.skip")}
                    </Button>
                  </div>
                ) : (
                  <div className="flex flex-col gap-3 mt-2 border-t border-slate-700 pt-4">
                    <span className="text-xs font-semibold">{t("labels.skipReason")}</span>
                    <Select value={reasonCode} onValueChange={setReasonCode}>
                      <SelectTrigger className="w-full text-xs">
                        <SelectValue placeholder={t("labels.reasonPlaceholder")} />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="TOO_FAR">{t("reasons.TOO_FAR")}</SelectItem>
                        <SelectItem value="BLOCKED_LOCATION">{t("reasons.BLOCKED_LOCATION")}</SelectItem>
                        <SelectItem value="EQUIPMENT_UNAVAILABLE">{t("reasons.EQUIPMENT_UNAVAILABLE")}</SelectItem>
                        <SelectItem value="TASK_CONTEXT_SWITCH">{t("reasons.TASK_CONTEXT_SWITCH")}</SelectItem>
                      </SelectContent>
                    </Select>

                    <div className="flex gap-2 mt-2">
                      <Button
                        variant="outline"
                        onClick={() => setShowRejectForm(false)}
                        className="w-1/2 text-xs"
                      >
                        {t("actions.back")}
                      </Button>
                      <Button
                        variant="destructive"
                        onClick={handleReject}
                        disabled={acting || !reasonCode}
                        className="w-1/2 text-xs"
                      >
                        {t("actions.confirmSkip")}
                      </Button>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            {recommendation!.candidates.length > 1 && (
              <div className="flex flex-col gap-2 mt-2">
                <span className="text-xs font-semibold text-slate-400 px-1">{t("labels.otherOptions")}</span>
                {recommendation!.candidates.slice(1).map((c) => (
                  <div
                    key={c.taskId}
                    className="flex items-center justify-between p-3 border border-slate-700 rounded-md text-xs bg-slate-800/30"
                  >
                    <div className="flex flex-col">
                      <span className="font-semibold">{c.operationType}</span>
                      <span className="text-slate-500 text-[10px] font-mono">
                        {c.taskId.substring(0, 8)}...
                      </span>
                    </div>
                    <span className="font-mono text-slate-400">{c.score.toFixed(1)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </MobileShell>
  );
}
