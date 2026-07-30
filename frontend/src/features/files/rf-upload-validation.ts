export type AttachmentUploadSource = "RF_CAMERA" | "FILE_PICKER";

export const MAX_ATTACHMENT_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB

export const ALLOWED_IMAGE_MIMES = new Set([
  "image/jpeg",
  "image/jpg",
  "image/png",
  "image/webp",
]);

export const ALLOWED_IMAGE_EXTENSIONS = new Set([
  "jpg",
  "jpeg",
  "png",
  "webp",
]);

export const ALLOWED_FALLBACK_EXTENSIONS = new Set([
  "jpg",
  "jpeg",
  "png",
  "webp",
  "pdf",
]);

export const ALLOWED_FALLBACK_MIMES = new Set([
  "image/jpeg",
  "image/jpg",
  "image/png",
  "image/webp",
  "application/pdf",
]);

export type FileValidationResult =
  | "FILE_TOO_LARGE"
  | "INVALID_CAMERA_IMAGE"
  | "UNSUPPORTED_TYPE"
  | "INVALID_SOURCE"
  | null;

export function validateRfUpload(
  file: { name: string; size: number; type: string },
  source: AttachmentUploadSource
): FileValidationResult {
  if (source !== "RF_CAMERA" && source !== "FILE_PICKER") {
    return "INVALID_SOURCE";
  }

  if (file.size > MAX_ATTACHMENT_SIZE_BYTES) {
    return "FILE_TOO_LARGE";
  }

  const mime = (file.type || "").toLowerCase().trim();
  const parts = file.name.split(".");
  const ext = parts.length > 1 ? parts.pop()!.toLowerCase().trim() : "";

  if (source === "RF_CAMERA") {
    if (!ALLOWED_IMAGE_MIMES.has(mime) && !ALLOWED_IMAGE_EXTENSIONS.has(ext)) {
      return "INVALID_CAMERA_IMAGE";
    }
    return null;
  }

  // FILE_PICKER
  if (!ext || !ALLOWED_FALLBACK_EXTENSIONS.has(ext)) {
    return "UNSUPPORTED_TYPE";
  }

  if (mime && !ALLOWED_FALLBACK_MIMES.has(mime)) {
    return "UNSUPPORTED_TYPE";
  }

  return null;
}
