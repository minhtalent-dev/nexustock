"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import {
  bindAttachment,
  deleteAttachment,
  downloadAttachmentBlob,
  listAttachments,
  uploadFile,
  type AttachmentItem,
  type UploadResult,
} from "@/features/files/api";
import { AttachmentPreviewDialog } from "@/features/files/attachment-preview-dialog";

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
  const [previewItem, setPreviewItem] = useState<AttachmentItem | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

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
        uploadId: uploaded.uploadId,
        entityType,
        entityId,
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

  const onDownload = async (item: AttachmentItem) => {
    setDownloadingId(item.id);
    try {
      const downloadUrl = item.downloadUrl ?? `/files/attachments/${item.id}/content?disposition=attachment`;
      await downloadAttachmentBlob(downloadUrl, item.fileName);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("downloadFailed")));
    } finally {
      setDownloadingId(null);
    }
  };

  const displayPending = !entityId ? pendingUploads : [];

  return (
    <div className="space-y-3 rounded-lg border border-border bg-card/40 p-4 font-sans">
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
          <span className="inline-flex h-8 items-center rounded-md bg-primary px-3 text-xs font-medium text-primary-foreground hover:bg-primary/90 transition-colors">
            {uploading ? t("uploading") : t("uploadBtn")}
          </span>
        </label>
      </div>

      {loading ? <p className="text-xs text-muted-foreground">{ts("loading")}</p> : null}

      <ul className="space-y-2">
        {items.map((item) => {
          const isPreviewable = item.kind === "IMAGE" || item.contentType.startsWith("image/") || item.contentType === "application/pdf" || item.fileName.toLowerCase().endsWith(".pdf");
          return (
            <li key={item.id} className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2 text-sm bg-card">
              <div className="min-w-0 flex-1">
                <button
                  type="button"
                  onClick={() => (isPreviewable ? setPreviewItem(item) : void onDownload(item))}
                  className="font-medium text-left truncate text-primary hover:underline block max-w-full text-xs sm:text-sm"
                >
                  {item.fileName}
                </button>
                <div className="text-[11px] text-muted-foreground mt-0.5">
                  {item.kind} · {(item.sizeBytes / 1024).toFixed(1)} KB
                </div>
              </div>
              <div className="flex items-center gap-1 flex-shrink-0">
                {isPreviewable && (
                  <Button type="button" variant="outline" size="xs" onClick={() => setPreviewItem(item)} className="h-7 text-xs px-2">
                    {t("previewBtn")}
                  </Button>
                )}
                <Button
                  type="button"
                  variant="outline"
                  size="xs"
                  onClick={() => void onDownload(item)}
                  disabled={downloadingId === item.id}
                  className="h-7 text-xs px-2"
                >
                  {downloadingId === item.id ? t("downloading") : t("downloadBtn")}
                </Button>
                <Button type="button" variant="destructive" size="xs" onClick={() => void onDelete(item.id)} className="h-7 text-xs px-2">
                  {tc("delete")}
                </Button>
              </div>
            </li>
          );
        })}
        {displayPending.map((item) => (
          <li key={item.uploadId} className="rounded-md border border-dashed border-border px-3 py-2 text-sm text-muted-foreground">
            {t("pendingUploads")}: {item.fileName}
          </li>
        ))}
        {!loading && items.length === 0 && displayPending.length === 0 ? (
          <li className="text-xs text-muted-foreground">{t("noAttachments")}</li>
        ) : null}
      </ul>

      <AttachmentPreviewDialog
        isOpen={!!previewItem}
        onClose={() => setPreviewItem(null)}
        item={previewItem}
      />
    </div>
  );
}
