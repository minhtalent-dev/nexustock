"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { ArrowLeft, CheckCircle, XCircle, Clock } from "lucide-react";

interface EventDto {
  id: string;
  eventType: string;
  actor: string;
  occurredAt: string;
  traceId: string | null;
}

interface CandidateDetail {
  id: string;
  itemId: string;
  lotId: string;
  waveItemId: string;
  qtyAvailable: number;
  qtyRequested: number;
  qtyMatched: number;
  matchScore: number;
  status: string;
  rejectedReason: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
  events: EventDto[];
}

const STATUS_COLORS: Record<string, string> = {
  Pending: "bg-yellow-100 text-yellow-800",
  Accepted: "bg-green-100 text-green-800",
  Rejected: "bg-red-100 text-red-800",
  Expired: "bg-gray-100 text-gray-600",
  Executing: "bg-blue-100 text-blue-800",
};

export default function CandidateDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [candidate, setCandidate] = useState<CandidateDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [acceptOpen, setAcceptOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [actioning, setActioning] = useState(false);

  const fetchDetail = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.get(`/cross-docking/${id}`);
      setCandidate(res.data);
    } catch (err) {
      setError(getHttpErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    queueMicrotask(() => void fetchDetail());
  }, [fetchDetail]);

  const handleAccept = async () => {
    setActioning(true);
    try {
      await api.post(`/cross-docking/${id}/accept`);
      showSuccess("Candidate accepted.");
      setAcceptOpen(false);
      fetchDetail();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setActioning(false);
    }
  };

  const handleReject = async () => {
    if (!rejectReason.trim()) return;
    setActioning(true);
    try {
      await api.post(`/cross-docking/${id}/reject`, { reason: rejectReason.trim() });
      showSuccess("Candidate rejected.");
      setRejectOpen(false);
      setRejectReason("");
      fetchDetail();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setActioning(false);
    }
  };

  if (loading) return (
    <div className="p-6 space-y-4">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-40 w-full" />
    </div>
  );

  if (error || !candidate) return (
    <div className="p-6 text-center text-red-600">{error ?? "Candidate not found."}</div>
  );

  const isPending = candidate.status === "Pending";

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.push("/admin/cross-docking")}>
          <ArrowLeft className="w-4 h-4 mr-1" /> Back
        </Button>
        <h1 className="text-xl font-semibold">Cross-dock candidate</h1>
        <Badge className={STATUS_COLORS[candidate.status] ?? ""}>{candidate.status}</Badge>
      </div>

      <Card>
        <CardHeader><CardTitle>Candidate details</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm">
            <div><dt className="text-muted-foreground">ID</dt><dd className="font-mono text-xs mt-0.5">{candidate.id}</dd></div>
            <div><dt className="text-muted-foreground">Item ID</dt><dd className="font-mono text-xs mt-0.5">{candidate.itemId}</dd></div>
            <div><dt className="text-muted-foreground">Lot ID</dt><dd className="font-mono text-xs mt-0.5">{candidate.lotId}</dd></div>
            <div><dt className="text-muted-foreground">Wave item ID</dt><dd className="font-mono text-xs mt-0.5">{candidate.waveItemId}</dd></div>
            <div><dt className="text-muted-foreground">Qty available</dt><dd className="mt-0.5">{candidate.qtyAvailable}</dd></div>
            <div><dt className="text-muted-foreground">Qty requested</dt><dd className="mt-0.5">{candidate.qtyRequested}</dd></div>
            <div><dt className="text-muted-foreground">Qty matched</dt><dd className="mt-0.5 font-semibold">{candidate.qtyMatched}</dd></div>
            <div><dt className="text-muted-foreground">Match score</dt><dd className="mt-0.5">{candidate.matchScore}%</dd></div>
            {candidate.rejectedReason && (
              <div className="col-span-2"><dt className="text-muted-foreground">Reject reason</dt><dd className="mt-0.5 text-red-600">{candidate.rejectedReason}</dd></div>
            )}
          </dl>
        </CardContent>
      </Card>

      {isPending && (
        <div className="flex gap-3">
          <Button onClick={() => setAcceptOpen(true)} className="bg-green-600 hover:bg-green-700 text-white">
            <CheckCircle className="w-4 h-4 mr-1" /> Accept
          </Button>
          <Button variant="outline" onClick={() => setRejectOpen(true)} className="border-red-300 text-red-600 hover:bg-red-50">
            <XCircle className="w-4 h-4 mr-1" /> Reject
          </Button>
        </div>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base">Event timeline</CardTitle></CardHeader>
        <CardContent>
          {candidate.events.length === 0 ? (
            <p className="text-sm text-muted-foreground">No events recorded.</p>
          ) : (
            <ol className="relative border-l border-muted ml-3 space-y-4">
              {candidate.events.map((e) => (
                <li key={e.id} className="ml-4">
                  <div className="absolute -left-1.5 mt-1 w-3 h-3 rounded-full bg-primary" />
                  <div className="text-sm font-medium">{e.eventType}</div>
                  <div className="text-xs text-muted-foreground flex gap-3">
                    <span className="flex items-center gap-1"><Clock className="w-3 h-3" />{new Date(e.occurredAt).toLocaleString()}</span>
                    <span>by {e.actor}</span>
                  </div>
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>

      <Dialog open={acceptOpen} onOpenChange={setAcceptOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Accept this candidate?</DialogTitle></DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAcceptOpen(false)}>Cancel</Button>
            <Button disabled={actioning} onClick={handleAccept} className="bg-green-600 hover:bg-green-700 text-white">
              {actioning ? "Accepting..." : "Confirm accept"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Reject this candidate</DialogTitle></DialogHeader>
          <div className="space-y-2 py-2">
            <Textarea placeholder="Reason for rejection..." value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} rows={3} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectOpen(false)}>Cancel</Button>
            <Button variant="destructive" disabled={actioning || !rejectReason.trim()} onClick={handleReject}>
              {actioning ? "Rejecting..." : "Confirm reject"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
