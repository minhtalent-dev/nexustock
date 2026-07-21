"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import api from "@/lib/api";
import { showError, showSuccess, showWarning } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";

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

export default function ImportPage() {
  const t = useTranslations("MasterData.import");
  const [importType, setImportType] = useState("ITEMS");
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ImportResultDto | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setFile(e.target.files[0]);
      setResult(null);
    }
  };

  const handlePreview = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;

    setLoading(true);

    const formData = new FormData();
    formData.append("file", file);

    try {
      const res = await api.post<ImportResultDto>(`/master-data/imports/preview?importType=${importType}`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setResult(res.data);
      if (res.data.success) {
        showSuccess(t("toast.previewOk"));
      } else {
        showWarning(t("toast.previewHasErrors", { count: res.data.errorRows }));
      }
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.uploadFailed")));
    } finally {
      setLoading(false);
    }
  };

  const handleCommit = async () => {
    if (!result) return;
    setLoading(true);

    try {
      await api.post("/master-data/imports/commit", { batchId: result.batchId });
      showSuccess(t("toast.commitOk"));
      setResult(null);
      setFile(null);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.commitFailed")));
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadErrors = () => {
    if (!result) return;
    window.open(`http://localhost:5024/api/master-data/imports/${result.batchId}/errors`);
  };

  return (
    <div className="max-w-4xl">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-white/90">{t("page.title")}</h1>
        <p className="text-xs text-white/40 mt-1">{t("page.subtitle")}</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        <div className="bg-[#111] border border-[#222] p-4 rounded-lg text-xs text-white/60">
          <span className="font-semibold text-white/90 block mb-1">{t("help.itemsTitle")}</span>
          <code>code, name, baseUomCode, shelfLifeDays, minStock</code>
        </div>
        <div className="bg-[#111] border border-[#222] p-4 rounded-lg text-xs text-white/60">
          <span className="font-semibold text-white/90 block mb-1">{t("help.locationsTitle")}</span>
          <code>warehouseCode, zoneCode, code, xCoord, yCoord, zCoord, maxCapacity</code>
        </div>
        <div className="bg-[#111] border border-[#222] p-4 rounded-lg text-xs text-white/60">
          <span className="font-semibold text-white/90 block mb-1">{t("help.partnersTitle")}</span>
          <code>code, name, partnerType, address, taxCode</code>
        </div>
      </div>

      <div className="bg-[#111] border border-[#222] p-6 rounded-lg mb-6">
        <form onSubmit={handlePreview} className="flex flex-col gap-4">
          <div className="flex flex-col md:flex-row gap-4">
            <div className="flex-1">
              <label className="block text-xs font-semibold text-white/40 uppercase tracking-wider mb-2">{t("fields.importType")}</label>
              <select
                className="w-full bg-[#1a1a1a] border border-[#333] rounded-md px-3 py-2 text-sm text-white/95 focus:outline-none focus:border-[#555]"
                value={importType}
                onChange={(e) => setImportType(e.target.value)}
              >
                <option value="ITEMS">{t("options.items")}</option>
                <option value="LOCATIONS">{t("options.locations")}</option>
                <option value="PARTNERS">{t("options.partners")}</option>
              </select>
            </div>
            <div className="flex-1">
              <label className="block text-xs font-semibold text-white/40 uppercase tracking-wider mb-2">{t("fields.file")}</label>
              <input
                type="file"
                accept=".csv"
                onChange={handleFileChange}
                className="w-full bg-[#1a1a1a] border border-[#333] rounded-md px-3 py-1.5 text-sm text-white/80 file:mr-4 file:py-0.5 file:px-2.5 file:rounded-md file:border-0 file:text-xs file:bg-[#333] file:text-white hover:file:bg-[#444]"
              />
            </div>
          </div>
          <Button
            type="submit"
            disabled={loading || !file}
            className="self-start"
          >
            {t("actions.preview")}
          </Button>
        </form>
      </div>

      {loading && <p className="text-white/40 text-sm mb-6">{t("states.processing")}</p>}

      {result && (
        <div className="bg-[#111] border border-[#222] p-6 rounded-lg">
          <h2 className="text-lg font-bold text-white/90 mb-4">{t("result.title", { id: result.batchId.slice(0, 8) })}</h2>
          <div className="grid grid-cols-3 gap-4 mb-6">
            <div className="p-3 bg-[#1a1a1a] border border-[#222] rounded-md">
              <span className="text-xs text-white/40 block">{t("result.total")}</span>
              <span className="text-xl font-bold text-white">{result.totalRows}</span>
            </div>
            <div className="p-3 bg-[#1a1a1a] border border-[#222] rounded-md">
              <span className="text-xs text-white/40 block">{t("result.valid")}</span>
              <span className="text-xl font-bold text-green-400">{result.successRows}</span>
            </div>
            <div className="p-3 bg-[#1a1a1a] border border-[#222] rounded-md">
              <span className="text-xs text-white/40 block">{t("result.errors")}</span>
              <span className="text-xl font-bold text-red-400">{result.errorRows}</span>
            </div>
          </div>

          {result.errorRows > 0 ? (
            <div>
              <div className="flex items-center justify-between mb-3">
                <span className="text-sm font-semibold text-white/80">{t("result.errorDetail")}</span>
                <Button
                  onClick={handleDownloadErrors}
                  variant="destructive"
                  size="sm"
                >
                  {t("actions.downloadErrors")}
                </Button>
              </div>
              <div className="max-h-60 overflow-y-auto border border-[#222] rounded-md">
                <table className="w-full text-xs text-left">
                  <thead className="bg-[#1a1a1a] text-white/40 uppercase">
                    <tr>
                      <th className="p-2 w-16">{t("columns.row")}</th>
                      <th className="p-2">{t("columns.raw")}</th>
                      <th className="p-2">{t("columns.message")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#222] text-white/80">
                    {result.errors.map((err) => (
                      <tr key={err.rowIndex} className="hover:bg-[#1a1a1a]">
                        <td className="p-2 font-mono">{err.rowIndex}</td>
                        <td className="p-2 max-w-xs truncate">{JSON.stringify(err.raw)}</td>
                        <td className="p-2 text-red-400">{err.errorMessage}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            <Button
              onClick={handleCommit}
              disabled={loading}
            >
              {t("actions.commit")}
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
