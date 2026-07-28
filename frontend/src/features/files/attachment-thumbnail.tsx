"use client";

import { useEffect, useState } from "react";
import { File, FileImage, FileText } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { fetchAttachmentBlob, type AttachmentItem } from "@/features/files/api";

interface AttachmentThumbnailProps {
  item: AttachmentItem;
}

export function AttachmentThumbnail({ item }: AttachmentThumbnailProps) {
  const [loadedThumbnail, setLoadedThumbnail] = useState<{
    sourceUrl: string;
    objectUrl: string;
  } | null>(null);
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  const thumbnailUrl = item.thumbnailUrl ?? null;

  useEffect(() => {
    if (!thumbnailUrl) return;

    const controller = new AbortController();
    let objectUrl: string | null = null;

    void fetchAttachmentBlob(thumbnailUrl, controller.signal)
      .then((blob) => {
        if (!blob.type.startsWith("image/")) {
          throw new Error("Thumbnail response is not an image");
        }
        objectUrl = URL.createObjectURL(blob);
        setLoadedThumbnail({ sourceUrl: thumbnailUrl, objectUrl });
        setFailedUrl(null);
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setFailedUrl(thumbnailUrl);
        }
      });

    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [thumbnailUrl]);

  const src = loadedThumbnail?.sourceUrl === thumbnailUrl
    ? loadedThumbnail.objectUrl
    : null;
  const failed = failedUrl === thumbnailUrl;

  if (thumbnailUrl && !src && !failed) {
    return <Skeleton className="h-12 w-12 shrink-0 rounded-md" />;
  }

  if (src) {
    return (
      // Endpoint authenticated và backend đã resize ảnh; next/image không phù hợp Blob URL.
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={src}
        alt={item.fileName}
        className="h-10 w-10 flex-shrink-0 rounded border border-border bg-background object-cover"
      />
    );
  }

  const isImage = item.previewKind === "image" || item.contentType.startsWith("image/");
  const isPdf = item.previewKind === "pdf" || item.contentType === "application/pdf";
  const iconClass = "h-5 w-5 text-muted-foreground";

  return (
    <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded border border-border bg-muted/30">
      {isImage ? (
        <FileImage className={iconClass} />
      ) : isPdf ? (
        <FileText className={iconClass} />
      ) : (
        <File className={iconClass} />
      )}
    </div>
  );
}
