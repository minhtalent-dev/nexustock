import api from "@/lib/api";
import type { CreatePrintJobRequest, PrintJobDto, ReprintJobRequest } from "./types";

export async function createPrintJob(request: CreatePrintJobRequest): Promise<PrintJobDto> {
  const response = await api.post<PrintJobDto>("/printing/jobs", request);
  return response.data;
}

export async function reprintJob(printJobId: string, request: ReprintJobRequest): Promise<PrintJobDto> {
  const response = await api.post<PrintJobDto>(`/printing/jobs/${printJobId}/reprint`, request);
  return response.data;
}
