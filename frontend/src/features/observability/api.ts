import api from "@/lib/api";
import {
  ObservabilitySummary,
  KpiSnapshot,
  ActivityTimelineEntry,
  OperationalAlert,
  TraceDetail,
  PagedResult
} from "./types";

export async function getObservabilitySummary(params?: {
  from?: string;
  to?: string;
}): Promise<ObservabilitySummary> {
  const res = await api.get<ObservabilitySummary>("/observability/summary", { params });
  return res.data;
}

export async function getKpiSnapshots(params?: {
  metricGroup?: string;
  metricKey?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<KpiSnapshot>> {
  const res = await api.get<PagedResult<KpiSnapshot>>("/observability/kpis", { params });
  return res.data;
}

export async function getTimeline(params?: {
  entityType?: string;
  entityId?: string;
  traceId?: string;
  severity?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<ActivityTimelineEntry>> {
  const res = await api.get<PagedResult<ActivityTimelineEntry>>("/observability/timeline", { params });
  return res.data;
}

export async function getEntityTimeline(entityType: string, entityId: string): Promise<ActivityTimelineEntry[]> {
  const res = await api.get<ActivityTimelineEntry[]>(`/observability/timeline/${entityType}/${entityId}`);
  return res.data;
}

export async function getAlerts(params?: {
  status?: string;
  severity?: string;
  alertType?: string;
  sourceModule?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<OperationalAlert>> {
  const res = await api.get<PagedResult<OperationalAlert>>("/observability/alerts", { params });
  return res.data;
}

export async function ackAlert(id: string, note?: string): Promise<{ success: boolean; status: string }> {
  const res = await api.post<{ success: boolean; status: string }>(`/observability/alerts/${id}/ack`, { note });
  return res.data;
}

export async function resolveAlert(id: string, note?: string): Promise<{ success: boolean; status: string }> {
  const res = await api.post<{ success: boolean; status: string }>(`/observability/alerts/${id}/resolve`, { note });
  return res.data;
}

export async function getTraceDetail(traceId: string): Promise<TraceDetail> {
  const res = await api.get<TraceDetail>(`/observability/traces/${traceId}`);
  return res.data;
}
