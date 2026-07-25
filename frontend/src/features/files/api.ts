import api from "@/lib/api";

export type UploadResult = {
  uploadId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  kind: string;
  provider: string;
  urlForLegacyCompat?: string;
  expiresAt: string;
};

export type AttachmentItem = {
  id: string;
  entityType: string;
  entityId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  kind: string;
  provider: string;
  previewKind?: "image" | "pdf" | "download";
  contentUrl: string;
  downloadUrl: string;
  thumbnailUrl?: string | null;
  createdAt: string;
};

export type StorageSettings = {
  activeProvider: string;
  publicBaseUrl: string | null;
  localPathConfigured: boolean;
  providers: Array<{ id: string; label: string; configured: boolean }>;
  lastTestAt: string | null;
  lastTestOk: boolean | null;
  lastTestMessage: string | null;
};

export async function uploadFile(file: File): Promise<UploadResult> {
  const formData = new FormData();
  formData.append("file", file);
  const res = await api.post<UploadResult>("/files/upload", formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data;
}

export async function bindAttachment(payload: {
  uploadId: string;
  entityType: string;
  entityId: string;
}): Promise<AttachmentItem> {
  const res = await api.post<AttachmentItem>("/files/attachments", payload);
  return res.data;
}

export async function listAttachments(entityType: string, entityId: string): Promise<AttachmentItem[]> {
  const res = await api.get<{ items: AttachmentItem[] }>("/files/attachments", {
    params: { entityType, entityId },
  });
  return res.data.items;
}

export async function deleteAttachment(id: string): Promise<void> {
  await api.delete(`/files/attachments/${id}`);
}

export async function fetchAttachmentBlob(contentUrl: string, signal?: AbortSignal): Promise<Blob> {
  let path = contentUrl;
  if (path.startsWith("http://") || path.startsWith("https://")) {
    try {
      const u = new URL(path);
      path = u.pathname + u.search;
    } catch {
      // ignore
    }
  }
  if (path.startsWith("/api/")) {
    path = path.substring(5);
  } else if (path.startsWith("api/")) {
    path = path.substring(4);
  } else if (path.startsWith("/")) {
    path = path.substring(1);
  }

  const res = await api.get<Blob>(path, { responseType: "blob", signal });
  return res.data;
}

export async function downloadAttachmentBlob(downloadUrl: string, fileName: string): Promise<void> {
  const blob = await fetchAttachmentBlob(downloadUrl);
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export async function getStorageSettings(): Promise<StorageSettings> {
  const res = await api.get<StorageSettings>("/files/storage-settings");
  return res.data;
}

export async function saveStorageSettings(body: Record<string, unknown>): Promise<StorageSettings> {
  const res = await api.put<StorageSettings>("/files/storage-settings", body);
  return res.data;
}

export async function testStorageSettings(body?: Record<string, unknown>): Promise<{ ok: boolean; message: string }> {
  const res = await api.post<{ ok: boolean; message: string }>("/files/storage-settings/test", body ?? {});
  return res.data;
}
