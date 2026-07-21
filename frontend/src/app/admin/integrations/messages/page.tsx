"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { getIntegrationMessages } from "@/features/erp-integration/api";
import { IntegrationMessage } from "@/features/erp-integration/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";

export default function IntegrationMessagesPage() {
  const t = useTranslations("Admin.integrations.messages");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [messages, setMessages] = useState<IntegrationMessage[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [status, setStatus] = useState<string>("all");
  const [traceId, setTraceId] = useState("");
  const [selectedMessage, setSelectedMessage] = useState<IntegrationMessage | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchMessages = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getIntegrationMessages({
        status: status === "all" ? undefined : status,
        traceId: traceId.trim() || undefined,
        page,
        pageSize,
      });
      setMessages(data.items);
      setTotal(data.total);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [status, traceId, page, pageSize, t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchMessages());
  }, [fetchMessages]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    fetchMessages();
  };

  const getStatusBadge = (statusValue: string) => {
    switch (statusValue) {
      case "accepted":
        return <Badge className="bg-emerald-600 hover:bg-emerald-500">{t("statusAccepted")}</Badge>;
      case "conflict":
        return <Badge className="bg-amber-600 hover:bg-amber-500">{t("statusConflict")}</Badge>;
      case "failed":
        return <Badge className="bg-rose-600 hover:bg-rose-500">{t("statusFailed")}</Badge>;
      default:
        return <Badge>{statusValue}</Badge>;
    }
  };

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">{t("title")}</h1>
        <form onSubmit={handleSearch} className="flex gap-4">
          <Input
            placeholder={t("tracePlaceholder")}
            value={traceId}
            onChange={(e) => setTraceId(e.target.value)}
            className="bg-zinc-900 border-zinc-800 text-white w-64 text-xs h-9"
          />
          <Select value={status} onValueChange={(val) => { setStatus(val); setPage(1); }}>
            <SelectTrigger className="bg-zinc-900 border-zinc-800 text-white w-40 text-xs h-9">
              <SelectValue placeholder={t("statusPlaceholder")} />
            </SelectTrigger>
            <SelectContent className="bg-zinc-900 border-zinc-800 text-white text-xs">
              <SelectItem value="all">{t("statusAll")}</SelectItem>
              <SelectItem value="accepted">{t("statusAccepted")}</SelectItem>
              <SelectItem value="failed">{t("statusFailed")}</SelectItem>
              <SelectItem value="conflict">{t("statusConflict")}</SelectItem>
            </SelectContent>
          </Select>
          <Button type="submit" size="sm" className="bg-emerald-600 hover:bg-emerald-500 text-xs">{tc("search")}</Button>
        </form>
      </div>

      <Card className="bg-zinc-900 border-zinc-800 text-white">
        <CardHeader>
          <CardTitle className="text-sm font-semibold">{t("cardTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-6 text-xs text-zinc-400 font-mono">{t("loading")}</div>
          ) : (
            <Table className="text-xs">
              <TableHeader className="border-b border-zinc-800">
                <TableRow>
                  <TableHead className="text-zinc-400">{t("colExternalSystem")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colRefCode")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colIdempotencyKey")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colStatus")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colTraceId")}</TableHead>
                  <TableHead className="text-zinc-400">{t("colTime")}</TableHead>
                  <TableHead className="text-zinc-400 text-right">{t("colActions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {messages.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center py-6 text-zinc-500">
                      {t("empty")}
                    </TableCell>
                  </TableRow>
                ) : (
                  messages.map((m) => (
                    <TableRow key={m.id} className="hover:bg-zinc-800/30">
                      <TableCell className="font-semibold">{m.externalSystem}</TableCell>
                      <TableCell className="font-mono">{m.externalReference}</TableCell>
                      <TableCell className="font-mono text-zinc-400 max-w-[150px] truncate" title={m.idempotencyKey}>
                        {m.idempotencyKey}
                      </TableCell>
                      <TableCell>{getStatusBadge(m.status)}</TableCell>
                      <TableCell className="font-mono text-zinc-500">{m.traceId}</TableCell>
                      <TableCell>{new Date(m.createdAt).toLocaleString("vi-VN")}</TableCell>
                      <TableCell className="text-right">
                        <Button
                          size="xs"
                          variant="outline"
                          onClick={() => setSelectedMessage(m)}
                          className="border-zinc-700 text-zinc-300 hover:text-white hover:bg-zinc-800 text-[10px] h-7"
                        >
                          {t("viewPayload")}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          )}

          <div className="flex justify-between items-center mt-4">
            <div className="text-[10px] text-zinc-500">{t("totalRecords", { total })}</div>
            <div className="flex gap-2">
              <Button
                size="xs"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="bg-zinc-800 border border-zinc-750 text-white text-[10px] h-7 disabled:opacity-50"
              >
                {tc("previous")}
              </Button>
              <Button
                size="xs"
                disabled={page * pageSize >= total}
                onClick={() => setPage(page + 1)}
                className="bg-zinc-800 border border-zinc-750 text-white text-[10px] h-7 disabled:opacity-50"
              >
                {tc("next")}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={selectedMessage !== null} onOpenChange={() => setSelectedMessage(null)}>
        <DialogContent className="bg-zinc-950 border-zinc-850 text-white max-w-3xl">
          <DialogHeader>
            <DialogTitle className="text-sm font-bold">
              {t("detailTitle", { ref: selectedMessage?.externalReference ?? "" })}
            </DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-4 text-xs font-sans max-h-[500px] overflow-y-auto">
            <div className="grid grid-cols-2 gap-2 bg-zinc-900/50 p-3 rounded border border-zinc-900">
              <div><span className="text-zinc-500">{t("senderSystem")}:</span> {selectedMessage?.externalSystem}</div>
              <div><span className="text-zinc-500">{t("messageType")}:</span> {selectedMessage?.messageType}</div>
              <div><span className="text-zinc-500">{t("colIdempotencyKey")}:</span> <code className="text-zinc-350">{selectedMessage?.idempotencyKey}</code></div>
              <div><span className="text-zinc-500">{t("colTraceId")}:</span> <code className="text-zinc-350">{selectedMessage?.traceId}</code></div>
            </div>

            {selectedMessage?.errorCode && (
              <div className="bg-rose-950/30 border border-rose-900 p-3 rounded text-rose-300">
                <span className="font-bold">{t("errorCode")}:</span> {selectedMessage.errorCode}
                <p className="mt-1 text-[11px] text-rose-400">{selectedMessage.errorMessage}</p>
              </div>
            )}

            <div>
              <div className="text-zinc-400 font-semibold mb-2">{t("requestPayload")}</div>
              <pre className="bg-zinc-900 p-3 rounded border border-zinc-800 overflow-x-auto text-[10px] font-mono text-emerald-400">
                {selectedMessage ? JSON.stringify(JSON.parse(selectedMessage.payload), null, 2) : ""}
              </pre>
            </div>

            {selectedMessage?.responsePayload && (
              <div>
                <div className="text-zinc-400 font-semibold mb-2">{t("responsePayload")}</div>
                <pre className="bg-zinc-900 p-3 rounded border border-zinc-800 overflow-x-auto text-[10px] font-mono text-cyan-400">
                  {JSON.stringify(JSON.parse(selectedMessage.responsePayload), null, 2)}
                </pre>
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
