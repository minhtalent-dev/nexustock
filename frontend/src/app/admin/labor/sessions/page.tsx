"use client";

import * as React from "react";
import {
  laborApi,
  LaborSessionDto,
  CurrentShiftResponse,
  StartLaborSessionRequest,
} from "@/lib/labor-api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { Play, Pause, CheckCircle2, XCircle, RefreshCw, Plus, Clock, ShieldAlert } from "lucide-react";

const STATUS_COLORS: Record<string, string> = {
  Running: "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400",
  Paused: "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400",
  Completed: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  Cancelled: "bg-rose-100 text-rose-800 dark:bg-rose-900/30 dark:text-rose-400",
};

export default function LaborSessionsPage() {
  const [sessions, setSessions] = React.useState<LaborSessionDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [total, setTotal] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [statusFilter, setStatusFilter] = React.useState("ALL");
  const [shiftInfo, setShiftInfo] = React.useState<CurrentShiftResponse | null>(null);

  // Realtime clock duration updates
  const [now, setNow] = React.useState<number>(Date.now());

  // Start Session Modal
  const [startOpen, setStartOpen] = React.useState(false);
  const [sourceTaskType, setSourceTaskType] = React.useState("PICKING");
  const [sourceTaskId, setSourceTaskId] = React.useState("");
  const [operationType, setOperationType] = React.useState("PICKING");
  const [locationId, setLocationId] = React.useState("");
  const [starting, setStarting] = React.useState(false);

  // Cancel Session Modal
  const [cancelOpen, setCancelOpen] = React.useState(false);
  const [cancellingId, setCancellingId] = React.useState<string | null>(null);
  const [cancelReason, setCancelReason] = React.useState("");
  const [cancelling, setCancelling] = React.useState(false);

  const fetchShift = async () => {
    try {
      const res = await laborApi.getCurrentShift();
      setShiftInfo(res);
    } catch {
      // Ignored: Shift might not be running or initialized yet.
    }
  };

  const fetchSessions = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await laborApi.listSessions({
        status: statusFilter === "ALL" ? undefined : statusFilter,
        page,
        pageSize: 10,
      });
      setSessions(res.items ?? []);
      setTotal(res.total ?? 0);
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter]);

  React.useEffect(() => {
    queueMicrotask(() => {
      void fetchShift();
      void fetchSessions();
    });
  }, [fetchSessions]);

  // Update clock tick every second to drive realtime session timers
  React.useEffect(() => {
    const timer = setInterval(() => {
      setNow(Date.now());
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const handleStartSession = async () => {
    setStarting(true);
    try {
      const req: StartLaborSessionRequest = {
        sourceTaskType,
        sourceTaskId: sourceTaskId.trim() || undefined,
        operationType,
        locationId: locationId.trim() || undefined,
      };
      const res = await laborApi.startSession(req);
      showSuccess(`Session started. Status: ${res.status}`);
      setStartOpen(false);
      setSourceTaskId("");
      setLocationId("");
      fetchSessions();
      fetchShift();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setStarting(false);
    }
  };

  const handlePause = async (id: string) => {
    try {
      await laborApi.pauseSession(id);
      showSuccess("Session paused.");
      fetchSessions();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    }
  };

  const handleResume = async (id: string) => {
    try {
      await laborApi.resumeSession(id);
      showSuccess("Session resumed.");
      fetchSessions();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    }
  };

  const handleComplete = async (id: string) => {
    try {
      await laborApi.completeSession(id);
      showSuccess("Session completed.");
      fetchSessions();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    }
  };

  const openCancelDialog = (id: string) => {
    setCancellingId(id);
    setCancelReason("");
    setCancelOpen(true);
  };

  const handleCancel = async () => {
    if (!cancellingId) return;
    if (!cancelReason.trim()) {
      showError("Reason is required.");
      return;
    }
    setCancelling(true);
    try {
      await laborApi.cancelSession(cancellingId, cancelReason.trim());
      showSuccess("Session cancelled.");
      setCancelOpen(false);
      fetchSessions();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setCancelling(false);
    }
  };

  // Helper calculating live seconds elapsed
  const renderLiveDuration = (session: LaborSessionDto) => {
    if (session.status !== "Running") {
      const totalSec = session.durationSeconds;
      const h = Math.floor(totalSec / 3600);
      const m = Math.floor((totalSec % 3600) / 60);
      const s = totalSec % 60;
      return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
    }

    // Calculating duration in realtime
    const startMs = new Date(session.startedAt).getTime();
    const elapsedSec = Math.max(0, Math.floor((now - startMs) / 1000) - session.pausedSeconds);
    const h = Math.floor(elapsedSec / 3600);
    const m = Math.floor((elapsedSec % 3600) / 60);
    const s = elapsedSec % 60;

    return (
      <span className="font-mono text-emerald-500 font-bold animate-pulse flex items-center gap-1">
        <Clock className="h-3 w-3" />
        {`${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`}
      </span>
    );
  };

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground">Labor Sessions</h1>
          <p className="text-muted-foreground text-sm">Control active work timers and session status transitions.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={fetchSessions} disabled={loading} className="gap-2">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            Refresh
          </Button>
          <Button onClick={() => setStartOpen(true)} className="gap-2">
            <Plus className="h-4 w-4" /> Start New Session
          </Button>
        </div>
      </div>

      {/* Shift Info Status */}
      {shiftInfo && (
        <Card className="bg-emerald-50/20 border-emerald-500/20 dark:bg-emerald-950/10">
          <CardContent className="flex items-center justify-between p-4 flex-wrap gap-2 text-sm">
            <div className="flex items-center gap-2">
              <div className="h-2.5 w-2.5 rounded-full bg-emerald-500 animate-ping" />
              <span className="font-medium text-muted-foreground">Active Shift:</span>
              <strong className="text-foreground">{shiftInfo.shiftCode}</strong>
            </div>
            <div className="text-muted-foreground text-xs">
              Started at: <strong>{new Date(shiftInfo.startedAt).toLocaleTimeString()}</strong>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Sessions Table Filter */}
      <Card>
        <CardHeader className="py-3 flex flex-row items-center justify-between border-b bg-muted/20">
          <CardTitle className="text-sm font-semibold">Active & Completed Log</CardTitle>
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[150px] h-8">
              <SelectValue placeholder="Filter Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Status</SelectItem>
              <SelectItem value="Running">Running</SelectItem>
              <SelectItem value="Paused">Paused</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Cancelled">Cancelled</SelectItem>
            </SelectContent>
          </Select>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Operation</TableHead>
                <TableHead>Source Task</TableHead>
                <TableHead>User ID</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Started At</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading && sessions.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                    Loading sessions...
                  </TableCell>
                </TableRow>
              ) : sessions.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                    No labor sessions found. Start a session to track.
                  </TableCell>
                </TableRow>
              ) : (
                sessions.map((session) => (
                  <TableRow key={session.id}>
                    <TableCell className="font-semibold">{session.operationType}</TableCell>
                    <TableCell>
                      <span className="text-xs text-muted-foreground block">{session.sourceTaskType}</span>
                      <span className="font-mono text-xs text-foreground">{session.sourceTaskId || "N/A"}</span>
                    </TableCell>
                    <TableCell className="text-xs">{session.userId}</TableCell>
                    <TableCell>
                      <Badge className={STATUS_COLORS[session.status] || "bg-gray-100"}>
                        {session.status}
                      </Badge>
                    </TableCell>
                    <TableCell>{renderLiveDuration(session)}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {new Date(session.startedAt).toLocaleString()}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1.5">
                        {session.status === "Running" && (
                          <>
                            <Button variant="outline" size="xs" onClick={() => handlePause(session.id)} className="h-7 text-xs gap-1 border-amber-500/30 text-amber-600 hover:bg-amber-500/10">
                              <Pause className="h-3.5 w-3.5" /> Pause
                            </Button>
                            <Button variant="outline" size="xs" onClick={() => handleComplete(session.id)} className="h-7 text-xs gap-1 border-emerald-500/30 text-emerald-600 hover:bg-emerald-500/10">
                              <CheckCircle2 className="h-3.5 w-3.5" /> Complete
                            </Button>
                          </>
                        )}
                        {session.status === "Paused" && (
                          <>
                            <Button variant="outline" size="xs" onClick={() => handleResume(session.id)} className="h-7 text-xs gap-1 border-green-500/30 text-green-600 hover:bg-green-500/10">
                              <Play className="h-3.5 w-3.5" /> Resume
                            </Button>
                            <Button variant="outline" size="xs" onClick={() => handleComplete(session.id)} className="h-7 text-xs gap-1 border-emerald-500/30 text-emerald-600 hover:bg-emerald-500/10">
                              <CheckCircle2 className="h-3.5 w-3.5" /> Complete
                            </Button>
                          </>
                        )}
                        {(session.status === "Running" || session.status === "Paused") && (
                          <Button variant="outline" size="xs" onClick={() => openCancelDialog(session.id)} className="h-7 text-xs gap-1 border-rose-500/30 text-rose-600 hover:bg-rose-500/10">
                            <XCircle className="h-3.5 w-3.5" /> Cancel
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Start Session Dialog */}
      <Dialog open={startOpen} onOpenChange={setStartOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Start Labor Session</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <label className="text-sm font-semibold">Source Task Type</label>
              <Select value={sourceTaskType} onValueChange={setSourceTaskType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="PICKING">Picking</SelectItem>
                  <SelectItem value="PUTAWAY">Putaway</SelectItem>
                  <SelectItem value="PACKING">Packing</SelectItem>
                  <SelectItem value="QC">Quality Control</SelectItem>
                  <SelectItem value="CYCLE_COUNT">Cycle Counting</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-semibold">Source Task ID (Optional)</label>
              <Input
                placeholder="Enter GUID or task reference..."
                value={sourceTaskId}
                onChange={(e) => setSourceTaskId(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-semibold">Operation Type</label>
              <Select value={operationType} onValueChange={setOperationType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="PICKING">Picking</SelectItem>
                  <SelectItem value="PUTAWAY">Putaway</SelectItem>
                  <SelectItem value="PACKING">Packing</SelectItem>
                  <SelectItem value="RECEIVING">Receiving</SelectItem>
                  <SelectItem value="COUNTING">Counting</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-semibold">Location ID (Optional)</label>
              <Input
                placeholder="Enter Location GUID..."
                value={locationId}
                onChange={(e) => setLocationId(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStartOpen(false)}>Cancel</Button>
            <Button onClick={handleStartSession} disabled={starting}>
              {starting ? "Starting..." : "Start Timer"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Cancel Session Dialog */}
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-rose-600">
              <ShieldAlert className="h-5 w-5" /> Cancel Labor Session
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <p className="text-sm text-muted-foreground">Please provide a valid cancellation reason to abort this worker timer.</p>
            <div className="space-y-2">
              <label className="text-sm font-semibold">Reason</label>
              <Input
                placeholder="e.g., Equipment Malfunction, Shift ended early, Assigned wrong task..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>Back</Button>
            <Button variant="destructive" onClick={handleCancel} disabled={cancelling}>
              {cancelling ? "Cancelling..." : "Confirm Abort"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
