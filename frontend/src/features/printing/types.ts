export type PrintJobStatus = "queued" | "sending" | "printed" | "failed" | "cancelled";

export interface PrintJobDto {
  id: string;
  templateId: string;
  templateCode: string;
  printerCode: string;
  status: PrintJobStatus;
  idempotencyKey: string;
  renderedCommandHash: string;
  sourceJobId?: string | null;
  reasonCode?: string | null;
  reprintCount: number;
  createdAt: string;
  errorMessage?: string | null;
}

export interface CreatePrintJobRequest {
  templateId: string;
  printerCode: string;
  payload: Record<string, string>;
  idempotencyKey: string;
}

export interface ReprintJobRequest {
  reasonCode: string;
  idempotencyKey: string;
}

export type LocalPrinterConnectionState =
  | "idle"
  | "connecting"
  | "connected"
  | "unavailable"
  | "printing"
  | "printed"
  | "error";

export interface LocalPrinterStatus {
  printerCode: string;
  status: string;
  port?: number;
  error?: string;
}

export interface LocalPrinterPrintResult {
  success: boolean;
  message?: string;
}
