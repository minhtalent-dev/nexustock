"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
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
  const t = useTranslations("Common.files");
  const tc = useTranslations("Common.actions");
  const ts = useTranslations("Common.states");

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
      showError(getHttpErrorMessage(err, t("loadFailed")));
    } finally {
      setLoading(false);
    }
  }, [entityId, entityType, t]);

  useEffect(() => {
    const timer = setTimeout(() => {
      void refresh();
    }, 0);
    return () => clearTimeout(timer);
  }, [refresh]);

  const onFile = async (file: File | null) => {
    if (!file) return;
    setUploading(true);
    try {
      const uploaded = await uploadFile(file);
      if (!entityId) {
        onPendingChange?.([...pendingUploads, uploaded]);
        showSuccess(t("toastUploaded"));
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
      showSuccess(t("toastSaved"));
      await refresh();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("uploadFailed")));
    } finally {
      setUploading(false);
    }
  };

  const onDelete = async (id: string) => {
    try {
      await deleteAttachment(id);
      showSuccess(t("toastRemoved"));
      await refresh();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("deleteFailed")));
    }
  };

  const displayPending = !entityId ? pendingUploads : [];

  return (
    <div className="space-y-3 rounded-lg border border-border bg-card/40 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-foreground">{t("attachments")}</h3>
          <p className="text-xs text-muted-foreground">{t("hint")}</p>
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
            {uploading ? t("uploading") : t("uploadBtn")}
          </span>
        </label>
      </div>

      {loading ? <p className="text-xs text-muted-foreground">{ts("loading")}</p> : null}

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
              {tc("delete")}
            </Button>
          </li>
        ))}
        {displayPending.map((item) => (
          <li key={item.storageKey} className="rounded-md border border-dashed border-border px-3 py-2 text-sm text-muted-foreground">
            {t("pendingUploads")}: {item.fileName}
          </li>
        ))}
        {!loading && items.length === 0 && displayPending.length === 0 ? (
          <li className="text-xs text-muted-foreground">{t("noAttachments")}</li>
        ) : null}
      </ul>
    </div>
  );
}
