"use client";

import { useEffect, useState } from "react";
import { getDeliveries, replayDelivery, replayBulk } from "@/features/webhook/api";
import { WebhookDelivery } from "@/features/webhook/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { showError } from "@/lib/toast";
import { toast } from "sonner";

const STATUS_VARIANTS: Record<string, "default" | "secondary" | "destructive" | "outline"> = {
  pending: "secondary",
  sending: "outline",
  delivered: "default",
  deadLetter: "destructive",
};

export default function WebhookDeliveriesPage() {
  const [deliveries, setDeliveries] = useState<WebhookDelivery[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [status, setStatus] = useState("all");
  const [traceId, setTraceId] = useState("");
  const [loading, setLoading] = useState(false);
  const [selectedDelivery, setSelectedDelivery] = useState<WebhookDelivery | null>(null);
  const [replayingAll, setReplayingAll] = useState(false);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      try {
        const data = await getDeliveries({
          status: status === "all" ? undefined : status,
          traceId: traceId.trim() || undefined,
          page,
          pageSize,
        });
        if (active) {
          setDeliveries(data.items);
          setTotal(data.total);
        }
      } catch {
        showError("Không thể tải danh sách delivery.");
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [status, traceId, page, pageSize]);

  const fetchDeliveries = () => {
    // Chỉ trigger reload bằng cách giữ nguyên dependencies
    setPage(p => p);
  };

  const handleReplay = async (delivery: WebhookDelivery) => {
    try {
      await replayDelivery(delivery.id);
      toast.success(`Đã replay delivery ${delivery.id.slice(0, 8)}...`);
      fetchDeliveries();
    } catch {
      showError("Replay thất bại.");
    }
  };

  const handleReplayAllDLQ = async () => {
    setReplayingAll(true);
    try {
      const res = await replayBulk({ filterStatus: "deadLetter" });
      toast.success(`Đã replay ${res.replayed} DLQ deliveries.`);
      fetchDeliveries();
    } catch {
      showError("Replay All DLQ thất bại.");
    } finally {
      setReplayingAll(false);
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Webhook Deliveries</h1>
          <p className="text-muted-foreground text-sm mt-1">Lịch sử gửi và trạng thái webhook.</p>
        </div>
        {status === "deadLetter" && (
          <Button variant="destructive" onClick={handleReplayAllDLQ} disabled={replayingAll}>
            {replayingAll ? "Replaying..." : "Replay All DLQ"}
          </Button>
        )}
      </div>

      {/* Filters */}
      <div className="flex gap-3 items-center flex-wrap">
        <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
          <SelectTrigger className="w-44" id="delivery-status-filter">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All</SelectItem>
            <SelectItem value="pending">Pending</SelectItem>
            <SelectItem value="sending">Sending</SelectItem>
            <SelectItem value="delivered">Delivered</SelectItem>
            <SelectItem value="deadLetter">Dead Letter</SelectItem>
          </SelectContent>
        </Select>
        <Input
          id="delivery-traceid-filter"
          placeholder="Filter by Trace ID..."
          value={traceId}
          onChange={(e) => { setTraceId(e.target.value); setPage(1); }}
          className="w-64"
        />
        <Button variant="outline" onClick={fetchDeliveries}>Refresh</Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Deliveries ({total})</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-muted-foreground">Đang tải...</p>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Event Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Retries</TableHead>
                    <TableHead>HTTP Code</TableHead>
                    <TableHead>Trace ID</TableHead>
                    <TableHead>Created</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {deliveries.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={7} className="text-center text-muted-foreground">
                        Không có delivery nào.
                      </TableCell>
                    </TableRow>
                  )}
                  {deliveries.map((d) => (
                    <TableRow key={d.id} className="cursor-pointer hover:bg-muted/50" onClick={() => setSelectedDelivery(d)}>
                      <TableCell className="font-mono text-xs">{d.eventType}</TableCell>
                      <TableCell>
                        <Badge variant={STATUS_VARIANTS[d.status] ?? "secondary"}>{d.status}</Badge>
                      </TableCell>
                      <TableCell className="text-center">{d.retryCount}</TableCell>
                      <TableCell>{d.lastResponseCode ?? "—"}</TableCell>
                      <TableCell className="font-mono text-xs">{d.traceId.slice(0, 12)}...</TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {new Date(d.createdAt).toLocaleString("vi-VN")}
                      </TableCell>
                      <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                        {d.status === "deadLetter" && (
                          <Button size="sm" variant="outline" onClick={() => handleReplay(d)}>
                            Replay
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              <div className="flex justify-between items-center mt-4 text-sm">
                <span className="text-muted-foreground">
                  Trang {page}/{totalPages || 1} — {total} bản ghi
                </span>
                <div className="flex gap-2">
                  <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    Previous
                  </Button>
                  <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                    Next
                  </Button>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Detail Dialog */}
      <Dialog open={!!selectedDelivery} onOpenChange={(open) => !open && setSelectedDelivery(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Delivery Detail</DialogTitle>
          </DialogHeader>
          {selectedDelivery && (
            <div className="space-y-3 text-sm">
              <div className="grid grid-cols-2 gap-2">
                <div><span className="text-muted-foreground">ID:</span> <span className="font-mono text-xs">{selectedDelivery.id}</span></div>
                <div><span className="text-muted-foreground">Event:</span> {selectedDelivery.eventType}</div>
                <div><span className="text-muted-foreground">Status:</span> <Badge variant={STATUS_VARIANTS[selectedDelivery.status]}>{selectedDelivery.status}</Badge></div>
                <div><span className="text-muted-foreground">Retries:</span> {selectedDelivery.retryCount}</div>
                <div><span className="text-muted-foreground">HTTP Code:</span> {selectedDelivery.lastResponseCode ?? "—"}</div>
                <div><span className="text-muted-foreground">Trace ID:</span> <span className="font-mono text-xs">{selectedDelivery.traceId}</span></div>
              </div>
              {selectedDelivery.lastError && (
                <div>
                  <p className="text-muted-foreground mb-1">Last Error:</p>
                  <pre className="bg-muted rounded p-2 text-xs overflow-auto">{selectedDelivery.lastError}</pre>
                </div>
              )}
              <div>
                <p className="text-muted-foreground mb-1">Payload:</p>
                <pre className="bg-muted rounded p-2 text-xs overflow-auto max-h-48">
                  {(() => {
                    try {
                      return JSON.stringify(JSON.parse(selectedDelivery.payload ?? "{}"), null, 2);
                    } catch {
                      return selectedDelivery.payload ?? "";
                    }
                  })()}
                </pre>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
