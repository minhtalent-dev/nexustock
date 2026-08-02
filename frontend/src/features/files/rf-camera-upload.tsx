"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Spinner } from "@/components/ui/spinner";
import { Camera, Upload, AlertTriangle, ImageIcon, Trash2 } from "lucide-react";
import {
  validateRfUpload,
  type AttachmentUploadSource,
  type FileValidationResult,
} from "./rf-upload-validation";

export type RfCameraUploadProps = {
  disabled?: boolean;
  uploading: boolean;
  enableRfCapture?: boolean;
  onUpload: (file: File, source: AttachmentUploadSource) => Promise<boolean>;
};

export function RfCameraUpload({
  disabled = false,
  uploading,
  enableRfCapture = false,
  onUpload,
}: RfCameraUploadProps) {
  const t = useTranslations("Common.files");

  const cameraInputRef = useRef<HTMLInputElement | null>(null);
  const fallbackInputRef = useRef<HTMLInputElement | null>(null);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [source, setSource] = useState<AttachmentUploadSource>("FILE_PICKER");
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<FileValidationResult>(null);
  const [isOnline, setIsOnline] = useState<boolean>(() =>
    typeof window !== "undefined" ? navigator.onLine : true
  );

  // Monitor online status
  useEffect(() => {
    if (typeof window === "undefined") return;

    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  // Cleanup object URL
  const cleanupPreview = useCallback(() => {
    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
      setPreviewUrl(null);
    }
  }, [previewUrl]);

  useEffect(() => {
    return () => {
      cleanupPreview();
    };
  }, [cleanupPreview]);

  const handleFileSelect = (file: File | null, chosenSource: AttachmentUploadSource) => {
    if (!file) return;

    cleanupPreview();
    setValidationError(null);

    const error = validateRfUpload(file, chosenSource);
    if (error) {
      setValidationError(error);
      setSelectedFile(null);
      return;
    }

    setSelectedFile(file);
    setSource(chosenSource);

    // Local preview for image files
    if (file.type.startsWith("image/")) {
      const url = URL.createObjectURL(file);
      setPreviewUrl(url);
    }
  };

  const handleRemove = () => {
    cleanupPreview();
    setSelectedFile(null);
    setValidationError(null);
    if (cameraInputRef.current) cameraInputRef.current.value = "";
    if (fallbackInputRef.current) fallbackInputRef.current.value = "";
  };

  const handleUploadSubmit = async () => {
    if (!selectedFile || uploading || !isOnline) return;
    const success = await onUpload(selectedFile, source);
    if (success) {
      handleRemove();
    }
  };

  const renderErrorMessage = (err: FileValidationResult) => {
    switch (err) {
      case "FILE_TOO_LARGE":
        return t("fileTooLarge");
      case "INVALID_CAMERA_IMAGE":
        return t("invalidCameraImage");
      case "UNSUPPORTED_TYPE":
        return t("unsupportedType");
      default:
        return null;
    }
  };

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-card/20 p-3 text-sm font-sans">
      {!isOnline && (
        <Alert variant="destructive" className="py-2 text-xs">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{t("offlineWarning")}</AlertDescription>
        </Alert>
      )}

      {/* Action buttons */}
      <div className="flex flex-wrap items-center gap-2">
        {enableRfCapture && (
          <label className="inline-flex cursor-pointer">
            <input
              ref={cameraInputRef}
              type="file"
              accept="image/*"
              capture="environment"
              className="hidden"
              disabled={disabled || uploading}
              onChange={(e) => {
                const file = e.target.files?.[0] ?? null;
                handleFileSelect(file, "RF_CAMERA");
              }}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={disabled || uploading}
              className="h-9 gap-1.5 text-xs font-medium"
              onClick={() => cameraInputRef.current?.click()}
            >
              <Camera className="h-4 w-4 text-primary" />
              <span>{t("takePhotoBtn")}</span>
            </Button>
          </label>
        )}

        <label className="inline-flex cursor-pointer">
          <input
            ref={fallbackInputRef}
            type="file"
            accept=".jpg,.jpeg,.png,.webp,.pdf"
            className="hidden"
            disabled={disabled || uploading}
            onChange={(e) => {
              const file = e.target.files?.[0] ?? null;
              handleFileSelect(file, "FILE_PICKER");
            }}
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={disabled || uploading}
            className="h-9 gap-1.5 text-xs font-medium"
            onClick={() => fallbackInputRef.current?.click()}
          >
            <Upload className="h-4 w-4 text-muted-foreground" />
            <span>{t("chooseFileBtn")}</span>
          </Button>
        </label>
      </div>

      {/* Validation Error */}
      {validationError && (
        <Alert variant="destructive" className="py-2 text-xs">
          <AlertDescription>{renderErrorMessage(validationError)}</AlertDescription>
        </Alert>
      )}

      {/* Selection card & local preview */}
      {selectedFile && (
        <div className="flex flex-col gap-2 rounded-md border border-border bg-card p-3" aria-live="polite">
          <div className="flex items-center justify-between gap-2">
            <div className="flex items-center gap-2 min-w-0 flex-1">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded border border-border bg-muted/30">
                {previewUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={previewUrl}
                    alt={t("localPreviewAlt")}
                    className="h-full w-full object-cover rounded"
                  />
                ) : (
                  <ImageIcon className="h-5 w-5 text-muted-foreground" />
                )}
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-medium text-foreground">{selectedFile.name}</p>
                <div className="flex items-center gap-2 text-[10px] text-muted-foreground mt-0.5">
                  <span>{(selectedFile.size / (1024 * 1024)).toFixed(2)} MB</span>
                  <Badge variant="secondary" className="text-[10px] px-1.5 py-0 h-4">
                    {source === "RF_CAMERA" ? t("sourceCamera") : t("sourceFile")}
                  </Badge>
                </div>
              </div>
            </div>

            <div className="flex items-center gap-1 shrink-0">
              <Button
                type="button"
                variant="default"
                size="xs"
                disabled={uploading || !isOnline}
                onClick={handleUploadSubmit}
                className="h-8 gap-1 text-xs px-2.5"
              >
                {uploading ? (
                  <>
                    <Spinner className="h-3.5 w-3.5" />
                    <span>{t("uploading")}</span>
                  </>
                ) : (
                  <>
                    <Upload className="h-3.5 w-3.5" />
                    <span>{t("uploadBtn")}</span>
                  </>
                )}
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="xs"
                disabled={uploading}
                onClick={handleRemove}
                className="h-8 text-xs px-2 text-destructive hover:bg-destructive/10 hover:text-destructive"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
