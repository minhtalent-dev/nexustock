export interface WebhookSubscription {
  id: string;
  targetUrl: string;
  eventTypes: string[];
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSubscriptionResponse {
  subscriptionId: string;
  secretKey: string; // Chỉ trả về 1 lần
}

export interface CreateSubscriptionRequest {
  targetUrl: string;
  eventTypes: string[];
}

export interface UpdateSubscriptionRequest {
  targetUrl?: string;
  eventTypes?: string[];
  isActive?: boolean;
}

export interface WebhookDelivery {
  id: string;
  subscriptionId: string;
  eventType: string;
  status: "pending" | "sending" | "delivered" | "deadLetter";
  retryCount: number;
  nextAttemptAt: string | null;
  traceId: string;
  lastResponseCode: number | null;
  lastError: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DeliveryListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: WebhookDelivery[];
}

export interface ReplayBulkRequest {
  ids?: string[];
  filterStatus?: string;
}
