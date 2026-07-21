"use client";

import * as React from "react";
import { NextTaskRecommendationResponse, taskInterleavingApi } from "@/lib/task-interleaving-api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { RefreshCw, Play, SkipForward, Ban } from "lucide-react";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";

export default function MobileNextTaskPage() {
  const [recommendation, setRecommendation] = React.useState<NextTaskRecommendationResponse | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [acting, setActing] = React.useState(false);

  // Reject flow
  const [showRejectForm, setShowRejectForm] = React.useState(false);
  const [reasonCode, setReasonCode] = React.useState<string>("");
  const [note, setNote] = React.useState("");

  const loadSuggestion = React.useCallback(async () => {
    setLoading(true);
    setShowRejectForm(false);
    setReasonCode("");
    setNote("");
    try {
      const res = await taskInterleavingApi.getNext({ maxCandidates: 5 });
      setRecommendation(res);
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

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
      showSuccess("Task accepted successfully!");
      // Mock navigation to mobile task workflow
      window.location.href = `/mobile/tasks/${recommendation.selected.taskId}`;
    } catch (err) {
      showError(getHttpErrorMessage(err));
      loadSuggestion();
    } finally {
      setActing(false);
    }
  };

  const handleReject = async () => {
    if (!recommendation) return;
    if (!reasonCode) {
      showError("Please select a reason to skip.");
      return;
    }
    setActing(true);
    try {
      await taskInterleavingApi.rejectRecommendation(recommendation.recommendationId, {
        reasonCode,
        note: note ? note : undefined,
      });
      showSuccess("Skip recorded.");
      loadSuggestion();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setActing(false);
      setShowRejectForm(false);
    }
  };

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] gap-4 p-6">
        <RefreshCw className="size-8 animate-spin text-primary" />
        <span className="text-sm font-medium">Finding optimal next task...</span>
      </div>
    );
  }

  const hasSelected = recommendation && recommendation.selected;

  return (
    <div className="flex flex-col gap-6 p-4 max-w-md mx-auto">
      <div className="text-center">
        <h2 className="text-xl font-bold">Suggested next task</h2>
        <p className="text-xs text-muted-foreground">Spatial optimization active</p>
      </div>

      {!hasSelected ? (
        <Card className="border-dashed">
          <CardContent className="flex flex-col items-center justify-center py-12 text-center gap-4">
            <Ban className="size-12 text-muted-foreground" />
            <div>
              <p className="font-semibold text-sm">No eligible task found</p>
              <p className="text-xs text-muted-foreground">You can request another search or consult supervisor.</p>
            </div>
            <Button
              id="task-interleaving-find-another-button"
              onClick={loadSuggestion}
              disabled={loading}
              className="w-full mt-4"
            >
              Request another search
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-4">
          <Card className="border-2 border-primary bg-card">
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold uppercase tracking-wider text-primary">
                  {recommendation!.selected!.operationType}
                </span>
                <span className="text-xs font-mono bg-primary/10 text-primary px-2 py-0.5 rounded-full">
                  Score: {recommendation!.selected!.score.toFixed(1)}
                </span>
              </div>
              <CardTitle className="text-lg mt-1">
                {recommendation!.selected!.taskType}
              </CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-4">
              <div className="grid grid-cols-2 gap-2 text-xs">
                <div className="bg-muted p-2 rounded">
                  <span className="text-muted-foreground block">Location ID</span>
                  <span className="font-mono font-medium block truncate">
                    {recommendation!.selected!.locationId?.substring(0, 8) ?? "--"}
                  </span>
                </div>
                <div className="bg-muted p-2 rounded">
                  <span className="text-muted-foreground block">Zone ID</span>
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
                    Accept task
                  </Button>
                  <Button
                    id="task-interleaving-reject-button"
                    variant="ghost"
                    onClick={() => setShowRejectForm(true)}
                    disabled={acting}
                    className="w-full py-6 text-xs text-muted-foreground"
                    data-icon="inline-start"
                  >
                    <SkipForward className="size-4" />
                    Skip suggestion
                  </Button>
                </div>
              ) : (
                <div className="flex flex-col gap-3 mt-2 border-t pt-4">
                  <span className="text-xs font-semibold">Select reason to skip:</span>
                  <Select value={reasonCode} onValueChange={setReasonCode}>
                    <SelectTrigger className="w-full text-xs">
                      <SelectValue placeholder="Choose reason..." />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="TOO_FAR">Task location is too far</SelectItem>
                      <SelectItem value="BLOCKED_LOCATION">Location is blocked</SelectItem>
                      <SelectItem value="EQUIPMENT_UNAVAILABLE">Missing equipment</SelectItem>
                      <SelectItem value="TASK_CONTEXT_SWITCH">Prefer other work</SelectItem>
                    </SelectContent>
                  </Select>

                  <div className="flex gap-2 mt-2">
                    <Button
                      variant="outline"
                      onClick={() => setShowRejectForm(false)}
                      className="w-1/2 text-xs"
                    >
                      Back
                    </Button>
                    <Button
                      variant="destructive"
                      onClick={handleReject}
                      disabled={acting || !reasonCode}
                      className="w-1/2 text-xs"
                    >
                      Confirm skip
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Quick list of backup suggestions */}
          {recommendation!.candidates.length > 1 && (
            <div className="flex flex-col gap-2 mt-2">
              <span className="text-xs font-semibold text-muted-foreground px-1">Other options:</span>
              {recommendation!.candidates.slice(1).map((c) => (
                <div
                  key={c.taskId}
                  className="flex items-center justify-between p-3 border rounded-md text-xs bg-muted/30"
                >
                  <div className="flex flex-col">
                    <span className="font-semibold">{c.operationType}</span>
                    <span className="text-muted-foreground text-[10px] font-mono">
                      {c.taskId.substring(0, 8)}...
                    </span>
                  </div>
                  <span className="font-mono text-muted-foreground">{c.score.toFixed(1)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
