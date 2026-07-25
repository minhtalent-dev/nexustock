"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { fetchAttachmentBlob, downloadAttachmentBlob, type AttachmentItem } from "@/features/files/api";
import { showError } from "@/lib/toast";

interface AttachmentPreviewDialogProps {
  isOpen: boolean;
  onClose: () => void;
  item: AttachmentItem | null;
}

export function AttachmentPreviewDialog({ isOpen, onClose, item }: AttachmentPreviewDialogProps) {
  const t = useTranslations("Common.files");
  const tc = useTranslations("Common.actions");

  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [downloading, setDownloading] = useState(false);

  useEffect(() => {
    if (!isOpen || !item) {
      return;
    }

    let activeUrl: string | null = null;
    
    // Set loading state asynchronously to prevent react-hooks/set-state-in-effect
    Promise.resolve().then(() => {
      setLoading(true);
      setError(false);
    });

    const contentUrl = item.contentUrl ?? `/files/attachments/${item.id}/content?disposition=inline`;

    fetchAttachmentBlob(contentUrl)
      .then((blob) => {
        activeUrl = URL.createObjectURL(blob);
        setBlobUrl(activeUrl);
      })
      .catch(() => {
        setError(true);
        showError(t("loadContentFailed"));
      })
      .finally(() => {
        setLoading(false);
      });

    return () => {
      if (activeUrl) {
        URL.revokeObjectURL(activeUrl);
      }
      setBlobUrl(null);
      setError(false);
      setLoading(false);
    };
  }, [isOpen, item, t]);

  if (!item) return null;

  const isImage = item.kind === "IMAGE" || item.contentType.startsWith("image/");
  const isPdf = item.contentType === "application/pdf" || item.fileName.toLowerCase().endsWith(".pdf");

  const handleDownload = async () => {
    setDownloading(true);
    try {
      const downloadUrl = item.downloadUrl ?? `/files/attachments/${item.id}/content?disposition=attachment`;
      await downloadAttachmentBlob(downloadUrl, item.fileName);
    } catch {
      showError(t("downloadFailed"));
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-4xl max-h-[90vh] bg-card border-border text-foreground flex flex-col font-sans">
        <DialogHeader className="flex-shrink-0">
          <DialogTitle className="text-base font-semibold text-foreground truncate pr-6">
            {item.fileName}
          </DialogTitle>
        </DialogHeader>

        <div className="flex-1 min-h-[350px] max-h-[70vh] flex items-center justify-center bg-zinc-950/60 rounded-md border border-border overflow-hidden relative p-2">
          {loading && (
            <div className="text-sm text-muted-foreground animate-pulse">
              {t("loadingPreview")}
            </div>
          )}

          {!loading && error && (
            <div className="text-center space-y-3">
              <p className="text-xs text-rose-400">{t("previewError")}</p>
              <Button type="button" variant="outline" size="sm" onClick={handleDownload} disabled={downloading}>
                {downloading ? t("downloading") : t("downloadBtn")}
              </Button>
            </div>
          )}

          {!loading && !error && blobUrl && (
            <>
              {isImage && (
                /* eslint-disable-next-line @next/next/no-img-element */
                <img
                  src={blobUrl}
                  alt={item.fileName}
                  className="max-w-full max-h-[65vh] object-contain rounded"
                />
              )}
              {isPdf && (
                <iframe
                  src={blobUrl}
                  title={item.fileName}
                  className="w-full h-[65vh] border-0 rounded"
                />
              )}
              {!isImage && !isPdf && (
                <div className="text-center space-y-3 p-4">
                  <p className="text-xs text-muted-foreground">{t("unsupportedInline")}</p>
                  <Button type="button" variant="outline" size="sm" onClick={handleDownload} disabled={downloading}>
                    {downloading ? t("downloading") : t("downloadBtn")}
                  </Button>
                </div>
              )}
            </>
          )}
        </div>

        <DialogFooter className="flex-shrink-0 gap-2 sm:justify-between items-center pt-2">
          <div className="text-xs text-muted-foreground truncate">
            {item.kind} · {(item.sizeBytes / 1024).toFixed(1)} KB
          </div>
          <div className="flex gap-2">
            <Button type="button" variant="outline" size="sm" onClick={handleDownload} disabled={downloading} className="h-8 text-xs">
              {downloading ? t("downloading") : t("downloadBtn")}
            </Button>
            <Button type="button" variant="secondary" size="sm" onClick={onClose} className="h-8 text-xs">
              {tc("close")}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
