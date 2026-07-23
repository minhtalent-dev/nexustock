import api from "@/lib/api";

export type UploadResult = {
  fileName: string;
  contentType: string;
  sizeBytes: number;
  kind: string;
  provider: string;
  storageKey: string;
  url: string;
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
  storageKey: string;
  url: string;
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
  entityType: string;
  entityId: string;
  url: string;
  provider: string;
  storageKey: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  kind: string;
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
