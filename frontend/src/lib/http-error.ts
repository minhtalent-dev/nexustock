type ApiErrorLike = {
  response?: {
    data?: {
      message?: string;
    };
  };
};

export function getHttpErrorMessage(error: unknown, fallback: string) {
  if (typeof error !== "object" || error === null || !("response" in error)) {
    return fallback;
  }

  return (error as ApiErrorLike).response?.data?.message || fallback;
}
