"use client";

import { useState, useId } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "@/components/ui/dialog";
import api from "@/lib/api";
import { showError, showSuccess, showWarning } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { Upload, Download, FileSpreadsheet, AlertTriangle, CheckCircle2 } from "lucide-react";

interface ImportRowErrorDto {
  rowIndex: number;
  raw: Record<string, string>;
  errorMessage: string;
}

interface ImportResultDto {
  success: boolean;
  batchId: string;
  importType: string;
  status: string;
  totalRows: number;
  successRows: number;
  errorRows: number;
  errors: ImportRowErrorDto[];
}

interface OperationalImportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  previewUrl: string;
  commitUrl: string;
  errorUrl: string;
  templateType?: string;
  onSuccess: () => void;
}

export function OperationalImportDialog({
  open,
  onOpenChange,
  title,
  description,
  previewUrl,
  commitUrl,
  errorUrl,
  templateType,
  onSuccess,
}: OperationalImportDialogProps) {
  const t = useTranslations("Admin.common");
  const dialogTitleId = useId();
  const dialogDescId = useId();

  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ImportResultDto | null>(null);

  const handleReset = () => {
    setFile(null);
    setResult(null);
    setLoading(false);
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      handleReset();
    }
    onOpenChange(newOpen);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setFile(e.target.files[0]);
      setResult(null);
    }
  };

  const handlePreview = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file || loading) return;

    setLoading(true);
    const formData = new FormData();
    formData.append("file", file);

    try {
      const res = await api.post<ImportResultDto>(previewUrl, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setResult(res.data);
      if (res.data.success) {
        showSuccess(res.data.errorRows === 0 ? "Xem trước thành công" : "Xem trước hoàn tất");
      } else {
        showWarning(`Phát hiện ${res.data.errorRows} dòng dữ liệu lỗi.`);
      }
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Tải file xem trước thất bại"));
    } finally {
      setLoading(false);
    }
  };

  const handleCommit = async () => {
    if (!result || loading) return;

    setLoading(true);
    try {
      await api.post(commitUrl, { batchId: result.batchId });
      showSuccess("Nhập dữ liệu thành công");
      handleOpenChange(false);
      onSuccess();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Duyệt nhập dữ liệu thất bại"));
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadErrors = () => {
    if (!result) return;
    const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5024/api";
    window.open(`${apiUrl}${errorUrl}/${result.batchId}`);
  };

  const handleDownloadTemplate = (format: "csv" | "xlsx") => {
    if (!templateType) return;
    const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5024/api";
    window.open(`${apiUrl}/imports/template?type=${templateType}&format=${format}`);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent aria-labelledby={dialogTitleId} aria-describedby={dialogDescId} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle id={dialogTitleId} className="flex items-center gap-2 text-lg font-bold">
            <FileSpreadsheet className="h-5 w-5 text-emerald-500" />
            {title}
          </DialogTitle>

          <DialogDescription id={dialogDescId} className="text-xs text-muted-foreground">
            {description || "Chọn tệp CSV hoặc Excel (.xlsx) để nhập dữ liệu dòng tự động."}
          </DialogDescription>

        </DialogHeader>

        <div className="space-y-4 py-2">
          {templateType && (
            <div className="flex items-center justify-between p-3 bg-muted/40 border border-border rounded-md text-xs">
              <span className="text-muted-foreground">Tải tệp mẫu chuẩn v1:</span>
              <div className="flex gap-2">
                <Button size="sm" variant="outline" onClick={() => handleDownloadTemplate("csv")} className="h-7 text-xs gap-1">
                  <Download className="h-3.5 w-3.5" /> CSV
                </Button>
                <Button size="sm" variant="outline" onClick={() => handleDownloadTemplate("xlsx")} className="h-7 text-xs gap-1">
                  <Download className="h-3.5 w-3.5" /> XLSX
                </Button>
              </div>
            </div>
          )}

          <form onSubmit={handlePreview} className="flex flex-col gap-3">
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                Chọn tệp dữ liệu (.csv, .xlsx)
              </label>
              <input
                type="file"
                accept=".csv,.xlsx"
                onChange={handleFileChange}
                disabled={loading}
                className="w-full bg-background border border-input rounded-md px-3 py-1.5 text-sm file:mr-4 file:py-1 file:px-3 file:rounded-md file:border-0 file:text-xs file:font-semibold file:bg-emerald-600 file:text-white hover:file:bg-emerald-500"
              />
            </div>
            <Button type="submit" disabled={loading || !file} className="self-start gap-1.5">
              <Upload className="h-4 w-4" />
              {loading ? "Đang xử lý..." : "Tải lên & Xem trước"}
            </Button>
          </form>

          {result && (
            <div className="space-y-4 border-t border-border pt-4">
              <div className="grid grid-cols-3 gap-3">
                <div className="p-3 bg-muted/50 border border-border rounded-md">
                  <span className="text-[11px] text-muted-foreground block">Tổng số dòng</span>
                  <span className="text-lg font-bold font-mono">{result.totalRows}</span>
                </div>
                <div className="p-3 bg-emerald-500/10 border border-emerald-500/20 rounded-md">
                  <span className="text-[11px] text-emerald-500 block flex items-center gap-1">
                    <CheckCircle2 className="h-3 w-3" /> Hợp lệ
                  </span>
                  <span className="text-lg font-bold font-mono text-emerald-500">{result.successRows}</span>
                </div>
                <div className="p-3 bg-red-500/10 border border-red-500/20 rounded-md">
                  <span className="text-[11px] text-red-500 block flex items-center gap-1">
                    <AlertTriangle className="h-3 w-3" /> Dòng lỗi
                  </span>
                  <span className="text-lg font-bold font-mono text-red-500">{result.errorRows}</span>
                </div>
              </div>

              {result.errorRows > 0 ? (
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-red-500 flex items-center gap-1">
                      <AlertTriangle className="h-3.5 w-3.5" /> Chi tiết dòng bị lỗi:
                    </span>
                    <Button onClick={handleDownloadErrors} variant="destructive" size="sm" className="h-7 text-xs gap-1">
                      <Download className="h-3.5 w-3.5" /> Tải file lỗi (CSV)
                    </Button>
                  </div>
                  <div className="max-h-48 overflow-y-auto border border-border rounded-md">
                    <table className="w-full text-xs text-left">
                      <thead className="bg-muted text-muted-foreground uppercase text-[10px] sticky top-0">
                        <tr>
                          <th className="p-2 w-16">Dòng</th>
                          <th className="p-2">Chi tiết lỗi</th>
                        </tr>
                      </thead>
                      <tbody>
                        {result.errors.map((err) => (
                          <tr key={err.rowIndex} className="border-t border-border hover:bg-muted/20">
                            <td className="p-2 font-mono font-semibold">{err.rowIndex}</td>
                            <td className="p-2 text-red-400">{err.errorMessage}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : (
                <div className="p-3 bg-emerald-500/10 border border-emerald-500/20 rounded-md text-xs text-emerald-500">
                  Tất cả các dòng dữ liệu đều hợp lệ. Bấm &quot;Xác nhận duyệt&quot; để cập nhật hệ thống.
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter className="flex justify-end gap-2 border-t border-border pt-3">
          <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={loading}>
            {t("cancel")}
          </Button>
          {result && result.errorRows === 0 && (
            <Button onClick={handleCommit} disabled={loading} className="bg-emerald-600 hover:bg-emerald-500 text-white">
              {loading ? "Đang lưu..." : "Xác nhận duyệt"}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
