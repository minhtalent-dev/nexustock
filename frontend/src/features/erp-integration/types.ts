export interface IntegrationMessage {
  id: string;
  tenantId: string;
  idempotencyKey: string;
  payloadHash: string;
  externalSystem: string;
  externalReference: string;
  contractVersion: string;
  direction: "inbound" | "outbound";
  messageType: string;
  payload: string;
  responsePayload?: string;
  status: "accepted" | "failed" | "conflict";
  errorCode?: string;
  errorMessage?: string;
  traceId: string;
  createdAt: string;
  updatedAt?: string;
}

export interface IntegrationMapping {
  id: string;
  tenantId: string;
  externalSystem: string;
  mappingType: "item" | "warehouse" | "partner" | "uom";
  externalCode: string;
  internalCode: string;
  status: "active" | "inactive";
  createdAt: string;
  updatedAt?: string;
}

export interface IntegrationImportJob {
  id: string;
  tenantId: string;
  importType: string;
  fileName: string;
  status: string;
  totalRows: number;
  validRows: number;
  errorRows: number;
  previewPayload: string;
  traceId: string;
  createdAt: string;
  expiresAt: string;
}

export interface ImportPreviewRow {
  rowIndex: number;
  rawData: Record<string, string>;
  isValid: boolean;
  errorMessage?: string;
}

export interface ImportPreviewResult {
  jobId: string;
  importType: string;
  status: string;
  totalRows: number;
  validRows: number;
  errorRows: number;
  rows: ImportPreviewRow[];
  message?: string;
}
