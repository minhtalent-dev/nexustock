"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { OpsExportButtons } from "@/components/ops-export-buttons";
import { RefreshCw, Play, Layers, ClipboardList, CheckCircle, Settings, Plus, X } from "lucide-react";

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
  const t = useTranslations("Admin.replenishment");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [activeTab, setActiveTab] = useState<"rules" | "tasks">("tasks");
  const [rules, setRules] = useState<ReplenishmentRule[]>([]);
  const [tasks, setTasks] = useState<ReplenishmentTask[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [locations, setLocations] = useState<StorageLocation[]>([]);

  const [loadingRules, setLoadingRules] = useState(false);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [submittingRule, setSubmittingRule] = useState(false);
  const [runningEngine, setRunningEngine] = useState(false);

  const [newRule, setNewRule] = useState({
    itemId: "",
    locationId: "",
    minQty: 10,
    maxQty: 50,
  });

  const [completingTask, setCompletingTask] = useState<ReplenishmentTask | null>(null);
  const [actualQty, setActualQty] = useState<number>(0);
  const [operatorName, setOperatorName] = useState("");
  const [submittingComplete, setSubmittingComplete] = useState(false);

  const fetchRules = useCallback(async () => {
    setLoadingRules(true);
    try {
      const res = await api.get<ReplenishmentRule[]>("/replenishment/rules");
      setRules(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadRulesFailed"));
    } finally {
      setLoadingRules(false);
    }
  }, [t, tErrors]);

  const fetchTasks = useCallback(async () => {
    setLoadingTasks(true);
    try {
      const res = await api.get<ReplenishmentTask[]>("/replenishment/tasks");
      setTasks(res.data || []);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadTasksFailed"));
    } finally {
      setLoadingTasks(false);
    }
  }, [t, tErrors]);

  const fetchMetadata = useCallback(async () => {
    try {
      const prodRes = await api.get<Product[]>("/masterdata/products");
      setProducts(prodRes.data || []);
    } catch {
      // ignore if API not ready
    }

    try {
      const locRes = await api.get<StorageLocation[]>("/masterdata/locations");
      setLocations(locRes.data || []);
    } catch {
      // ignore
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => {
      void fetchRules();
      void fetchTasks();
      void fetchMetadata();
    });
  }, [fetchRules, fetchTasks, fetchMetadata]);

  const handleCreateRule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newRule.itemId || !newRule.locationId) {
      showApiErrorToast("", t("errors.fieldsRequired"));
      return;
    }

    setSubmittingRule(true);
    try {
      await api.post("/replenishment/rules", newRule);
      showSuccess(t("toastCreateRuleSuccess"));
      setNewRule({ itemId: "", locationId: "", minQty: 10, maxQty: 50 });
      fetchRules();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createRuleFailed"));
    } finally {
      setSubmittingRule(false);
    }
  };

  const handleRunEngine = async () => {
    setRunningEngine(true);
    try {
      const res = await api.post("/replenishment/generate?strategy=FEFO");
      const generatedCount = res.data?.length || 0;
      showSuccess(t("toastRunEngineSuccess", { count: generatedCount }));
      fetchTasks();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.runEngineFailed"));
    } finally {
      setRunningEngine(false);
    }
  };

  const handleCancelTask = async (taskId: string) => {
    if (!confirm(t("confirmCancel"))) {
      return;
    }

    try {
      await api.post(`/replenishment/tasks/${taskId}/cancel`);
      showSuccess(t("toastCancelSuccess"));
      fetchTasks();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.cancelFailed"));
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
      showApiErrorToast("", t("errors.actualQtyInvalid"));
      return;
    }

    setSubmittingComplete(true);
    try {
      const payload = {
        actualQty,
        operatorName: operatorName || tc("system"),
      };
      await api.post(`/replenishment/tasks/${completingTask.id}/complete`, payload);
      showSuccess(t("toastCompleteSuccess"));
      setCompletingTask(null);
      fetchTasks();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.completeFailed"));
    } finally {
      setSubmittingComplete(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "COMPLETED":
        return <Badge className="bg-emerald-600 hover:bg-emerald-500 text-white">{t("statusCompleted")}</Badge>;
      case "CANCELLED":
        return <Badge className="bg-rose-600 hover:bg-rose-500 text-white">{t("statusCancelled")}</Badge>;
      case "ASSIGNED":
        return <Badge className="bg-amber-600 hover:bg-amber-500 text-white">{t("statusAssigned")}</Badge>;
      case "PENDING":
      default:
        return <Badge className="bg-muted hover:bg-zinc-700 text-muted-foreground">{t("statusPending")}</Badge>;
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
    <PageShell className="gap-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-3">
            <Layers className="h-6 w-6 text-emerald-500" />
            {t("title")}
          </h1>
          <p className="text-xs text-muted-foreground mt-1">{t("subtitle")}</p>
        </div>
        <div className="flex items-center gap-3">
          <OpsExportButtons type="REPLENISHMENT_TASKS" />
          <Button
            onClick={handleRunEngine}
            disabled={runningEngine}
            className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-9 px-4 flex items-center gap-2"
          >
            <Play className={`h-4 w-4 ${runningEngine ? "animate-spin" : ""}`} />
            {runningEngine ? t("runningEngine") : t("runEngine")}
          </Button>
          <Button
            onClick={() => {
              fetchRules();
              fetchTasks();
            }}
            variant="outline"
            className="border-border hover:bg-muted text-muted-foreground h-9 w-9 p-0"
          >
            <RefreshCw className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <div className="flex border-b border-border">
        <button
          onClick={() => setActiveTab("tasks")}
          className={`py-2.5 px-4 text-xs font-semibold border-b-2 transition-all ${
            activeTab === "tasks" ? "border-emerald-500 text-emerald-500" : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          {t("tabTasks")}
        </button>
        <button
          onClick={() => setActiveTab("rules")}
          className={`py-2.5 px-4 text-xs font-semibold border-b-2 transition-all ${
            activeTab === "rules" ? "border-emerald-500 text-emerald-500" : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          {t("tabRules")}
        </button>
      </div>

      {activeTab === "tasks" ? (
        <Card className="bg-card border-border text-foreground">
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-2">
              <ClipboardList className="h-4 w-4 text-emerald-500" />
              {t("tasksTitle")}
            </CardTitle>
            <CardDescription className="text-xs text-muted-foreground">{t("tasksDesc")}</CardDescription>
          </CardHeader>
          <CardContent>
            {loadingTasks && tasks.length === 0 ? (
              <div className="text-center py-12 text-muted-foreground text-xs">{t("loadingTasks")}</div>
            ) : tasks.length === 0 ? (
              <div className="text-center py-12 text-muted-foreground text-xs">{t("emptyTasks")}</div>
            ) : (
              <div className="overflow-x-auto">
                <Table className="text-xs">
                  <TableHeader className="border-b border-border">
                    <TableRow className="border-b border-border hover:bg-muted/50">
                      <TableHead className="text-muted-foreground">{t("colProduct")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colSourceBulk")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colTargetPickFace")}</TableHead>
                      <TableHead className="text-muted-foreground">{t("colLotNo")}</TableHead>
                      <TableHead className="text-muted-foreground text-right">{t("colRequestedQty")}</TableHead>
                      <TableHead className="text-muted-foreground text-right">{t("colActualQty")}</TableHead>
                      <TableHead className="text-muted-foreground text-center">{t("colStatus")}</TableHead>
                      <TableHead className="text-muted-foreground text-center">{t("colActions")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tasks.map((task) => (
                      <TableRow key={task.id} className="border-b border-border/50 hover:bg-muted/30">
                        <TableCell className="font-medium text-muted-foreground">{getProductCode(task.itemId)}</TableCell>
                        <TableCell className="text-muted-foreground font-mono">{getLocationCode(task.sourceLocationId)}</TableCell>
                        <TableCell className="text-emerald-400 font-mono">{getLocationCode(task.targetLocationId)}</TableCell>
                        <TableCell className="text-muted-foreground">{task.lotNo}</TableCell>
                        <TableCell className="text-right font-semibold">{task.requestedQty}</TableCell>
                        <TableCell className="text-right text-muted-foreground">{task.actualQty ?? "-"}</TableCell>
                        <TableCell className="text-center">{getStatusBadge(task.status)}</TableCell>
                        <TableCell className="text-center flex justify-center gap-2">
                          {(task.status === "PENDING" || task.status === "ASSIGNED") && (
                            <>
                              <Button
                                onClick={() => handleOpenComplete(task)}
                                className="bg-emerald-600 hover:bg-emerald-500 text-white h-7 px-3 text-[10px] rounded"
                              >
                                {t("completeBtn")}
                              </Button>
                              <Button
                                onClick={() => handleCancelTask(task.id)}
                                variant="outline"
                                className="border-border hover:bg-muted text-rose-500 h-7 px-3 text-[10px] rounded"
                              >
                                {t("cancelBtn")}
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
          <div className="lg:col-span-2">
            <Card className="bg-card border-border text-foreground">
              <CardHeader>
                <CardTitle className="text-sm font-semibold flex items-center gap-2">
                  <Settings className="h-4 w-4 text-emerald-500" />
                  {t("rulesTitle")}
                </CardTitle>
              </CardHeader>
              <CardContent>
                {loadingRules && rules.length === 0 ? (
                  <div className="text-center py-12 text-muted-foreground text-xs">{t("loadingRules")}</div>
                ) : rules.length === 0 ? (
                  <div className="text-center py-12 text-muted-foreground text-xs">{t("emptyRules")}</div>
                ) : (
                  <div className="overflow-x-auto">
                    <Table className="text-xs">
                      <TableHeader className="border-b border-border">
                        <TableRow className="border-b border-border hover:bg-muted/50">
                          <TableHead className="text-muted-foreground">{t("colProduct")}</TableHead>
                          <TableHead className="text-muted-foreground">{t("colPickFace")}</TableHead>
                          <TableHead className="text-muted-foreground text-right">{t("colMinQty")}</TableHead>
                          <TableHead className="text-muted-foreground text-right">{t("colMaxQty")}</TableHead>
                          <TableHead className="text-muted-foreground">{t("colCreatedBy")}</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {rules.map((rule) => (
                          <TableRow key={rule.id} className="border-b border-border/50 hover:bg-muted/30">
                            <TableCell className="font-semibold text-foreground">{getProductCode(rule.itemId)}</TableCell>
                            <TableCell className="font-mono text-muted-foreground">{getLocationCode(rule.locationId)}</TableCell>
                            <TableCell className="text-right text-amber-500 font-semibold">{rule.minQty}</TableCell>
                            <TableCell className="text-right text-emerald-500 font-semibold">{rule.maxQty}</TableCell>
                            <TableCell className="text-muted-foreground">{rule.createdBy}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          <div className="lg:col-span-1">
            <Card className="bg-card border-border text-foreground">
              <CardHeader>
                <CardTitle className="text-sm font-semibold flex items-center gap-2">
                  <Plus className="h-4 w-4 text-emerald-500" />
                  {t("addRuleTitle")}
                </CardTitle>
              </CardHeader>
              <CardContent>
                <form onSubmit={handleCreateRule} className="flex flex-col gap-4 text-xs">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-[10px] text-muted-foreground">{t("itemLabel")}</label>
                    {products.length > 0 ? (
                      <select
                        value={newRule.itemId}
                        onChange={(e) => setNewRule({ ...newRule, itemId: e.target.value })}
                        className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full"
                      >
                        <option value="">{t("selectProduct")}</option>
                        {products.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.code} - {p.name}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        type="text"
                        placeholder={t("itemIdPlaceholder")}
                        value={newRule.itemId}
                        onChange={(e) => setNewRule({ ...newRule, itemId: e.target.value })}
                        className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full font-mono"
                      />
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-[10px] text-muted-foreground">{t("locationLabel")}</label>
                    {locations.length > 0 ? (
                      <select
                        value={newRule.locationId}
                        onChange={(e) => setNewRule({ ...newRule, locationId: e.target.value })}
                        className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full"
                      >
                        <option value="">{t("selectLocation")}</option>
                        {locations.map((l) => (
                          <option key={l.id} value={l.id}>
                            {l.code}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        type="text"
                        placeholder={t("locationIdPlaceholder")}
                        value={newRule.locationId}
                        onChange={(e) => setNewRule({ ...newRule, locationId: e.target.value })}
                        className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full font-mono"
                      />
                    )}
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="flex flex-col gap-1.5">
                      <label className="text-[10px] text-muted-foreground">{t("minQtyLabel")}</label>
                      <input
                        type="number"
                        value={newRule.minQty}
                        onChange={(e) => setNewRule({ ...newRule, minQty: parseFloat(e.target.value) || 0 })}
                        className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full"
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label className="text-[10px] text-muted-foreground">{t("maxQtyLabel")}</label>
                      <input
                        type="number"
                        value={newRule.maxQty}
                        onChange={(e) => setNewRule({ ...newRule, maxQty: parseFloat(e.target.value) || 0 })}
                        className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full"
                      />
                    </div>
                  </div>

                  <Button
                    type="submit"
                    disabled={submittingRule}
                    className="bg-emerald-600 hover:bg-emerald-500 text-white w-full h-9 text-xs rounded mt-2"
                  >
                    {submittingRule ? t("creatingRule") : t("createRule")}
                  </Button>
                </form>
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {completingTask && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-card border border-border rounded-lg max-w-lg w-full text-foreground shadow-xl flex flex-col">
            <div className="flex items-center justify-between p-4 border-b border-border">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <CheckCircle className="h-4 w-4 text-emerald-500" />
                {t("completeDialogTitle")}
              </h3>
              <button onClick={() => setCompletingTask(null)} className="text-muted-foreground hover:text-foreground transition-all">
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="p-4 flex flex-col gap-4 text-xs">
              <div className="bg-background/40 p-3 rounded border border-border/80 font-mono text-[11px] text-muted-foreground flex flex-col gap-1">
                <div>{t("completeProduct", { product: getProductCode(completingTask.itemId) })}</div>
                <div>{t("completeLot", { lot: completingTask.lotNo })}</div>
                <div>{t("completeFromBulk", { location: getLocationCode(completingTask.sourceLocationId) })}</div>
                <div>{t("completeToPickFace", { location: getLocationCode(completingTask.targetLocationId) })}</div>
                <div className="text-foreground mt-1">
                  {t("completeRequested", { qty: completingTask.requestedQty })}
                </div>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-muted-foreground">{t("actualQtyLabel")}</label>
                <input
                  type="number"
                  value={actualQty}
                  onChange={(e) => setActualQty(parseFloat(e.target.value) || 0)}
                  className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full font-bold"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-[10px] text-muted-foreground">{t("operatorLabel")}</label>
                <input
                  type="text"
                  placeholder={t("operatorPlaceholder")}
                  value={operatorName}
                  onChange={(e) => setOperatorName(e.target.value)}
                  className="bg-muted border border-border text-foreground rounded p-2 text-xs focus:outline-none h-9 w-full"
                />
              </div>
            </div>
            <div className="flex justify-end gap-3 p-4 border-t border-border bg-background/20">
              <Button onClick={() => setCompletingTask(null)} variant="outline" className="border-border hover:bg-muted text-muted-foreground text-xs h-8 px-4">
                {t("cancelBtn")}
              </Button>
              <Button onClick={handleCompleteTask} disabled={submittingComplete} className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs h-8 px-4">
                {submittingComplete ? t("processing") : t("confirmComplete")}
              </Button>
            </div>
          </div>
        </div>
      )}
    </PageShell>
  );
}
