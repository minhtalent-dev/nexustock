"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { RefreshCw, Search, Zap } from "lucide-react";

interface CandidateDto {
  id: string;
  itemId: string;
  lotId: string;
  waveItemId: string;
  qtyAvailable: number;
  qtyRequested: number;
  qtyMatched: number;
  matchScore: number;
  status: string;
  createdAt: string;
}

const STATUS_COLORS: Record<string, string> = {
  Pending: "bg-yellow-100 text-yellow-800",
  Accepted: "bg-green-100 text-green-800",
  Rejected: "bg-red-100 text-red-800",
  Expired: "bg-gray-100 text-gray-600",
  Executing: "bg-blue-100 text-blue-800",
};

export default function CrossDockingPage() {
  const router = useRouter();
  const [candidates, setCandidates] = useState<CandidateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const pageSize = 20;

  const [evaluateOpen, setEvaluateOpen] = useState(false);
  const [lotIdInput, setLotIdInput] = useState("");
  const [evaluating, setEvaluating] = useState(false);

  const fetchCandidates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string | number> = { page, pageSize };
      if (statusFilter && statusFilter !== "all") params.status = statusFilter;
      const res = await api.get("/cross-docking/candidates", { params });
      setCandidates(res.data.items ?? []);
      setTotal(res.data.total ?? 0);
    } catch (err) {
      setError(getHttpErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter]);

  useEffect(() => {
    queueMicrotask(() => void fetchCandidates());
  }, [fetchCandidates]);

  const handleEvaluate = async () => {
    if (!lotIdInput.trim()) return;
    setEvaluating(true);
    try {
      const res = await api.post("/cross-docking/evaluate", { lotId: lotIdInput.trim() });
      const count = res.data.candidates?.length ?? 0;
      showSuccess(`Evaluated: ${count} candidate(s) created.`);
      setEvaluateOpen(false);
      setLotIdInput("");
      fetchCandidates();
    } catch (err) {
      showError(getHttpErrorMessage(err));
    } finally {
      setEvaluating(false);
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Cross-docking candidates</h1>
          <p className="text-sm text-muted-foreground">Direct transfer suggestions from inbound lots to open shipments.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={fetchCandidates}>
            <RefreshCw className="w-4 h-4 mr-1" /> Refresh
          </Button>
          <Button size="sm" onClick={() => setEvaluateOpen(true)}>
            <Zap className="w-4 h-4 mr-1" /> Evaluate lot
          </Button>
        </div>
      </div>

      <div className="flex gap-3 items-center">
        <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v); setPage(1); }}>
          <SelectTrigger className="w-40">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            <SelectItem value="Pending">Pending</SelectItem>
            <SelectItem value="Accepted">Accepted</SelectItem>
            <SelectItem value="Rejected">Rejected</SelectItem>
            <SelectItem value="Expired">Expired</SelectItem>
          </SelectContent>
        </Select>
        <span className="text-sm text-muted-foreground">{total} total</span>
      </div>

      <Card>
        <CardContent className="p-0">
          {error ? (
            <div className="p-6 text-center text-red-600">{error}</div>
          ) : loading ? (
            <div className="p-4 space-y-2">
              {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : candidates.length === 0 ? (
            <div className="p-12 text-center text-muted-foreground">
              <Search className="w-8 h-8 mx-auto mb-2 opacity-40" />
              No cross-dock candidates found.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Item ID</TableHead>
                  <TableHead>Lot ID</TableHead>
                  <TableHead className="text-right">Qty matched</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {candidates.map((c) => (
                  <TableRow key={c.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/admin/cross-docking/${c.id}`)}>
                    <TableCell className="font-mono text-xs">{c.itemId}</TableCell>
                    <TableCell className="font-mono text-xs">{c.lotId}</TableCell>
                    <TableCell className="text-right">{c.qtyMatched}</TableCell>
                    <TableCell className="text-right">{c.matchScore}%</TableCell>
                    <TableCell>
                      <Badge className={STATUS_COLORS[c.status] ?? ""}>{c.status}</Badge>
                    </TableCell>
                    <TableCell className="text-xs">{new Date(c.createdAt).toLocaleString()}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" onClick={(e) => { e.stopPropagation(); router.push(`/admin/cross-docking/${c.id}`); }}>View</Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {totalPages > 1 && (
        <div className="flex justify-center gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
          <span className="text-sm self-center">Page {page} / {totalPages}</span>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</Button>
        </div>
      )}

      <Dialog open={evaluateOpen} onOpenChange={setEvaluateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Evaluate lot for cross-docking</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <p className="text-sm text-muted-foreground">Enter a QC-released lot ID to find matching outbound demand.</p>
            <Input
              placeholder="Lot ID (UUID)"
              value={lotIdInput}
              onChange={(e) => setLotIdInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleEvaluate()}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEvaluateOpen(false)}>Cancel</Button>
            <Button disabled={evaluating || !lotIdInput.trim()} onClick={handleEvaluate}>
              {evaluating ? "Evaluating..." : "Evaluate"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
