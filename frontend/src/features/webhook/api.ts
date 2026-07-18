import api from "@/lib/api";
import {
  WebhookSubscription,
  CreateSubscriptionRequest,
  CreateSubscriptionResponse,
  UpdateSubscriptionRequest,
  DeliveryListResponse,
  ReplayBulkRequest,
} from "./types";

export async function getSubscriptions(): Promise<WebhookSubscription[]> {
  const res = await api.get<WebhookSubscription[]>("/webhooks/subscriptions");
  return res.data;
}

export async function createSubscription(
  req: CreateSubscriptionRequest
): Promise<CreateSubscriptionResponse> {
  const res = await api.post<CreateSubscriptionResponse>("/webhooks/subscriptions", req);
  return res.data;
}

export async function updateSubscription(
  id: string,
  req: UpdateSubscriptionRequest
): Promise<void> {
  await api.patch(`/webhooks/subscriptions/${id}`, req);
}

export async function deleteSubscription(id: string): Promise<void> {
  await api.delete(`/webhooks/subscriptions/${id}`);
}

export async function getDeliveries(params: {
  status?: string;
  subscriptionId?: string;
  eventType?: string;
  traceId?: string;
  page?: number;
  pageSize?: number;
}): Promise<DeliveryListResponse> {
  const res = await api.get<DeliveryListResponse>("/webhooks/deliveries", { params });
  return res.data;
}

export async function replayDelivery(id: string): Promise<void> {
  await api.post(`/webhooks/deliveries/${id}/replay`);
}

export async function replayBulk(req: ReplayBulkRequest): Promise<{ replayed: number }> {
  const res = await api.post<{ replayed: number }>("/webhooks/deliveries/replay-bulk", req);
  return res.data;
}
