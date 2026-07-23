"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import {
  bindAttachment,
  deleteAttachment,
  listAttachments,
  uploadFile,
  type AttachmentItem,
  type UploadResult,
} from "@/features/files/api";

type Props = {
  entityType: string;
  entityId: string | null;
  pendingUploads?: UploadResult[];
  onPendingChange?: (items: UploadResult[]) => void;
};

export function EntityAttachmentsPanel({ entityType, entityId, pendingUploads = [], onPendingChange }: Props) {
  const [items, setItems] = useState<AttachmentItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);

  const refresh = useCallback(async () => {
    if (!entityId) {
      setItems([]);
      return;
    }
    setLoading(true);
    try {
      setItems(await listAttachments(entityType, entityId));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Failed to load attachments"));
    } finally {
      setLoading(false);
    }
  }, [entityId, entityType]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const onFile = async (file: File | null) => {
    if (!file) return;
    setUploading(true);
    try {
      const uploaded = await uploadFile(file);
      if (!entityId) {
        onPendingChange?.([...pendingUploads, uploaded]);
        showSuccess("File uploaded — will bind after save");
        return;
      }
      await bindAttachment({
        entityType,
        entityId,
        url: uploaded.url,
        provider: uploaded.provider,
        storageKey: uploaded.storageKey,
        fileName: uploaded.fileName,
        contentType: uploaded.contentType,
        sizeBytes: uploaded.sizeBytes,
        kind: uploaded.kind,
      });
      showSuccess("Attachment saved");
      await refresh();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Upload failed"));
    } finally {
      setUploading(false);
    }
  };

  const onDelete = async (id: string) => {
    try {
      await deleteAttachment(id);
      showSuccess("Attachment removed");
      await refresh();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Delete failed"));
    }
  };

  const displayPending = !entityId ? pendingUploads : [];

  return (
    <div className="space-y-3 rounded-lg border border-border bg-card/40 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-foreground">Attachments</h3>
          <p className="text-xs text-muted-foreground">Images (jpeg/png/webp) and PDF up to 10 MB</p>
        </div>
        <label className="inline-flex cursor-pointer">
          <input
            type="file"
            className="hidden"
            accept=".jpg,.jpeg,.png,.webp,.pdf"
            disabled={uploading}
            onChange={(e) => void onFile(e.target.files?.[0] ?? null)}
          />
          <span className="inline-flex h-8 items-center rounded-md bg-primary px-3 text-xs font-medium text-primary-foreground">
            {uploading ? "Uploading…" : "Upload"}
          </span>
        </label>
      </div>

      {loading ? <p className="text-xs text-muted-foreground">Loading…</p> : null}

      <ul className="space-y-2">
        {items.map((item) => (
          <li key={item.id} className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2 text-sm">
            <div className="min-w-0">
              <a href={item.url} target="_blank" rel="noreferrer" className="truncate text-primary underline">
                {item.fileName}
              </a>
              <div className="text-xs text-muted-foreground">
                {item.kind} · {item.provider}
              </div>
            </div>
            <Button type="button" variant="destructive" size="xs" onClick={() => void onDelete(item.id)}>
              Delete
            </Button>
          </li>
        ))}
        {displayPending.map((item) => (
          <li key={item.storageKey} className="rounded-md border border-dashed border-border px-3 py-2 text-sm text-muted-foreground">
            Pending: {item.fileName}
          </li>
        ))}
        {!loading && items.length === 0 && displayPending.length === 0 ? (
          <li className="text-xs text-muted-foreground">No attachments yet</li>
        ) : null}
      </ul>
    </div>
  );
}
