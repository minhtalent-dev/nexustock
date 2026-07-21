"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { Settings, Plus, ClipboardList, ShieldCheck, FileText, Loader2 } from "lucide-react";

interface ConditionDto {
  field: string;
  operator: string;
  value: string;
}

interface ActionDto {
  actionType: string;
  actionParameters?: string;
}

interface RuleDto {
  id: string;
  code: string;
  name: string;
  type: string;
  priority: number;
  isActive: boolean;
  activeFrom?: string;
  activeTo?: string;
  conditions: ConditionDto[];
  action: ActionDto;
  createdAt: string;
  createdBy: string;
}

interface RuleExecutionLogDto {
  id: string;
  ruleSetId?: string;
  ruleTypeCode: string;
  inputContextJson: string;
  matched: boolean;
  resultAction: string;
  details?: string;
  createdAt: string;
}

export default function RulesPage() {
  const t = useTranslations("Admin.rules");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [activeTab, setActiveTab] = useState<"rules" | "logs">("rules");
  const [rules, setRules] = useState<RuleDto[]>([]);
  const [logs, setLogs] = useState<RuleExecutionLogDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [logPage, setLogPage] = useState(1);
  const [logPageSize] = useState(20);

  const [typeFilter, setTypeFilter] = useState("");
  const [logTypeFilter, setLogTypeFilter] = useState("");

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [ruleType, setRuleType] = useState("PUTAWAY");
  const [priority, setPriority] = useState(0);
  const [conditions, setConditions] = useState<ConditionDto[]>([{ field: "", operator: "EQUALS", value: "" }]);
  const [actionType, setActionType] = useState("BLOCK");
  const [actionParams, setActionParams] = useState("");

  const [selectedRule, setSelectedRule] = useState<RuleDto | null>(null);

  const fetchRules = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<RuleDto[]>("/rules", {
        params: { type: typeFilter || undefined }
      });
      setRules(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadRulesFailed"));
    } finally {
      setLoading(false);
    }
  }, [typeFilter, t, tErrors]);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<{ items: RuleExecutionLogDto[]; totalCount: number }>("/rules/logs", {
        params: {
          ruleType: logTypeFilter || undefined,
          page: logPage,
          pageSize: logPageSize
        }
      });
      setLogs(res.data.items);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadLogsFailed"));
    } finally {
      setLoading(false);
    }
  }, [logPage, logPageSize, logTypeFilter, t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => {
      if (activeTab === "rules") {
        void fetchRules();
      } else {
        void fetchLogs();
      }
    });
  }, [activeTab, fetchRules, fetchLogs]);

  const handleAddCondition = () => {
    setConditions([...conditions, { field: "", operator: "EQUALS", value: "" }]);
  };

  const handleRemoveCondition = (index: number) => {
    setConditions(conditions.filter((_, i) => i !== index));
  };

  const handleConditionChange = (index: number, key: keyof ConditionDto, value: string) => {
    const updated = [...conditions];
    updated[index] = { ...updated[index], [key]: value };
    setConditions(updated);
  };

  const handleCreateRule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (conditions.some(c => !c.field || !c.value)) {
      showError(t("errors.conditionsIncomplete"));
      return;
    }
    setCreating(true);
    try {
      await api.post("/rules", {
        code,
        name,
        type: ruleType,
        priority,
        conditions,
        action: {
          actionType,
          actionParameters: actionParams || null
        }
      });
      showSuccess(t("toastCreateSuccess"));
      setIsCreateOpen(false);
      resetForm();
      fetchRules();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setCreating(false);
    }
  };

  const resetForm = () => {
    setCode("");
    setName("");
    setRuleType("PUTAWAY");
    setPriority(0);
    setConditions([{ field: "", operator: "EQUALS", value: "" }]);
    setActionType("BLOCK");
    setActionParams("");
  };

  const getActionBadge = (action: string) => {
    switch (action) {
      case "ALLOW":
        return <Badge className="bg-green-600 hover:bg-green-700 text-white">ALLOW</Badge>;
      case "WARN":
        return <Badge className="bg-yellow-600 hover:bg-yellow-700 text-white">WARN</Badge>;
      case "BLOCK":
        return <Badge variant="destructive">BLOCK</Badge>;
      default:
        return <Badge variant="outline">{action}</Badge>;
    }
  };

  const ruleTypeOptions = (
    <>
      <option value="">{tc("all")}</option>
      <option value="PUTAWAY">{t("typePutaway")}</option>
      <option value="ALLOCATION">{t("typeAllocation")}</option>
      <option value="REPLENISHMENT">{t("typeReplenishment")}</option>
    </>
  );

  const ruleTypeSelectOptions = (
    <>
      <option value="PUTAWAY">{t("typePutaway")}</option>
      <option value="ALLOCATION">{t("typeAllocation")}</option>
      <option value="REPLENISHMENT">{t("typeReplenishment")}</option>
    </>
  );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{t("title")}</h1>
          <p className="text-muted-foreground text-sm">{t("subtitle")}</p>
        </div>
        <div className="flex gap-2">
          <Button variant={activeTab === "rules" ? "default" : "outline"} onClick={() => setActiveTab("rules")}>
            <Settings className="h-4 w-4 mr-2" /> {t("tabRules")}
          </Button>
          <Button variant={activeTab === "logs" ? "default" : "outline"} onClick={() => setActiveTab("logs")}>
            <FileText className="h-4 w-4 mr-2" /> {t("tabLogs")}
          </Button>
        </div>
      </div>

      {activeTab === "rules" ? (
        <>
          <div className="flex items-center justify-between bg-card p-4 rounded-lg border gap-4">
            <div className="flex items-center gap-4">
              <div className="space-y-1 w-48">
                <Label className="text-xs">{t("filterTypeLabel")}</Label>
                <select
                  className="w-full bg-background border rounded px-2 py-1.5 text-sm h-9"
                  value={typeFilter}
                  onChange={(e) => setTypeFilter(e.target.value)}
                >
                  {ruleTypeOptions}
                </select>
              </div>
              <Button variant="secondary" onClick={() => setTypeFilter("")} className="mt-5 h-9">
                {t("refreshFilter")}
              </Button>
            </div>
            <Button onClick={() => setIsCreateOpen(true)} className="gap-1.5">
              <Plus className="h-4 w-4" /> {t("createRule")}
            </Button>
          </div>

          <div className="bg-card border rounded-lg overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("colCode")}</TableHead>
                  <TableHead>{t("colName")}</TableHead>
                  <TableHead>{t("colType")}</TableHead>
                  <TableHead className="text-center">{t("colPriority")}</TableHead>
                  <TableHead>{t("colAction")}</TableHead>
                  <TableHead>{t("colStatus")}</TableHead>
                  <TableHead>{t("colCreatedAt")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center h-24">
                      <Loader2 className="h-6 w-6 animate-spin mx-auto text-muted-foreground" />
                    </TableCell>
                  </TableRow>
                ) : rules.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center h-24 text-muted-foreground">
                      {t("emptyRules")}
                    </TableCell>
                  </TableRow>
                ) : (
                  rules.map((rule) => (
                    <TableRow key={rule.id} className="cursor-pointer hover:bg-muted" onClick={() => setSelectedRule(rule)}>
                      <TableCell className="font-semibold">{rule.code}</TableCell>
                      <TableCell>{rule.name}</TableCell>
                      <TableCell>{rule.type}</TableCell>
                      <TableCell className="text-center font-mono">{rule.priority}</TableCell>
                      <TableCell>{getActionBadge(rule.action.actionType)}</TableCell>
                      <TableCell>
                        <Badge variant={rule.isActive ? "default" : "secondary"}>
                          {rule.isActive ? t("statusRunning") : t("statusPaused")}
                        </Badge>
                      </TableCell>
                      <TableCell>{new Date(rule.createdAt).toLocaleDateString("vi-VN")}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </>
      ) : (
        <>
          <div className="flex items-center gap-4 bg-card p-4 rounded-lg border">
            <div className="space-y-1 w-48">
              <Label className="text-xs">{t("filterTypeShort")}</Label>
              <select
                className="w-full bg-background border rounded px-2 py-1.5 text-sm h-9"
                value={logTypeFilter}
                onChange={(e) => { setLogTypeFilter(e.target.value); setLogPage(1); }}
              >
                {ruleTypeOptions}
              </select>
            </div>
            <Button variant="secondary" onClick={() => { setLogTypeFilter(""); setLogPage(1); }} className="mt-5 h-9">
              {t("refreshFilter")}
            </Button>
          </div>

          <div className="bg-card border rounded-lg overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("colTime")}</TableHead>
                  <TableHead>{t("colBusinessType")}</TableHead>
                  <TableHead>{t("colContext")}</TableHead>
                  <TableHead className="text-center">{t("colMatchResult")}</TableHead>
                  <TableHead>{t("colActionResult")}</TableHead>
                  <TableHead>{t("colEvalDetail")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center h-24">
                      <Loader2 className="h-6 w-6 animate-spin mx-auto text-muted-foreground" />
                    </TableCell>
                  </TableRow>
                ) : logs.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center h-24 text-muted-foreground">
                      {t("emptyLogs")}
                    </TableCell>
                  </TableRow>
                ) : (
                  logs.map((l) => (
                    <TableRow key={l.id}>
                      <TableCell className="text-xs">{new Date(l.createdAt).toLocaleString("vi-VN")}</TableCell>
                      <TableCell className="font-semibold text-xs">{l.ruleTypeCode}</TableCell>
                      <TableCell className="text-xs font-mono max-w-[200px] truncate" title={l.inputContextJson}>
                        {l.inputContextJson}
                      </TableCell>
                      <TableCell className="text-center">
                        <Badge variant={l.matched ? "default" : "secondary"}>
                          {l.matched ? t("matched") : t("notMatched")}
                        </Badge>
                      </TableCell>
                      <TableCell>{getActionBadge(l.resultAction)}</TableCell>
                      <TableCell className="text-xs max-w-[300px] truncate" title={l.details}>
                        {l.details}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </>
      )}

      <Dialog open={selectedRule !== null} onOpenChange={(open) => !open && setSelectedRule(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{t("detailTitle")}</DialogTitle>
          </DialogHeader>
          {selectedRule && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 text-sm border-b pb-2">
                <span className="text-muted-foreground">{t("detailCode")}</span>
                <span className="font-semibold text-right">{selectedRule.code}</span>
              </div>
              <div className="grid grid-cols-2 text-sm border-b pb-2">
                <span className="text-muted-foreground">{t("detailName")}</span>
                <span className="font-medium text-right">{selectedRule.name}</span>
              </div>
              <div className="grid grid-cols-2 text-sm border-b pb-2">
                <span className="text-muted-foreground">{t("detailType")}</span>
                <span className="font-medium text-right">{selectedRule.type}</span>
              </div>
              <div className="grid grid-cols-2 text-sm border-b pb-2">
                <span className="text-muted-foreground">{t("detailPriority")}</span>
                <span className="font-mono text-right font-semibold">{selectedRule.priority}</span>
              </div>
              <div className="grid grid-cols-2 text-sm border-b pb-2">
                <span className="text-muted-foreground">{t("detailAction")}</span>
                <span className="text-right">{getActionBadge(selectedRule.action.actionType)}</span>
              </div>
              {selectedRule.action.actionParameters && (
                <div className="space-y-1 text-sm border-b pb-2">
                  <span className="text-muted-foreground">{t("detailActionParams")}</span>
                  <pre className="bg-muted p-2 rounded text-xs font-mono">{selectedRule.action.actionParameters}</pre>
                </div>
              )}
              <div className="space-y-2">
                <span className="text-sm font-semibold flex items-center gap-1">
                  <ClipboardList className="h-4 w-4" /> {t("conditionsTitle")}
                </span>
                <div className="space-y-1.5">
                  {selectedRule.conditions.map((c, i) => (
                    <div key={i} className="flex items-center gap-1.5 text-xs bg-muted p-2 rounded">
                      <span className="font-mono text-blue-400">{c.field}</span>
                      <span className="font-bold text-zinc-400">{c.operator}</span>
                      <span className="font-mono text-green-400">{c.value}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSelectedRule(null)}>{tc("close")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{t("createTitle")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleCreateRule} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1">
                <Label>{t("labelCode")}</Label>
                <Input placeholder={t("codePlaceholder")} value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="space-y-1">
                <Label>{t("colName")}</Label>
                <Input placeholder={t("namePlaceholder")} value={name} onChange={(e) => setName(e.target.value)} required />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1">
                <Label>{t("colType")}</Label>
                <select
                  className="w-full bg-background border rounded px-2 py-1.5 text-sm h-10"
                  value={ruleType}
                  onChange={(e) => setRuleType(e.target.value)}
                >
                  {ruleTypeSelectOptions}
                </select>
              </div>
              <div className="space-y-1">
                <Label>{t("labelPriority")}</Label>
                <Input type="number" value={priority} onChange={(e) => setPriority(parseInt(e.target.value))} min={0} required />
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label className="flex items-center gap-1"><ShieldCheck className="h-4 w-4" /> {t("conditionsLabel")}</Label>
                <Button type="button" variant="outline" size="sm" onClick={handleAddCondition}>
                  {t("addCondition")}
                </Button>
              </div>
              <div className="space-y-2 max-h-48 overflow-y-auto pr-1">
                {conditions.map((c, index) => (
                  <div key={index} className="flex items-center gap-2 border p-2 rounded bg-muted/30">
                    <Input
                      placeholder={t("fieldPlaceholder")}
                      value={c.field}
                      onChange={(e) => handleConditionChange(index, "field", e.target.value)}
                      className="h-8 text-xs flex-1"
                      required
                    />
                    <select
                      className="bg-background border rounded px-1 py-1 text-xs h-8 w-28"
                      value={c.operator}
                      onChange={(e) => handleConditionChange(index, "operator", e.target.value)}
                    >
                      <option value="EQUALS">EQUALS</option>
                      <option value="NOT_EQUALS">NOT_EQUALS</option>
                      <option value="GREATER_THAN">GREATER_THAN</option>
                      <option value="LESS_THAN">LESS_THAN</option>
                      <option value="IN">IN</option>
                      <option value="NOT_IN">NOT_IN</option>
                    </select>
                    <Input
                      placeholder={t("valuePlaceholder")}
                      value={c.value}
                      onChange={(e) => handleConditionChange(index, "value", e.target.value)}
                      className="h-8 text-xs flex-1"
                      required
                    />
                    {conditions.length > 1 && (
                      <Button type="button" variant="ghost" size="sm" onClick={() => handleRemoveCondition(index)} className="text-red-500 hover:text-red-600 h-8 px-2">
                        {t("removeCondition")}
                      </Button>
                    )}
                  </div>
                ))}
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4 border-t pt-4">
              <div className="space-y-1">
                <Label>{t("labelAction")}</Label>
                <select
                  className="w-full bg-background border rounded px-2 py-1.5 text-sm h-10"
                  value={actionType}
                  onChange={(e) => setActionType(e.target.value)}
                >
                  <option value="BLOCK">{t("actionBlock")}</option>
                  <option value="WARN">{t("actionWarn")}</option>
                  <option value="ALLOW">{t("actionAllow")}</option>
                  <option value="PROPOSE_LOCATION">{t("actionProposeLocation")}</option>
                </select>
              </div>
              <div className="space-y-1">
                <Label>{t("labelActionParams")}</Label>
                <Input placeholder={t("actionParamsPlaceholder")} value={actionParams} onChange={(e) => setActionParams(e.target.value)} />
              </div>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => { setIsCreateOpen(false); resetForm(); }}>
                {tc("cancel")}
              </Button>
              <Button type="submit" disabled={creating}>
                {creating && <Loader2 className="h-4 w-4 animate-spin mr-1" />} {t("confirmCreate")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
