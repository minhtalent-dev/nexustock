"use client";

import { useCallback, useEffect, useState, useRef } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { RefreshCw, ClipboardList, Upload } from "lucide-react";

interface SerialNumber {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  serialNo: string;
  locationId: string;
  locationCode: string;
  status: string;
  createdAt: string;
  createdBy: string;
}

interface SerialEvent {
  id: string;
  eventType: string;
  fromLocationId: string | null;
  fromLocationCode: string | null;
  toLocationId: string | null;
  toLocationCode: string | null;
  referenceId: string | null;
  createdAt: string;
  createdBy: string;
}

interface Product {
  id: string;
  code: string;
  name: string;
}

interface StorageLocation {
  id: string;
  code: string;
}

export default function SerialPage() {
  const [serials, setSerials] = useState<SerialNumber[]>([]);
  const [selectedSerial, setSelectedSerial] = useState<SerialNumber | null>(null);
  const [timelineEvents, setTimelineEvents] = useState<SerialEvent[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);
  
  const [loadingSerials, setLoadingSerials] = useState(false);
  const [loadingTimeline, setLoadingTimeline] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [showImportModal, setShowImportModal] = useState(false);
  const [importForm, setImportForm] = useState({
    itemId: "",
    locationId: ""
  });
  
  const fileInputRef = useRef<HTMLInputElement>(null);

  const fetchSerials = useCallback(async () => {
    setLoadingSerials(true);
    try {
      const res = await api.get<SerialNumber[]>("/serials", {
        params: { query: searchQuery }
      });
      setSerials(res.data || []);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải danh sách Serial."));
    } finally {
      setLoadingSerials(false);
    }
  }, [searchQuery]);

  const fetchMetadata = useCallback(async () => {
    try {
      const prodRes = await api.get<{ items: Product[] }>("/master-data/products");
      setProducts(prodRes.data.items || []);
    } catch {}

    try {
      const locRes = await api.get<{ items: StorageLocation[] }>("/master-data/storage-locations");
      setLocations(locRes.data.items || []);
    } catch {}
  }, []);

  const fetchTimeline = async (serial: SerialNumber) => {
    setLoadingTimeline(true);
    try {
      const res = await api.get<SerialEvent[]>(`/serials/${serial.serialNo}`);
      setTimelineEvents(res.data || []);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải lịch sử sự kiện Serial."));
    } finally {
      setLoadingTimeline(false);
    }
  };

  useEffect(() => {
    queueMicrotask(() => {
      void fetchSerials();
      void fetchMetadata();
    });
  }, [fetchSerials, fetchMetadata]);

  const handleSelectSerial = (serial: SerialNumber) => {
    setSelectedSerial(serial);
    fetchTimeline(serial);
  };

  const handleUploadCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (!importForm.itemId || !importForm.locationId) {
      showError("Vui lòng chọn sản phẩm và kệ cất trước khi import.");
      return;
    }

    const formData = new FormData();
    formData.append("file", file);

    try {
      await api.post(`/serials/import?itemId=${importForm.itemId}&locationId=${importForm.locationId}`, formData, {
        headers: { "Content-Type": "multipart/form-data" }
      });
      showSuccess("Import tệp CSV thành công.");
      setShowImportModal(false);
      setImportForm({ itemId: "", locationId: "" });
      fetchSerials();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Import tệp lỗi hoặc có mã trùng."));
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <ClipboardList className="h-6 w-6 text-emerald-500" />
            Truy vết Mã Serial (Serial Tracking)
          </h1>
          <p className="text-xs text-zinc-400 mt-1">
            Quản lý vòng đời và truy vết từng đơn vị sản phẩm cụ thể bằng số Serial.
          </p>
        </div>
        <div className="flex gap-3">
          <Button
            onClick={() => setShowImportModal(true)}
            className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-9 px-4 flex items-center gap-2"
          >
            <Upload className="h-4 w-4" />
            Import CSV
          </Button>
          <Button
            onClick={fetchSerials}
            variant="outline"
            className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 px-4 flex items-center gap-2"
          >
            <RefreshCw className={`h-4 w-4 ${loadingSerials ? "animate-spin" : ""}`} />
            Làm mới
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left list panel */}
        <div className="lg:col-span-2 flex flex-col gap-4">
          <Card className="bg-zinc-900 border-zinc-800 text-white">
            <CardHeader className="p-4 flex flex-row items-center gap-4">
              <input
                type="text"
                placeholder="Tìm mã serial..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 flex-grow font-mono"
              />
              <Button onClick={fetchSerials} className="bg-blue-600 hover:bg-blue-500 text-white text-xs h-9">
                Tìm kiếm
              </Button>
            </CardHeader>
            <CardContent className="p-0">
              {loadingSerials ? (
                <div className="text-center py-12 text-zinc-500 text-xs">Đang tải danh sách serial...</div>
              ) : serials.length === 0 ? (
                <div className="text-center py-12 text-zinc-500 text-xs">Không tìm thấy mã serial nào.</div>
              ) : (
                <Table className="text-xs">
                  <TableHeader className="border-b border-zinc-800">
                    <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                      <TableHead className="text-zinc-400">Mã Serial</TableHead>
                      <TableHead className="text-zinc-400">Sản phẩm</TableHead>
                      <TableHead className="text-zinc-400">Vị trí kệ</TableHead>
                      <TableHead className="text-zinc-400">Trạng thái</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {serials.map((s) => (
                      <TableRow 
                        key={s.id} 
                        onClick={() => handleSelectSerial(s)}
                        className={`border-b border-zinc-800/50 hover:bg-zinc-800/30 cursor-pointer ${
                          selectedSerial?.id === s.id ? "bg-zinc-800/80" : ""
                        }`}
                      >
                        <TableCell className="font-bold text-zinc-200 font-mono">{s.serialNo}</TableCell>
                        <TableCell className="text-zinc-300">{s.itemCode} - {s.itemName}</TableCell>
                        <TableCell className="text-zinc-300 font-mono">{s.locationCode || s.locationId.substring(0,8)}</TableCell>
                        <TableCell>
                          <Badge className="bg-blue-600/80 hover:bg-blue-600 text-white text-[9px] scale-90">
                            {s.status}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Right timeline details panel */}
        <div className="lg:col-span-1 flex flex-col gap-6">
          {selectedSerial ? (
            <Card className="bg-zinc-900 border-zinc-800 text-white">
              <CardHeader>
                <CardTitle className="text-sm font-semibold">
                  Lịch sử truy vết: {selectedSerial.serialNo}
                </CardTitle>
              </CardHeader>
              <CardContent>
                {loadingTimeline ? (
                  <div className="text-center py-8 text-zinc-500 text-xs">Đang tải timeline...</div>
                ) : timelineEvents.length === 0 ? (
                  <div className="text-center py-8 text-zinc-500 text-xs">Không tìm thấy sự kiện nào.</div>
                ) : (
                  <div className="relative border-l border-zinc-800 pl-4 space-y-4 text-xs ml-2">
                    {timelineEvents.map((evt) => (
                      <div key={evt.id} className="relative">
                        <div className="absolute -left-[21px] top-1 bg-zinc-900 border border-zinc-700 w-2.5 h-2.5 rounded-full flex items-center justify-center">
                          <div className="w-1 h-1 bg-emerald-500 rounded-full" />
                        </div>
                        <div className="flex flex-col gap-1">
                          <div className="flex items-center gap-2">
                            <Badge className="bg-zinc-800 text-zinc-300 text-[9px] px-2 py-0.5 hover:bg-zinc-800">
                              {evt.eventType}
                            </Badge>
                            <span className="text-[10px] text-zinc-500">
                              {new Date(evt.createdAt).toLocaleString()}
                            </span>
                          </div>
                          <p className="text-zinc-400 text-[11px]">Người quét: {evt.createdBy}</p>
                          {evt.toLocationCode && (
                            <p className="text-zinc-300 text-[11px]">Vị trí: {evt.toLocationCode}</p>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          ) : (
            <div className="bg-zinc-900 border border-zinc-800 rounded-lg p-16 text-center text-zinc-500 text-xs flex flex-col items-center justify-center gap-2">
              Vui lòng chọn một mã Serial để xem lịch sử truy vết chi tiết.
            </div>
          )}
        </div>
      </div>

      {/* Import Modal Dialog */}
      {showImportModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-zinc-900 border border-zinc-800 rounded-lg max-w-sm w-full text-white shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-zinc-800">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <Upload className="h-4 w-4 text-emerald-500" />
                Import danh sách Serial
              </h3>
              <button onClick={() => setShowImportModal(false)} className="text-zinc-500 hover:text-white">
                Đóng
              </button>
            </div>
            <div className="p-4 flex flex-col gap-4 text-xs">
              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Sản phẩm áp dụng</label>
                <select
                  value={importForm.itemId}
                  onChange={(e) => setImportForm({ ...importForm, itemId: e.target.value })}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                >
                  <option value="">-- Chọn sản phẩm --</option>
                  {products.filter(p => p.id).map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.code} - {p.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Vị trí kệ cất</label>
                <select
                  value={importForm.locationId}
                  onChange={(e) => setImportForm({ ...importForm, locationId: e.target.value })}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                >
                  <option value="">-- Chọn vị trí kệ --</option>
                  {locations.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.code}
                    </option>
                  ))}
                </select>
              </div>

              <input
                type="file"
                ref={fileInputRef}
                onChange={handleUploadCsv}
                accept=".csv"
                className="hidden"
              />
              <Button
                onClick={() => fileInputRef.current?.click()}
                disabled={!importForm.itemId || !importForm.locationId}
                className="bg-emerald-600 w-full py-6 text-xs"
              >
                Chọn tệp tin CSV mẫu
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
