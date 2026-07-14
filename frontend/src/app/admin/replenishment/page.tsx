"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
import { RefreshCw, Play, Trash2, ArrowRight, Layers, ClipboardList, CheckCircle, HelpCircle, Settings, Plus, X } from "lucide-react";

interface ReplenishmentRule {
  id: string;
  itemId: string;
  locationId: string;
  minQty: number;
  maxQty: number;
  createdAt: string;
  createdBy: string;
}

interface ReplenishmentTask {
  id: string;
  itemId: string;
  sourceLocationId: string;
  targetLocationId: string;
  lotNo: string;
  requestedQty: number;
  actualQty: number | null;
  status: string;
  mobileTaskId: string | null;
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

export default function ReplenishmentPage() {
  const [activeTab, setActiveTab] = useState<"rules" | "tasks">("tasks");
  const [rules, setRules] = useState<ReplenishmentRule[]>([]);
  const [tasks, setTasks] = useState<ReplenishmentTask[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);

  const [loadingRules, setLoadingRules] = useState(false);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [submittingRule, setSubmittingRule] = useState(false);
  const [runningEngine, setRunningEngine] = useState(false);

  // Form State
  const [newRule, setNewRule] = useState({
    itemId: "",
    locationId: "",
    minQty: 10,
    maxQty: 50
  });

  // Complete Dialog/Form State
  const [completingTask, setCompletingTask] = useState<ReplenishmentTask | null>(null);
  const [actualQty, setActualQty] = useState<number>(0);
  const [operatorName, setOperatorName] = useState("");
  const [submittingComplete, setSubmittingComplete] = useState(false);

  const fetchRules = async () => {
    setLoadingRules(true);
    try {
      const res = await api.get<ReplenishmentRule[]>("/replenishment/rules");
      setRules(res.data || []);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải danh sách quy tắc bổ sung.");
    } finally {
      setLoadingRules(false);
    }
  };

  const fetchTasks = async () => {
    setLoadingTasks(true);
    try {
      const res = await api.get<ReplenishmentTask[]>("/replenishment/tasks");
      setTasks(res.data || []);
    } catch (err: any) {
      showError(err.response?.data?.message || "Không thể tải danh sách nhiệm vụ bổ sung.");
    } finally {
      setLoadingTasks(false);
    }
  };

  const fetchMetadata = async () => {
    try {
      const prodRes = await api.get<Product[]>("/masterdata/products");
      setProducts(prodRes.data || []);
    } catch {
      // Bỏ qua lỗi nếu API chưa sẵn sàng
    }

    try {
      const locRes = await api.get<StorageLocation[]>("/masterdata/locations");
      setLocations(locRes.data || []);
    } catch {
      // Bỏ qua lỗi
    }
  };

  useEffect(() => {
    fetchRules();
    fetchTasks();
    fetchMetadata();
  }, []);

  const handleCreateRule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newRule.itemId || !newRule.locationId) {
      showError("Vui lòng chọn đầy đủ sản phẩm và vị trí kệ.");
      return;
    }

