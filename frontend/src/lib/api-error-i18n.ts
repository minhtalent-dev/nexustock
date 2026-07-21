import { getHttpErrorPayload } from '@/lib/http-error';

export type ResolvedApiError = {
  code: string;
  codeLabel: string;
  message: string;
};

type TranslateFn = {
  (key: string): string;
};

function tryTranslate(t: TranslateFn, key: string): string | null {
  try {
    const value = t(key);
    if (!value || value === key) return null;
    return value;
  } catch {
    return null;
  }
}

/** Hỗ trợ cả useTranslations() (full path) và useTranslations('Errors') (relative). */
export function resolveApiError(error: unknown, t: TranslateFn): ResolvedApiError {
  const payload = getHttpErrorPayload(error);
  const code = payload.errorCode?.trim() || 'UNKNOWN';

  const codeLabel =
    tryTranslate(t, `codes.${code}`) ||
    tryTranslate(t, `Errors.codes.${code}`) ||
    payload.errorCode ||
    code;

  const message =
    tryTranslate(t, `messages.${code}`) ||
    tryTranslate(t, `Errors.messages.${code}`) ||
    payload.message ||
    tryTranslate(t, 'messages.generic') ||
    tryTranslate(t, 'Errors.messages.generic') ||
    'Request failed.';

  return { code, codeLabel, message };
}
