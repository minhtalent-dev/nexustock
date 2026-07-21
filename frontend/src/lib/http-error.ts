import type { AxiosError } from "axios";

type ApiErrorData = {
  message?: string;
  errorCode?: string;
};

type ApiErrorLike = {
  response?: {
    status?: number;
    data?: ApiErrorData;
  };
};

export type HttpErrorPayload = {
  status?: number;
  errorCode?: string;
  message?: string;
};

export function getHttpErrorPayload(error: unknown): HttpErrorPayload {
  if (typeof error !== "object" || error === null || !("response" in error)) {
    return {};
  }

  const err = error as ApiErrorLike;
  return {
    status: err.response?.status,
    errorCode: err.response?.data?.errorCode,
    message: err.response?.data?.message,
  };
}

export function getHttpErrorMessage(error: unknown, fallback = "Request failed") {
  const payload = getHttpErrorPayload(error);
  return payload.message || fallback;
}

/** Phân biệt feature-disabled vs unauthorized từ HTTP 403. */
export function isFeatureDisabledError(error: unknown): boolean {
  const payload = getHttpErrorPayload(error);
  return (
    payload.status === 403 &&
    payload.errorCode === "TASK_INTERLEAVING_DISABLED"
  );
}

export function isUnauthorizedError(error: unknown): boolean {
  const payload = getHttpErrorPayload(error);
  if (payload.status === 401) return true;
  if (payload.status === 403 && payload.errorCode !== "TASK_INTERLEAVING_DISABLED") {
    return true;
  }
  const axiosErr = error as AxiosError | undefined;
  return Boolean(axiosErr?.response?.status === 403 && !isFeatureDisabledError(error));
}
