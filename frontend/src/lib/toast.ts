import { toast } from "sonner";

type ToastConfig = {
  duration?: number;
  description?: string;
  action?: { label: string; onClick: () => void };
};

function mergeOptions(config?: ToastConfig) {
  return {
    duration: config?.duration ?? 4000,
    description: config?.description,
    action: config?.action,
  };
}

export function showSuccess(msg: string, config?: ToastConfig) {
  toast.success(msg, mergeOptions(config));
}

export function showError(msg: string, config?: ToastConfig) {
  toast.error(msg, mergeOptions(config));
}

/** Hiển thị lỗi API đã resolve (codeLabel + message). */
export function showApiErrorToast(codeLabel: string, message: string, config?: ToastConfig) {
  toast.error(message, mergeOptions({ ...config, description: codeLabel }));
}

export function showWarning(msg: string, config?: ToastConfig) {
  toast.warning(msg, mergeOptions(config));
}

export function showInfo(msg: string, config?: ToastConfig) {
  toast.info(msg, mergeOptions(config));
}

export function showLoading(msg: string) {
  return toast.loading(msg);
}

export function showPromise<T>(
  promise: Promise<T>,
  messages: { loading: string; success: string | ((data: T) => string); error: string | ((err: unknown) => string) }
) {
  return toast.promise(promise, messages);
}
