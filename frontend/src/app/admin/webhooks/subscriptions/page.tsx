"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { getSubscriptions, createSubscription, updateSubscription, deleteSubscription } from "@/features/webhook/api";
import { WebhookSubscription } from "@/features/webhook/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { toast } from "sonner";

export default function WebhookSubscriptionsPage() {
  const t = useTranslations("Admin.webhooks.subscriptions");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [subscriptions, setSubscriptions] = useState<WebhookSubscription[]>([]);
  const [loading, setLoading] = useState(false);

  const [createOpen, setCreateOpen] = useState(false);
  const [newTargetUrl, setNewTargetUrl] = useState("");
  const [newEventTypes, setNewEventTypes] = useState("");
  const [creating, setCreating] = useState(false);

  const [secretKeyOpen, setSecretKeyOpen] = useState(false);
  const [revealedSecretKey, setRevealedSecretKey] = useState("");

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      try {
        const data = await getSubscriptions();
        if (active) setSubscriptions(data);
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
  }, [t, tErrors]);

  const fetchSubscriptions = () => {
    getSubscriptions()
      .then(setSubscriptions)
      .catch((err) => {
        const { codeLabel, message } = resolveApiError(err, tErrors);
        showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
      });
  };

  const handleCreate = async () => {
    if (!newTargetUrl.trim()) return;
    const eventTypesArr = newEventTypes
      .split(",")
      .map((e) => e.trim())
      .filter(Boolean);
    if (eventTypesArr.length === 0) return;

    setCreating(true);
    try {
      const res = await createSubscription({ targetUrl: newTargetUrl, eventTypes: eventTypesArr });
      setRevealedSecretKey(res.secretKey);
      setSecretKeyOpen(true);
      setCreateOpen(false);
      setNewTargetUrl("");
      setNewEventTypes("");
      fetchSubscriptions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setCreating(false);
    }
  };

  const handleToggleActive = async (sub: WebhookSubscription) => {
    try {
      await updateSubscription(sub.id, { isActive: !sub.isActive });
      fetchSubscriptions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.updateFailed"));
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteSubscription(id);
      toast.success(t("toastDisabled"));
      fetchSubscriptions();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.deleteFailed"));
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t("title")}</h1>
          <p className="text-muted-foreground text-sm mt-1">{t("subtitle")}</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>{t("newSubscription")}</Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("listTitle", { count: subscriptions.length })}</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-muted-foreground">{tc("loading")}</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("colTargetUrl")}</TableHead>
                  <TableHead>{t("colEventTypes")}</TableHead>
                  <TableHead>{t("colStatus")}</TableHead>
                  <TableHead>{t("colCreated")}</TableHead>
                  <TableHead className="text-right">{t("colActions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {subscriptions.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-muted-foreground">
                      {t("empty")}
                    </TableCell>
                  </TableRow>
                )}
                {subscriptions.map((sub) => (
                  <TableRow key={sub.id}>
                    <TableCell className="font-mono text-xs max-w-xs truncate">{sub.targetUrl}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {sub.eventTypes.map((et) => (
                          <Badge key={et} variant="secondary" className="text-xs">{et}</Badge>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={sub.isActive ? "default" : "outline"}>
                        {sub.isActive ? t("active") : t("inactive")}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {new Date(sub.createdAt).toLocaleDateString("vi-VN")}
                    </TableCell>
                    <TableCell className="text-right space-x-2">
                      <Button size="sm" variant="ghost" onClick={() => handleToggleActive(sub)}>
                        {sub.isActive ? t("disable") : t("enable")}
                      </Button>
                      <Button size="sm" variant="destructive" onClick={() => handleDelete(sub.id)}>
                        {tc("delete")}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("createDialogTitle")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <label className="text-sm font-medium">{t("targetUrlLabel")}</label>
              <Input
                placeholder={t("targetUrlPlaceholder")}
                value={newTargetUrl}
                onChange={(e) => setNewTargetUrl(e.target.value)}
                className="mt-1"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t("eventTypesLabel")}</label>
              <Input
                placeholder={t("eventTypesPlaceholder")}
                value={newEventTypes}
                onChange={(e) => setNewEventTypes(e.target.value)}
                className="mt-1"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>{tc("cancel")}</Button>
            <Button onClick={handleCreate} disabled={creating}>
              {creating ? tc("creating") : tc("create")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={secretKeyOpen} onOpenChange={setSecretKeyOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("secretDialogTitle")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <p className="text-sm text-muted-foreground">
              {t.rich("secretDialogHint", {
                strong: (chunks) => <strong>{chunks}</strong>,
              })}
            </p>
            <div className="bg-muted rounded p-3 font-mono text-xs break-all select-all">
              {revealedSecretKey}
            </div>
          </div>
          <DialogFooter>
            <Button onClick={() => { navigator.clipboard.writeText(revealedSecretKey); toast.success(t("copied")); }}>
              {t("copyClipboard")}
            </Button>
            <Button variant="ghost" onClick={() => setSecretKeyOpen(false)}>{tc("close")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
    </PageShell>
  );
}
