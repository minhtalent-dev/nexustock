"use client";

import { useState, useRef } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import api from "@/lib/api";
import { showError, showSuccess } from "@/lib/toast";
import { Upload, FileText, X, Check, AlertTriangle } from "lucide-react";

interface QcResultDialogProps {
  isOpen: boolean;
  onClose: () => void;
  lotId: string;
  lotNo: string;
  qcRequestId: string;
  onSuccess: () => void;
}

export function QcResultDialog({ isOpen, onClose, lotId, lotNo, qcRequestId, onSuccess }: QcResultDialogProps) {
  const [isPassed, setIsPassed] = useState(true);
  const [metrics, setMetrics] = useState("");
  const [attachmentRefs, setAttachmentRefs] = useState("");
  const [uploading, setUploading] = useState(false);
  const [loading, setLoading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append("file", file);

    setUploading(true);
    try {
      const res = await api.post<{ url: string }>("/storage/upload", formData, {
        headers: { "Content-Type": "multipart/form-data" }
      });
      const uploadedUrl = res.data.url;
      setAttachmentRefs((prev) => (prev ? `${prev},${uploadedUrl}` : uploadedUrl));
      showSuccess("Tải tài liệu lên thành công.");
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi tải tệp lên.");
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  const removeAttachment = (urlToRemove: string) => {
    const refs = attachmentRefs.split(",").filter((url) => url !== urlToRemove).join(",");
    setAttachmentRefs(refs);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await api.post(`/qc/${lotId}/result`, {
        qcRequestId,
        isPassed,
        metrics: metrics.trim() || undefined,
        attachmentRefs: attachmentRefs || undefined
      });
      showSuccess("Ghi nhận kết quả QC thành công.");
      onSuccess();
      onClose();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi gửi kết quả QC.");
    } finally {
      setLoading(false);
    }
  };

  const attachmentsList = attachmentRefs ? attachmentRefs.split(",").filter(Boolean) : [];

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[500px] bg-zinc-900 border-zinc-800 text-white font-sans">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle className="text-lg font-semibold text-white">Kiểm định chất lượng lô {lotNo}</DialogTitle>
          </DialogHeader>

          <div className="grid gap-5 py-4">
            <div className="flex items-center justify-between bg-zinc-800/50 p-3 rounded-lg border border-zinc-850">
              <div>
                <Label className="text-sm font-medium text-white block">Trạng thái kiểm định</Label>
                <span className="text-xs text-zinc-400">
                  {isPassed ? "Lô hàng đạt tiêu chuẩn chất lượng" : "Lô hàng không đạt tiêu chuẩn"}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className={`text-xs font-semibold ${isPassed ? "text-emerald-500" : "text-rose-500"}`}>
                  {isPassed ? "Đạt" : "Không đạt"}
                </span>
                <Switch
                  checked={isPassed}
                  onCheckedChange={setIsPassed}
                  className="data-[state=checked]:bg-emerald-600 data-[state=unchecked]:bg-rose-600"
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="metrics" className="text-xs text-zinc-400">Thông số đo lường / Kết quả kiểm tra</Label>
              <Textarea
                id="metrics"
                placeholder="Nhập các thông số đo lường như độ ẩm, kích thước, khối lượng, lỗi phát hiện..."
                value={metrics}
                onChange={(e) => setMetrics(e.target.value)}
                rows={3}
                className="bg-zinc-800 border-zinc-700 text-white text-sm focus:ring-emerald-500"
              />
            </div>

            <div className="grid gap-2">
              <Label className="text-xs text-zinc-400">Tài liệu / Ảnh bằng chứng QC</Label>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={uploading}
                  className="border-dashed border-zinc-700 text-zinc-300 hover:bg-zinc-800 hover:text-white h-9 text-xs gap-2 flex-1"
                >
                  <Upload className="h-4 w-4" />
                  {uploading ? "Đang tải tệp lên..." : "Tải tệp đính kèm lên"}
                </Button>
                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleFileChange}
                  className="hidden"
                  accept="image/*,application/pdf"
                />
              </div>

              {attachmentsList.length > 0 && (
                <div className="grid grid-cols-2 gap-2 mt-2">
                  {attachmentsList.map((url, i) => {
                    const isImg = /\.(jpg|jpeg|png|webp|gif)$/i.test(url);
                    const filename = url.split("/").pop() || "File";
                    return (
                      <div key={i} className="flex items-center justify-between p-2 bg-zinc-800 rounded border border-zinc-750 text-xs">
                        <div className="flex items-center gap-2 overflow-hidden truncate">
                          {isImg ? (
                            <img src={url} alt="QC preview" className="w-8 h-8 rounded object-cover flex-shrink-0" />
                          ) : (
                            <FileText className="w-5 h-5 text-zinc-400 flex-shrink-0" />
                          )}
                          <span className="truncate text-zinc-300" title={filename}>{filename}</span>
                        </div>
                        <Button
                          type="button"
                          variant="ghost"
                          onClick={() => removeAttachment(url)}
                          className="h-6 w-6 p-0 hover:bg-zinc-700 text-zinc-400 hover:text-white rounded"
                        >
                          <X className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          <DialogFooter className="gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={loading}
              className="border-zinc-700 text-zinc-300 hover:bg-zinc-800 hover:text-white h-9 text-xs"
            >
              Hủy
            </Button>
            <Button
              type="submit"
              disabled={loading}
              className="bg-emerald-600 hover:bg-emerald-500 text-white h-9 text-xs"
            >
              {loading ? "Đang ghi nhận..." : "Gửi kết quả"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
