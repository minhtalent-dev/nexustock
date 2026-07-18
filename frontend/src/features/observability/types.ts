export interface KpiCard {
  metricKey: string;
  label: string;
  value: number;
  unit: 'count' | 'percent' | 'minutes';
  trend: 'up' | 'down' | 'flat' | 'stale' | 'unavailable';
}

export interface ObservabilitySummary {
  period: {
    from: string;
    to: string;
  };
  cards: KpiCard[];
  activeAlerts: number;
  traceId: string;
}

export interface KpiSnapshot {
  id: string;
  tenantId: string;
  metricKey: string;
  metricGroup: string;
  value: number;
  unit: string;
  periodStart: string;
  periodEnd: string;
  sourceModule: string;
  computedAt: string;
}

export interface ActivityTimelineEntry {
  id: string;
  tenantId: string;
  entityType: string;
  entityId: string;
  eventType: string;
  title: string;
  description?: string;
  severity: 'info' | 'warning' | 'critical';
  actorUserId?: string;
  actorName?: string;
  traceId: string;
  metadataJson?: string;
  createdAt: string;
}

export interface OperationalAlert {
  id: string;
  tenantId: string;
  alertType: string;
  severity: 'warning' | 'critical';
  status: 'open' | 'acknowledged' | 'resolved';
  title: string;
  message: string;
  sourceModule: string;
  sourceEntityType?: string;
  sourceEntityId?: string;
  traceId?: string;
  metricValue?: number;
  thresholdValue?: number;
  acknowledgedBy?: string;
  acknowledgedAt?: string;
  resolvedBy?: string;
  resolvedAt?: string;
  createdAt: string;
  updatedAt: string;
}

import { WebhookDelivery } from "@/features/webhook/types";

export interface TraceLog {
  id: string;
  tenantId?: string;
  traceId: string;
  spanName: string;
  source: string;
  level: string;
  message: string;
  durationMs?: number;
  metadataJson?: string;
  createdAt: string;
}

export interface TraceDetail {
  traceId: string;
  traceLogs: TraceLog[];
  timelineEntries: ActivityTimelineEntry[];
  webhookDeliveries: WebhookDelivery[];
}

export interface PagedResult<T> {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
}