    setSubmittingRule(true);
    try {
      const res = await api.post("/replenishment/rules", newRule);
      showSuccess("Tạo quy tắc bổ sung thành công.");
      setNewRule({ itemId: "", locationId: "", minQty: 10, maxQty: 50 });
      fetchRules();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi tạo quy tắc bổ sung.");
    } finally {
      setSubmittingRule(false);
    }
  };

  const handleRunEngine = async () => {
    setRunningEngine(true);
    try {
      const res = await api.post("/replenishment/generate?strategy=FEFO");
      const generatedCount = res.data?.length || 0;
      showSuccess(`Đã quét bổ sung hoàn tất. Đã sinh ${generatedCount} nhiệm vụ mới.`);
      fetchTasks();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi chạy tiến trình bổ sung hàng.");
    } finally {
      setRunningEngine(false);
    }
  };

  const handleCancelTask = async (taskId: string) => {
    if (!confirm("Bạn có chắc chắn muốn hủy bỏ nhiệm vụ bổ sung hàng này? Lượng tồn kho dự trữ sẽ được giải phóng.")) {
      return;
    }

    try {
      await api.post(`/replenishment/tasks/${taskId}/cancel`);
      showSuccess("Đã hủy bỏ nhiệm vụ thành công.");
      fetchTasks();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi hủy nhiệm vụ bổ sung.");
    }
  };

  const handleOpenComplete = (task: ReplenishmentTask) => {
    setCompletingTask(task);
    setActualQty(task.requestedQty);
    setOperatorName("");
  };

  const handleCompleteTask = async () => {
    if (!completingTask) return;
    if (actualQty < 0) {
      showError("Số lượng thực tế phải lớn hơn hoặc bằng 0.");
      return;
    }

    setSubmittingComplete(true);
    try {
      const payload = {
        actualQty,
        operatorName: operatorName || "System"
      };
      await api.post(`/replenishment/tasks/${completingTask.id}/complete`, payload);
      showSuccess("Xác nhận hoàn tất nhiệm vụ bổ sung thành công.");
      setCompletingTask(null);
      fetchTasks();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi xác nhận hoàn tất nhiệm vụ.");
    } finally {
      setSubmittingComplete(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "COMPLETED":
        return <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white">Completed</Badge>;
      case "CANCELLED":
        return <Badge className="bg-rose-600 hover:bg-rose-500 text-white">Cancelled</Badge>;
      case "ASSIGNED":
        return <Badge className="bg-amber-600 hover:bg-amber-500 text-white">Assigned</Badge>;
      case "PENDING":
      default:
        return <Badge className="bg-zinc-800 hover:bg-zinc-700 text-zinc-300">Pending</Badge>;
    }
  };

  const getProductCode = (id: string) => {
    const prod = products.find((p) => p.id === id);
    return prod ? `${prod.code} - ${prod.name}` : id.substring(0, 8);
  };

  const getLocationCode = (id: string) => {
    const loc = locations.find((l) => l.id === id);
    return loc ? loc.code : id.substring(0, 8);
  };

  return (
    <div className="flex flex-col gap-6 font-sans text-white">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Layers className="h-6 w-6 text-emerald-500" />
            Bổ sung hàng Pick Face
          </h1>
          <p className="text-xs text-zinc-400 mt-1">
            Giám sát mức tồn kho tại các vị trí lấy hàng (Pick Face) và bổ sung tự động từ kho lưu trữ Bulk.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Button
            onClick={handleRunEngine}
            disabled={runningEngine}
            className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-9 px-4 flex items-center gap-2"
          >
            <Play className={`h-4 w-4 ${runningEngine ? "animate-spin" : ""}`} />
            {runningEngine ? "Đang quét..." : "Chạy bổ sung tự động"}
          </Button>
          <Button
            onClick={() => {
              fetchRules();
              fetchTasks();
            }}
            variant="outline"
            className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 h-9 w-9 p-0"
          >
            <RefreshCw className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Tabs Menu */}
      <div className="flex border-b border-zinc-800">
        <button
          onClick={() => setActiveTab("tasks")}
          className={`py-2.5 px-4 text-xs font-semibold border-b-2 transition-all ${
            activeTab === "tasks" ? "border-emerald-500 text-emerald-500" : "border-transparent text-zinc-400 hover:text-white"
          }`}
        >
          Nhiệm vụ bổ sung
        </button>
        <button
          onClick={() => setActiveTab("rules")}
          className={`py-2.5 px-4 text-xs font-semibold border-b-2 transition-all ${
            activeTab === "rules" ? "border-emerald-500 text-emerald-500" : "border-transparent text-zinc-400 hover:text-white"
          }`}
        >
          Cấu hình quy tắc (Min/Max)
        </button>
      </div>

      {activeTab === "tasks" ? (
        <Card className="bg-zinc-900 border-zinc-800 text-white">
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-2">
              <ClipboardList className="h-4 w-4 text-emerald-500" />
              Nhiệm vụ đang xử lý
            </CardTitle>
            <CardDescription className="text-xs text-zinc-500">
              Danh sách các dịch chuyển hàng từ Bulk về Pick Face đang chờ nhân viên xử lý hoặc hoàn thành.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {loadingTasks && tasks.length === 0 ? (
              <div className="text-center py-12 text-zinc-500 text-xs">Đang tải danh sách nhiệm vụ...</div>
            ) : tasks.length === 0 ? (
              <div className="text-center py-12 text-zinc-500 text-xs">Không có nhiệm vụ bổ sung nào.</div>
            ) : (
              <div className="overflow-x-auto">
                <Table className="text-xs">
                  <TableHeader className="border-b border-zinc-800">
                    <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                      <TableHead className="text-zinc-400">Sản phẩm</TableHead>
                      <TableHead className="text-zinc-400">Kệ nguồn (Bulk)</TableHead>
                      <TableHead className="text-zinc-400">Kệ đích (Pick Face)</TableHead>
                      <TableHead className="text-zinc-400">Số lô (Lot No)</TableHead>
                      <TableHead className="text-zinc-400 text-right">SL yêu cầu</TableHead>
                      <TableHead className="text-zinc-400 text-right">SL thực tế</TableHead>
                      <TableHead className="text-zinc-400 text-center">Trạng thái</TableHead>
                      <TableHead className="text-zinc-400 text-center">Hành động</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tasks.map((task) => (
                      <TableRow key={task.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                        <TableCell className="font-medium text-zinc-300">{getProductCode(task.itemId)}</TableCell>
                        <TableCell className="text-zinc-300 font-mono">{getLocationCode(task.sourceLocationId)}</TableCell>
                        <TableCell className="text-emerald-400 font-mono">{getLocationCode(task.targetLocationId)}</TableCell>
                        <TableCell className="text-zinc-300">{task.lotNo}</TableCell>
                        <TableCell className="text-right font-semibold">{task.requestedQty}</TableCell>
                        <TableCell className="text-right text-zinc-400">{task.actualQty ?? "-"}</TableCell>
                        <TableCell className="text-center">{getStatusBadge(task.status)}</TableCell>
                        <TableCell className="text-center flex justify-center gap-2">
                          {(task.status === "PENDING" || task.status === "ASSIGNED") && (
                            <>
                              <Button
                                onClick={() => handleOpenComplete(task)}
                                className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-3 text-[10px] rounded"
                              >
                                Hoàn tất
                              </Button>
                              <Button
                                onClick={() => handleCancelTask(task.id)}
                                variant="outline"
                                className="border-zinc-800 hover:bg-zinc-800 text-rose-500 h-7 px-3 text-[10px] rounded"
                              >
                                Hủy bỏ
                              </Button>
                            </>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Rules List */}
          <div className="lg:col-span-2">
            <Card className="bg-zinc-900 border-zinc-800 text-white">
              <CardHeader>
                <CardTitle className="text-sm font-semibold flex items-center gap-2">
                  <Settings className="h-4 w-4 text-emerald-500" />
                  Quy tắc bổ sung hiện tại
                </CardTitle>
              </CardHeader>
              <CardContent>
                {loadingRules && rules.length === 0 ? (
                  <div className="text-center py-12 text-zinc-500 text-xs">Đang tải danh sách quy tắc...</div>
                ) : rules.length === 0 ? (
                  <div className="text-center py-12 text-zinc-500 text-xs">Chưa có quy tắc nào được định nghĩa.</div>
                ) : (
                  <div className="overflow-x-auto">
                    <Table className="text-xs">
                      <TableHeader className="border-b border-zinc-800">
                        <TableRow className="border-b border-zinc-800 hover:bg-zinc-800/50">
                          <TableHead className="text-zinc-400">Sản phẩm</TableHead>
                          <TableHead className="text-zinc-400">Vị trí lấy hàng (Pick Face)</TableHead>
                          <TableHead className="text-zinc-400 text-right">SL tối thiểu (Min)</TableHead>
                          <TableHead className="text-zinc-400 text-right">SL tối đa (Max)</TableHead>
                          <TableHead className="text-zinc-400">Người tạo</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {rules.map((rule) => (
                          <TableRow key={rule.id} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                            <TableCell className="font-semibold text-zinc-200">{getProductCode(rule.itemId)}</TableCell>
                            <TableCell className="font-mono text-zinc-300">{getLocationCode(rule.locationId)}</TableCell>
                            <TableCell className="text-right text-amber-500 font-semibold">{rule.minQty}</TableCell>
                            <TableCell className="text-right text-emerald-500 font-semibold">{rule.maxQty}</TableCell>
                            <TableCell className="text-zinc-400">{rule.createdBy}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Add Rule Form */}
          <div className="lg:col-span-1">
            <Card className="bg-zinc-900 border-zinc-800 text-white">
              <CardHeader>
                <CardTitle className="text-sm font-semibold flex items-center gap-2">
                  <Plus className="h-4 w-4 text-emerald-500" />
                  Thêm quy tắc mới
                </CardTitle>
              </CardHeader>
              <CardContent>
                <form onSubmit={handleCreateRule} className="flex flex-col gap-4 text-xs">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-[10px] text-zinc-500">Mã sản phẩm (Item ID / Code)</label>
                    {products.length > 0 ? (
                      <select
                        value={newRule.itemId}
                        onChange={(e) => setNewRule({ ...newRule, itemId: e.target.value })}
                        className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                      >
                        <option value="">-- Chọn sản phẩm --</option>
                        {products.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.code} - {p.name}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        type="text"
                        placeholder="Nhập GUID ItemId..."
                        value={newRule.itemId}
                        onChange={(e) => setNewRule({ ...newRule, itemId: e.target.value })}
                        className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-mono"
                      />
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-[10px] text-zinc-500">Vị trí Pick Face (Location ID / Code)</label>
                    {locations.length > 0 ? (
                      <select
                        value={newRule.locationId}
                        onChange={(e) => setNewRule({ ...newRule, locationId: e.target.value })}
                        className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                      >
                        <option value="">-- Chọn kệ lấy hàng --</option>
                        {locations.map((l) => (
                          <option key={l.id} value={l.id}>
                            {l.code}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        type="text"
                        placeholder="Nhập GUID LocationId..."
                        value={newRule.locationId}
                        onChange={(e) => setNewRule({ ...newRule, locationId: e.target.value })}
                        className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-mono"
                      />
                    )}
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="flex flex-col gap-1.5">
                      <label className="text-[10px] text-zinc-500">Tồn tối thiểu (Min)</label>
                      <input
                        type="number"
                        value={newRule.minQty}
                        onChange={(e) => setNewRule({ ...newRule, minQty: parseFloat(e.target.value) || 0 })}
                        className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label className="text-[10px] text-zinc-500">Tồn tối đa (Max)</label>
                      <input
                        type="number"
                        value={newRule.maxQty}
                        onChange={(e) => setNewRule({ ...newRule, maxQty: parseFloat(e.target.value) || 0 })}
                        className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                      />
                    </div>
                  </div>

                  <Button
                    type="submit"
                    disabled={submittingRule}
                    className="bg-emerald-600 hover:bg-emerald-500 text-white w-full h-9 text-xs rounded mt-2"
                  >
                    {submittingRule ? "Đang tạo..." : "Tạo quy tắc mới"}
                  </Button>
                </form>
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {/* Force Complete Modal Dialog */}
      {completingTask && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-zinc-900 border border-zinc-800 rounded-lg max-w-sm w-full text-white shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-zinc-800">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <CheckCircle className="h-4 w-4 text-emerald-500" />
                Xác nhận hoàn tất nhiệm vụ
              </h3>
              <button
                onClick={() => setCompletingTask(null)}
                className="text-zinc-500 hover:text-white transition-all"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="p-4 flex flex-col gap-4 text-xs">
              <div className="bg-zinc-950/40 p-3 rounded border border-zinc-800/80 font-mono text-[11px] text-zinc-400 flex flex-col gap-1">
                <div>Sản phẩm: {getProductCode(completingTask.itemId)}</div>
                <div>Lô hàng: {completingTask.lotNo}</div>
                <div>Từ kệ Bulk: {getLocationCode(completingTask.sourceLocationId)}</div>
                <div>Về Pick Face: {getLocationCode(completingTask.targetLocationId)}</div>
                <div className="text-zinc-200 mt-1">Yêu cầu: <span className="font-bold text-white text-xs">{completingTask.requestedQty}</span></div>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Số lượng dịch chuyển thực tế</label>
                <input
                  type="number"
                  value={actualQty}
                  onChange={(e) => setActualQty(parseFloat(e.target.value) || 0)}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full font-bold"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-zinc-500">Người thực hiện</label>
                <input
                  type="text"
                  placeholder="Nhập tên người thực hiện..."
                  value={operatorName}
                  onChange={(e) => setOperatorName(e.target.value)}
                  className="bg-zinc-800 border border-zinc-700 text-white rounded p-2 text-xs focus:outline-none h-9 w-full"
                />
              </div>
            </div>
            <div className="flex justify-end gap-3 p-4 border-t border-zinc-800 bg-zinc-950/20">
              <Button
                onClick={() => setCompletingTask(null)}
                variant="outline"
                className="border-zinc-800 hover:bg-zinc-800 text-zinc-300 text-xs h-8 px-4"
              >
                Hủy bỏ
              </Button>
              <Button
                onClick={handleCompleteTask}
                disabled={submittingComplete}
                className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4"
              >
                {submittingComplete ? "Đang xử lý..." : "Xác nhận"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
