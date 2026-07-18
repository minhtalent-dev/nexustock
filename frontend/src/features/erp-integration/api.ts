import api from "@/lib/api";
import { IntegrationMessage, IntegrationMapping, ImportPreviewResult } from "./types";

export async function getIntegrationMessages(params: {
  status?: string;
  messageType?: string;
  externalSystem?: string;
  traceId?: string;
  page?: number;
  pageSize?: number;
}) {
  const res = await api.get<{ total: number; items: IntegrationMessage[] }>("/integration/messages", { params });
  return res.data;
}

export async function getIntegrationMappings(params: {
  mappingType?: string;
  externalSystem?: string;
  externalCode?: string;
  page?: number;
  pageSize?: number;
}) {
  const res = await api.get<{ total: number; items: IntegrationMapping[] }>("/integration/mappings", { params });
  return res.data;
}

export async function createIntegrationMapping(data: {
  externalSystem: string;
  mappingType: string;
  externalCode: string;
  internalCode: string;
}) {
  const res = await api.post<IntegrationMapping>("/integration/mappings", data);
  return res.data;
}

export async function updateIntegrationMapping(id: string, data: {
  internalCode: string;
  status: string;
}) {
  const res = await api.put<IntegrationMapping>(`/integration/mappings/${id}`, data);
  return res.data;
}

export async function deleteIntegrationMapping(id: string) {
  await api.delete(`/integration/mappings/${id}`);
}

export async function previewImportMappings(externalSystem: string, file: File) {
  const formData = new FormData();
  formData.append("file", file);
  const res = await api.post<ImportPreviewResult>(`/integration/import/preview?externalSystem=${externalSystem}`, formData, {
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });
  return res.data;
}

export async function commitImportMappings(jobId: string) {
  const res = await api.post<ImportPreviewResult>(`/integration/import/commit/${jobId}`);
  return res.data;
}
