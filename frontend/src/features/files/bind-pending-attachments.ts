import type { UploadResult } from "./api";

export interface BindBatchResult {
  bound: UploadResult[];
  failed: Array<{
    item: UploadResult;
    error: unknown;
  }>;
}

/**
 * Liên kết hàng loạt các tệp đính kèm đang chờ xử lý.
 * inject hàm bind để không bị phụ thuộc trực tiếp vào API client hoặc router, giúp dễ dàng kiểm thử.
 */
export async function bindPendingAttachments(
  items: UploadResult[],
  bindFn: (item: UploadResult) => Promise<unknown>
): Promise<BindBatchResult> {
  if (items.length === 0) {
    return { bound: [], failed: [] };
  }

  const results = await Promise.allSettled(items.map(item => bindFn(item)));

  const bound: UploadResult[] = [];
  const failed: Array<{ item: UploadResult; error: unknown }> = [];

  results.forEach((res, index) => {
    const item = items[index];
    if (res.status === "fulfilled") {
      bound.push(item);
    } else {
      failed.push({ item, error: res.reason });
    }
  });

  return { bound, failed };
}
