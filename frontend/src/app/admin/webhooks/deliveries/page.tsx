"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { getDeliveries, replayDelivery, replayBulk } from "@/features/webhook/api";
import { WebhookDelivery } from "@/features/webhook/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { toast } from "sonner";

const STATUS_VARIANTS: Record<string, "default" | "secondary" | "destructive" | "outline"> = {
  pending: "secondary",
  sending: "outline",
  delivered: "default",
  deadLetter: "destructive",
};

export default function WebhookDeliveriesPage() {
  const t = useTranslations("Admin.webhooks.deliveries");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

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
      } catch (err) {
        const { codeLabel, message } = resolveApiError(err, tErrors);
        showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, [status, traceId, page, pageSize, t, tErrors]);

  const fetchDeliveries = () => {
    setPage(p => p);
  };

  const handleReplay = async (delivery: WebhookDelivery) => {
    try {
      await replayDelivery(delivery.id);
      toast.success(t("toastReplay", { id: `${delivery.id.slice(0, 8)}...` }));
      fetchDeliveries();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.replayFailed"));
    }
  };

  const handleReplayAllDLQ = async () => {
    setReplayingAll(true);
    try {
      const res = await replayBulk({ filterStatus: "deadLetter" });
      toast.success(t("toastReplayBulk", { count: res.replayed }));
      fetchDeliveries();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.replayBulkFailed"));
    } finally {
      setReplayingAll(false);
    }
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <PageShell className="gap-6">
      <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t("title")}</h1>
          <p className="text-muted-foreground text-sm mt-1">{t("subtitle")}</p>
        </div>
        {status === "deadLetter" && (
          <Button variant="destructive" onClick={handleReplayAllDLQ} disabled={replayingAll}>
            {replayingAll ? t("replaying") : t("replayAllDlq")}
          </Button>
        )}
      </div>

      <div className="flex gap-3 items-center flex-wrap">
        <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
          <SelectTrigger className="w-44" id="delivery-status-filter">
            <SelectValue placeholder={t("statusPlaceholder")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("statusAll")}</SelectItem>
            <SelectItem value="pending">{t("statusPending")}</SelectItem>
            <SelectItem value="sending">{t("statusSending")}</SelectItem>
            <SelectItem value="delivered">{t("statusDelivered")}</SelectItem>
            <SelectItem value="deadLetter">{t("statusDeadLetter")}</SelectItem>
          </SelectContent>
        </Select>
        <Input
          id="delivery-traceid-filter"
          placeholder={t("traceFilterPlaceholder")}
          value={traceId}
          onChange={(e) => { setTraceId(e.target.value); setPage(1); }}
          className="w-64"
        />
        <Button variant="outline" onClick={fetchDeliveries}>{tc("refresh")}</Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("listTitle", { total })}</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-muted-foreground">{tc("loading")}</p>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("colEventType")}</TableHead>
                    <TableHead>{t("colStatus")}</TableHead>
                    <TableHead>{t("colRetries")}</TableHead>
                    <TableHead>{t("colHttpCode")}</TableHead>
                    <TableHead>{t("colTraceId")}</TableHead>
                    <TableHead>{t("colCreated")}</TableHead>
                    <TableHead className="text-right">{t("colActions")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {deliveries.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={7} className="text-center text-muted-foreground">
                        {t("empty")}
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
                            {t("replay")}
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              <div className="flex justify-between items-center mt-4 text-sm">
                <span className="text-muted-foreground">
                  {tc("pageOf", { page, totalPages: totalPages || 1, total })}
                </span>
                <div className="flex gap-2">
                  <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    {tc("previous")}
                  </Button>
                  <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                    {tc("next")}
                  </Button>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!selectedDelivery} onOpenChange={(open) => !open && setSelectedDelivery(null)}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t("detailTitle")}</DialogTitle>
          </DialogHeader>
          {selectedDelivery && (
            <div className="space-y-3 text-sm">
              <div className="grid grid-cols-2 gap-2">
                <div><span className="text-muted-foreground">{t("labelId")}:</span> <span className="font-mono text-xs">{selectedDelivery.id}</span></div>
                <div><span className="text-muted-foreground">{t("labelEvent")}:</span> {selectedDelivery.eventType}</div>
                <div><span className="text-muted-foreground">{t("colStatus")}:</span> <Badge variant={STATUS_VARIANTS[selectedDelivery.status]}>{selectedDelivery.status}</Badge></div>
                <div><span className="text-muted-foreground">{t("labelRetries")}:</span> {selectedDelivery.retryCount}</div>
                <div><span className="text-muted-foreground">{t("colHttpCode")}:</span> {selectedDelivery.lastResponseCode ?? "—"}</div>
                <div><span className="text-muted-foreground">{t("colTraceId")}:</span> <span className="font-mono text-xs">{selectedDelivery.traceId}</span></div>
              </div>
              {selectedDelivery.lastError && (
                <div>
                  <p className="text-muted-foreground mb-1">{t("lastError")}:</p>
                  <pre className="bg-muted rounded p-2 text-xs overflow-auto">{selectedDelivery.lastError}</pre>
                </div>
              )}
              <div>
                <p className="text-muted-foreground mb-1">{t("payload")}:</p>
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
    </PageShell>
  );
}
