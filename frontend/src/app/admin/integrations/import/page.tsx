"use client";

import { useState } from "react";
import { previewImportMappings, commitImportMappings } from "@/features/erp-integration/api";
import { ImportPreviewResult } from "@/features/erp-integration/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { showError, showSuccess, showInfo } from "@/lib/toast";
import { FileSpreadsheet, Upload, AlertTriangle, CheckCircle, RefreshCw } from "lucide-react";

export default function IntegrationImportPage() {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreviewResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [committing, setCommitting] = useState(false);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFile(e.target.files[0]);
      setPreview(null);
    }
  };

  const handleUpload = async () => {
    if (!file) {
      showError("Vui lòng chọn file CSV trước.");
      return;
    }
    setLoading(true);
    try {
      const result = await previewImportMappings("SAP-ERP", file);
      setPreview(result);
      if (result.errorRows > 0) {
        showWarning("File import chứa lỗi. Vui lòng sửa lại trước khi commit.");
      } else {
        showSuccess("Dữ liệu hợp lệ. Sẵn sàng commit.");
      }
    } catch (err: unknown) {
      const error = err as { response?: { data?: { error?: string } } };
      showError(error.response?.data?.error || "Lỗi khi phân tích file CSV.");
    } finally {
      setLoading(false);
    }
  };

  const handleCommit = async () => {
    if (!preview || preview.status === "committed") return;
    if (preview.errorRows > 0) {
      showError("Không thể commit dữ liệu có chứa dòng lỗi.");
      return;
    }

    setCommitting(true);
    try {
      const result = await commitImportMappings(preview.jobId);
      setPreview(result);
      showSuccess("Nhập dữ liệu thành công vào hệ thống.");
      setFile(null);
    } catch (err: unknown) {
      const error = err as { response?: { data?: { error?: string } } };
      showError(error.response?.data?.error || "Lỗi khi commit dữ liệu.");
    } finally {
      setCommitting(false);
    }
  };

  const showWarning = (msg: string) => {
    showInfo(msg);
  };

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <h1 className="text-2xl font-bold">Import Wizard (Ánh xạ dữ liệu ERP)</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Step 1: Upload Panel */}
        <Card className="bg-zinc-900 border-zinc-800 text-white lg:col-span-1 h-fit">
          <CardHeader>
            <CardTitle className="text-sm font-semibold">1. Chọn file nhập liệu</CardTitle>
            <CardDescription className="text-zinc-500 text-[11px]">
              Tải lên file CSV chứa danh sách mapping (mappingType, externalCode, internalCode, status).
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-xs">
            <div className="border border-dashed border-zinc-800 rounded p-6 text-center hover:bg-zinc-900/30 cursor-pointer flex flex-col items-center gap-2">
              <Upload className="w-8 h-8 text-zinc-500" />
              <span className="text-[11px] text-zinc-400">Kéo thả hoặc nhấp để chọn file CSV</span>
              <Input
                type="file"
                accept=".csv"
                onChange={handleFileChange}
                className="hidden"
                id="csv-file-input"
              />
              <Button
                type="button"
                variant="outline"
                size="xs"
                onClick={() => document.getElementById("csv-file-input")?.click()}
                className="border-zinc-700 text-zinc-300 mt-2 text-[10px]"
              >
                Chọn file
              </Button>
            </div>

            {file && (
              <div className="bg-zinc-950 p-3 rounded border border-zinc-900 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <FileSpreadsheet className="w-4 h-4 text-amber-500" />
                  <span className="font-mono text-[11px] truncate max-w-[150px]" title={file.name}>
                    {file.name}
                  </span>
                </div>
                <span className="text-[9px] text-zinc-500">{(file.size / 1024).toFixed(1)} KB</span>
              </div>
            )}

            <Button
              disabled={!file || loading}
              onClick={handleUpload}
              className="w-full bg-emerald-600 hover:bg-emerald-500 text-xs h-9 disabled:opacity-50"
            >
              {loading ? (
                <>
                  <RefreshCw className="w-3 h-3 animate-spin mr-2" />
                  Đang xử lý...
                </>
              ) : (
                "Phân tích dữ liệu"
              )}
            </Button>
          </CardContent>
        </Card>

        {/* Step 2: Preview Panel */}
        <Card className="bg-zinc-900 border-zinc-800 text-white lg:col-span-2">
          <CardHeader className="flex flex-row justify-between items-center">
            <div>
              <CardTitle className="text-sm font-semibold">2. Xem trước kết quả (Preview)</CardTitle>
              <CardDescription className="text-zinc-500 text-[11px]">
                Kiểm tra lỗi logic ánh xạ trước khi ghi chính thức vào cơ sở dữ liệu.
              </CardDescription>
            </div>
            {preview && preview.status !== "committed" && (
              <Button
                disabled={preview.errorRows > 0 || committing}
                onClick={handleCommit}
                className="bg-blue-600 hover:bg-blue-500 text-xs h-9 disabled:opacity-50"
              >
                {committing ? (
                  <>
                    <RefreshCw className="w-3 h-3 animate-spin mr-2" />
                    Đang lưu...
                  </>
                ) : (
                  "Lưu vào hệ thống (Commit)"
                )}
              </Button>
            )}
          </CardHeader>
          <CardContent className="text-xs">
            {!preview ? (
              <div className="text-center py-16 text-zinc-500 italic">
                Chưa có dữ liệu preview. Vui lòng hoàn thành bước 1.
              </div>
            ) : (
              <div className="space-y-4">
                {/* Summary bar */}
                <div className="grid grid-cols-4 gap-4 bg-zinc-950 p-4 rounded border border-zinc-800/80 font-mono text-[11px]">
                  <div>
                    <span className="text-zinc-500 block mb-1">Trạng thái</span>
                    {preview.status === "committed" ? (
                      <span className="text-emerald-400 font-bold flex items-center gap-1">
                        <CheckCircle className="w-3 h-3" /> Thành công
                      </span>
                    ) : preview.errorRows > 0 ? (
                      <span className="text-rose-400 font-bold flex items-center gap-1">
                        <AlertTriangle className="w-3 h-3" /> Lỗi dòng
                      </span>
                    ) : (
                      <span className="text-cyan-400 font-bold">Hợp lệ</span>
                    )}
                  </div>
                  <div>
                    <span className="text-zinc-500 block mb-1">Tổng số dòng</span>
                    <span className="font-bold text-white">{preview.totalRows}</span>
                  </div>
                  <div>
                    <span className="text-zinc-500 block mb-1">Dòng hợp lệ</span>
                    <span className="font-bold text-emerald-400">{preview.validRows}</span>
                  </div>
                  <div>
                    <span className="text-zinc-500 block mb-1">Dòng lỗi</span>
                    <span className="font-bold text-rose-400">{preview.errorRows}</span>
                  </div>
                </div>

                <div className="max-h-[400px] overflow-y-auto border border-zinc-850 rounded">
                  <Table className="text-xs">
                    <TableHeader className="bg-zinc-950 sticky top-0">
                      <TableRow className="border-b border-zinc-850">
                        <TableHead className="w-16 text-zinc-400 text-center">Dòng</TableHead>
                        <TableHead className="text-zinc-400">Loại Mapping</TableHead>
                        <TableHead className="text-zinc-400">Mã ERP</TableHead>
                        <TableHead className="text-zinc-400">Mã WMS</TableHead>
                        <TableHead className="text-zinc-400">Trạng thái</TableHead>
                        <TableHead className="text-zinc-400">Kết quả</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {preview.rows.map((row) => (
                        <TableRow
                          key={row.rowIndex}
                          className={`border-b border-zinc-850/50 hover:bg-zinc-800/20 ${
                            !row.isValid ? "bg-rose-950/10" : ""
                          }`}
                        >
                          <TableCell className="text-center font-mono text-zinc-500">{row.rowIndex}</TableCell>
                          <TableCell className="font-semibold">{row.rawData.mappingtype}</TableCell>
                          <TableCell className="font-mono text-amber-400">{row.rawData.externalcode}</TableCell>
                          <TableCell className="font-mono text-emerald-400">{row.rawData.internalcode}</TableCell>
                          <TableCell className="font-mono text-zinc-400">{row.rawData.status}</TableCell>
                          <TableCell>
                            {row.isValid ? (
                              <Badge className="bg-emerald-950 text-emerald-400 border border-emerald-900/30 text-[9px]">
                                OK
                              </Badge>
                            ) : (
                              <span className="text-rose-400 text-[10px] block max-w-[250px] leading-relaxed">
                                {row.errorMessage}
                              </span>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
