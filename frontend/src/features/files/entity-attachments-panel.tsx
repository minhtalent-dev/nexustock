"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { useAuth } from "@/hooks/use-auth";
import { useConfirmDialog } from "@/lib/confirm-dialog";
import {
  bindAttachment,
  deleteAttachment,
  downloadAttachmentBlob,
  listAttachments,
  uploadFile,
  type AttachmentItem,
  type AttachmentUploadSource,
  type UploadResult,
} from "@/features/files/api";
import { AttachmentPreviewDialog } from "@/features/files/attachment-preview-dialog";
import { AttachmentThumbnail } from "./attachment-thumbnail";
import { RfCameraUpload } from "./rf-camera-upload";

type Props = {
  entityType: string;
  entityId: string | null;
  pendingUploads?: UploadResult[];
  onPendingChange?: (items: UploadResult[]) => void;
  enableRfCapture?: boolean;
};

export function EntityAttachmentsPanel({
  entityType,
  entityId,
  pendingUploads = [],
  onPendingChange,
  enableRfCapture = false,
}: Props) {
  const t = useTranslations("Common.files");
  const tc = useTranslations("Common.actions");
  const ts = useTranslations("Common.states");

  const { hasPermission } = useAuth();
  const confirm = useConfirmDialog();

  const canRead = hasPermission("files.read");
  const canUpload = hasPermission("files.upload");
  const canDelete = hasPermission("files.delete");

  const [items, setItems] = useState<AttachmentItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [previewItem, setPreviewItem] = useState<AttachmentItem | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!entityId || !canRead) {
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
  }, [entityId, entityType, canRead, t]);

  useEffect(() => {
    const timer = setTimeout(() => {
      void refresh();
    }, 0);
    return () => clearTimeout(timer);
  }, [refresh]);

  const onFileUpload = async (file: File, source: AttachmentUploadSource): Promise<boolean> => {
    if (!file || !canUpload) return false;
    setUploading(true);
    try {
      const uploaded = await uploadFile(file);
      if (!entityId) {
        onPendingChange?.([...pendingUploads, uploaded]);
        showSuccess(t("toastUploaded"));
        return true;
      }
      await bindAttachment({
        uploadId: uploaded.uploadId,
        entityType,
        entityId,
        source,
      });
      showSuccess(t("toastSaved"));
      await refresh();
      return true;
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("uploadFailed")));
      return false;
    } finally {
      setUploading(false);
    }
  };

  const onDelete = async (id: string) => {
    if (!canDelete) return;
    const ok = await confirm({
      title: t("confirmDeleteTitle"),
      description: t("confirmDeleteDescription"),
      confirmText: tc("delete"),
      cancelText: tc("cancel"),
      tone: "danger",
    });
    if (!ok) return;

    try {
      await deleteAttachment(id);
      showSuccess(t("toastRemoved"));
      await refresh();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("deleteFailed")));
    }
  };

  const onDownload = async (item: AttachmentItem) => {
    if (!canRead) return;
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

  if (!canRead) {
    return (
      <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive font-sans">
        {t("noPermissionRead")}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-card/40 p-4 font-sans">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-foreground">{t("attachments")}</h3>
          <p className="text-xs text-muted-foreground">{t("hint")}</p>
        </div>
      </div>

      {canUpload && (
        <RfCameraUpload
          uploading={uploading}
          enableRfCapture={enableRfCapture}
          onUpload={onFileUpload}
        />
      )}

      {loading ? <p className="text-xs text-muted-foreground">{ts("loading")}</p> : null}

      <ul className="flex flex-col gap-2">
        {items.map((item) => {
          const isPreviewable = item.kind === "IMAGE" || item.contentType.startsWith("image/") || item.contentType === "application/pdf" || item.fileName.toLowerCase().endsWith(".pdf");
          return (
            <li key={item.id} className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2 text-sm bg-card">
              <div className="flex items-center gap-3 min-w-0 flex-1">
                <AttachmentThumbnail item={item} />
                <div className="min-w-0 flex-1">
                  <button
                    type="button"
                    onClick={() => (isPreviewable ? setPreviewItem(item) : void onDownload(item))}
                    className="font-medium text-left truncate text-primary hover:underline block max-w-full text-xs sm:text-sm"
                  >
                    {item.fileName}
                  </button>
                  <div className="text-[10px] text-muted-foreground mt-0.5 flex flex-wrap gap-x-2 gap-y-0.5">
                    <span>{item.contentType}</span>
                    <span>•</span>
                    <span>{(item.sizeBytes / 1024).toFixed(1)} KB</span>
                    <span>•</span>
                    <span>{item.provider}</span>
                    <span>•</span>
                    <span>{new Date(item.createdAt).toLocaleString()}</span>
                  </div>
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
                {canDelete && (
                  <Button type="button" variant="destructive" size="xs" onClick={() => void onDelete(item.id)} className="h-7 text-xs px-2">
                    {tc("delete")}
                  </Button>
                )}
              </div>
            </li>
          );
        })}
        {pendingUploads.map((item) => (
          <li key={item.uploadId} className="flex items-center justify-between gap-2 rounded-md border border-dashed border-border px-3 py-2 text-sm bg-card/10 text-muted-foreground">
            <div className="min-w-0 flex-1 truncate">
              {t("pendingUploads")}: <span className="font-medium text-foreground">{item.fileName}</span>
            </div>
            {canUpload && (
              <Button
                type="button"
                variant="outline"
                size="xs"
                onClick={() => onPendingChange?.(pendingUploads.filter(p => p.uploadId !== item.uploadId))}
                className="h-7 text-xs px-2"
              >
                {t("removePendingBtn")}
              </Button>
            )}
          </li>
        ))}
        {!loading && items.length === 0 && pendingUploads.length === 0 ? (
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
