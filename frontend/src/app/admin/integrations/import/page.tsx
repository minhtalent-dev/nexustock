"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { previewImportMappings, commitImportMappings } from "@/features/erp-integration/api";
import { ImportPreviewResult } from "@/features/erp-integration/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess, showInfo } from "@/lib/toast";
import { FileSpreadsheet, Upload, AlertTriangle, CheckCircle, RefreshCw } from "lucide-react";

export default function IntegrationImportPage() {
  const t = useTranslations("Admin.integrations.import");
  const tErrors = useTranslations("Errors");

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
      showApiErrorToast("", t("errors.selectFile"));
      return;
    }
    setLoading(true);
    try {
      const result = await previewImportMappings("SAP-ERP", file);
      setPreview(result);
      if (result.errorRows > 0) {
        showInfo(t("toastFileErrors"));
      } else {
        showSuccess(t("toastReadyCommit"));
      }
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.parseFailed"));
    } finally {
      setLoading(false);
    }
  };

  const handleCommit = async () => {
    if (!preview || preview.status === "committed") return;
    if (preview.errorRows > 0) {
      showApiErrorToast("", t("errors.cannotCommitErrors"));
      return;
    }

    setCommitting(true);
    try {
      const result = await commitImportMappings(preview.jobId);
      setPreview(result);
      showSuccess(t("toastCommitSuccess"));
      setFile(null);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.commitFailed"));
    } finally {
      setCommitting(false);
    }
  };

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <h1 className="text-2xl font-bold">{t("title")}</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="bg-zinc-900 border-zinc-800 text-white lg:col-span-1 h-fit">
          <CardHeader>
            <CardTitle className="text-sm font-semibold">{t("step1Title")}</CardTitle>
            <CardDescription className="text-zinc-500 text-[11px]">
              {t("step1Desc")}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-xs">
            <div className="border border-dashed border-zinc-800 rounded p-6 text-center hover:bg-zinc-900/30 cursor-pointer flex flex-col items-center gap-2">
              <Upload className="w-8 h-8 text-zinc-500" />
              <span className="text-[11px] text-zinc-400">{t("dropHint")}</span>
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
                {t("chooseFile")}
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
                  {t("analyzing")}
                </>
              ) : (
                t("analyze")
              )}
            </Button>
          </CardContent>
        </Card>

        <Card className="bg-zinc-900 border-zinc-800 text-white lg:col-span-2">
          <CardHeader className="flex flex-row justify-between items-center">
            <div>
              <CardTitle className="text-sm font-semibold">{t("step2Title")}</CardTitle>
              <CardDescription className="text-zinc-500 text-[11px]">
                {t("step2Desc")}
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
                    {t("committing")}
                  </>
                ) : (
                  t("commit")
                )}
              </Button>
            )}
          </CardHeader>
          <CardContent className="text-xs">
            {!preview ? (
              <div className="text-center py-16 text-zinc-500 italic">
                {t("noPreview")}
              </div>
            ) : (
              <div className="space-y-4">
                <div className="grid grid-cols-4 gap-4 bg-zinc-950 p-4 rounded border border-zinc-800/80 font-mono text-[11px]">
                  <div>
                    <span className="text-zinc-500 block mb-1">{t("summaryStatus")}</span>
                    {preview.status === "committed" ? (
                      <span className="text-emerald-400 font-bold flex items-center gap-1">
                        <CheckCircle className="w-3 h-3" /> {t("statusCommitted")}
                      </span>
                    ) : preview.errorRows > 0 ? (
                      <span className="text-rose-400 font-bold flex items-center gap-1">
                        <AlertTriangle className="w-3 h-3" /> {t("statusRowError")}
                      </span>
                    ) : (
                      <span className="text-cyan-400 font-bold">{t("statusValid")}</span>
                    )}
                  </div>
                  <div>
                    <span className="text-zinc-500 block mb-1">{t("totalRows")}</span>
                    <span className="font-bold text-white">{preview.totalRows}</span>
                  </div>
                  <div>
                    <span className="text-zinc-500 block mb-1">{t("validRows")}</span>
                    <span className="font-bold text-emerald-400">{preview.validRows}</span>
                  </div>
                  <div>
                    <span className="text-zinc-500 block mb-1">{t("errorRows")}</span>
                    <span className="font-bold text-rose-400">{preview.errorRows}</span>
                  </div>
                </div>

                <div className="max-h-[400px] overflow-y-auto border border-zinc-850 rounded">
                  <Table className="text-xs">
                    <TableHeader className="bg-zinc-950 sticky top-0">
                      <TableRow className="border-b border-zinc-850">
                        <TableHead className="w-16 text-zinc-400 text-center">{t("colRow")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colMappingType")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colErpCode")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colWmsCode")}</TableHead>
                        <TableHead className="text-zinc-400">{t("summaryStatus")}</TableHead>
                        <TableHead className="text-zinc-400">{t("colResult")}</TableHead>
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
