"use client";

import { useEffect, useState } from "react";
import { getSubscriptions, createSubscription, updateSubscription, deleteSubscription } from "@/features/webhook/api";
import { WebhookSubscription } from "@/features/webhook/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showError } from "@/lib/toast";
import { toast } from "sonner";

export default function WebhookSubscriptionsPage() {
  const [subscriptions, setSubscriptions] = useState<WebhookSubscription[]>([]);
  const [loading, setLoading] = useState(false);

  // Create dialog
  const [createOpen, setCreateOpen] = useState(false);
  const [newTargetUrl, setNewTargetUrl] = useState("");
  const [newEventTypes, setNewEventTypes] = useState("");
  const [creating, setCreating] = useState(false);

  // Secret key reveal dialog
  const [secretKeyOpen, setSecretKeyOpen] = useState(false);
  const [revealedSecretKey, setRevealedSecretKey] = useState("");

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      try {
        const data = await getSubscriptions();
        if (active) setSubscriptions(data);
      } catch {
        showError("Không thể tải danh sách subscription.");
      } finally {
        if (active) setLoading(false);
      }
    }
    load();
    return () => {
      active = false;
    };
  }, []);

  const fetchSubscriptions = () => {
    // Kích hoạt reload bằng cách thay đổi trigger state nếu cần, ở đây chỉ đơn giản là gọi lại load
    getSubscriptions().then(setSubscriptions).catch(() => showError("Không thể tải danh sách subscription."));
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
    } catch {
      showError("Tạo subscription thất bại.");
    } finally {
      setCreating(false);
    }
  };

  const handleToggleActive = async (sub: WebhookSubscription) => {
    try {
      await updateSubscription(sub.id, { isActive: !sub.isActive });
      fetchSubscriptions();
    } catch {
      showError("Cập nhật thất bại.");
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteSubscription(id);
      toast.success("Đã vô hiệu hóa subscription.");
      fetchSubscriptions();
    } catch {
      showError("Xóa subscription thất bại.");
    }
  };

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Webhook Subscriptions</h1>
          <p className="text-muted-foreground text-sm mt-1">Quản lý các endpoint nhận webhook từ Nexustock.</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>+ New Subscription</Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Subscriptions ({subscriptions.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-muted-foreground">Đang tải...</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Target URL</TableHead>
                  <TableHead>Event Types</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {subscriptions.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-muted-foreground">
                      Chưa có subscription nào.
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
                        {sub.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {new Date(sub.createdAt).toLocaleDateString("vi-VN")}
                    </TableCell>
                    <TableCell className="text-right space-x-2">
                      <Button size="sm" variant="ghost" onClick={() => handleToggleActive(sub)}>
                        {sub.isActive ? "Disable" : "Enable"}
                      </Button>
                      <Button size="sm" variant="destructive" onClick={() => handleDelete(sub.id)}>
                        Delete
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New Webhook Subscription</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <label className="text-sm font-medium">Target URL</label>
              <Input
                placeholder="https://your-service.com/webhook"
                value={newTargetUrl}
                onChange={(e) => setNewTargetUrl(e.target.value)}
                className="mt-1"
              />
            </div>
            <div>
              <label className="text-sm font-medium">Event Types (comma-separated)</label>
              <Input
                placeholder="inbound.completed, shipment.confirmed"
                value={newEventTypes}
                onChange={(e) => setNewEventTypes(e.target.value)}
                className="mt-1"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={handleCreate} disabled={creating}>
              {creating ? "Creating..." : "Create"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Secret Key Reveal Dialog */}
      <Dialog open={secretKeyOpen} onOpenChange={setSecretKeyOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Secret Key — Save Now</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <p className="text-sm text-muted-foreground">
              Secret key chỉ hiển thị <strong>1 lần duy nhất</strong>. Lưu lại ngay để dùng xác thực HMAC signature.
            </p>
            <div className="bg-muted rounded p-3 font-mono text-xs break-all select-all">
              {revealedSecretKey}
            </div>
          </div>
          <DialogFooter>
            <Button onClick={() => { navigator.clipboard.writeText(revealedSecretKey); toast.success("Copied!"); }}>
              Copy to Clipboard
            </Button>
            <Button variant="ghost" onClick={() => setSecretKeyOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
