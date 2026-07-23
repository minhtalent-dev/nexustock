"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getIntegrationMappings,
  createIntegrationMapping,
  updateIntegrationMapping,
  deleteIntegrationMapping
} from "@/features/erp-integration/api";
import { IntegrationMapping } from "@/features/erp-integration/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";

export default function IntegrationMappingsPage() {
  const t = useTranslations("Admin.integrations.mappings");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [mappings, setMappings] = useState<IntegrationMapping[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [mappingType, setMappingType] = useState<string>("all");
  const [searchCode, setSearchCode] = useState("");
  const [loading, setLoading] = useState(false);

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [newMapping, setNewMapping] = useState({
    externalSystem: "SAP-ERP",
    mappingType: "item",
    externalCode: "",
    internalCode: ""
  });
  const [isEditing, setIsEditing] = useState<IntegrationMapping | null>(null);
  const [editForm, setEditForm] = useState({
    internalCode: "",
    status: "active" as "active" | "inactive"
  });

  const fetchMappings = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getIntegrationMappings({
        mappingType: mappingType === "all" ? undefined : mappingType,
        externalCode: searchCode.trim() || undefined,
        page,
        pageSize
      });
      setMappings(data.items);
      setTotal(data.total);
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [mappingType, searchCode, page, pageSize, t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => void fetchMappings());
  }, [fetchMappings]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    fetchMappings();
  };

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newMapping.externalCode.trim() || !newMapping.internalCode.trim()) {
      showApiErrorToast("", t("errors.fillRequired"));
      return;
    }
    try {
      await createIntegrationMapping(newMapping);
      showSuccess(t("toastCreateSuccess"));
      setIsAddOpen(false);
      setNewMapping({
        externalSystem: "SAP-ERP",
        mappingType: "item",
        externalCode: "",
        internalCode: ""
      });
      fetchMappings();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    }
  };

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isEditing) return;
    if (!editForm.internalCode.trim()) {
      showApiErrorToast("", t("errors.internalCodeRequired"));
      return;
    }
    try {
      await updateIntegrationMapping(isEditing.id, editForm);
      showSuccess(t("toastUpdateSuccess"));
      setIsEditing(null);
      fetchMappings();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.updateFailed"));
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm(t("confirmDelete"))) return;
    try {
      await deleteIntegrationMapping(id);
      showSuccess(t("toastDeleteSuccess"));
      fetchMappings();
    } catch (err) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.deleteFailed"));
    }
  };

  const getTypeLabel = (type: string) => {
    switch (type) {
      case "item": return t("typeItemFull");
      case "warehouse": return t("typeWarehouseFull");
      case "partner": return t("typePartnerFull");
      case "uom": return t("typeUomFull");
      default: return type;
    }
  };

  return (
    <PageShell className="gap-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">{t("title")}</h1>
        <div className="flex gap-4">
          <form onSubmit={handleSearch} className="flex gap-2">
            <Input
              placeholder={t("searchPlaceholder")}
              value={searchCode}
              onChange={(e) => setSearchCode(e.target.value)}
              className="bg-card border-border text-foreground w-48 text-xs h-9"
            />
            <Select value={mappingType} onValueChange={(val) => { setMappingType(val); setPage(1); }}>
              <SelectTrigger className="bg-card border-border text-foreground w-40 text-xs h-9">
                <SelectValue placeholder={t("typePlaceholder")} />
              </SelectTrigger>
              <SelectContent className="bg-card border-border text-foreground text-xs">
                <SelectItem value="all">{t("typeAll")}</SelectItem>
                <SelectItem value="item">{t("typeItem")}</SelectItem>
                <SelectItem value="warehouse">{t("typeWarehouse")}</SelectItem>
                <SelectItem value="partner">{t("typePartner")}</SelectItem>
                <SelectItem value="uom">{t("typeUom")}</SelectItem>
              </SelectContent>
            </Select>
            <Button type="submit" size="sm" className="bg-muted border border-border hover:bg-zinc-700 text-xs">{t("searchBtn")}</Button>
          </form>
          <Button onClick={() => setIsAddOpen(true)} size="sm" className="bg-emerald-600 hover:bg-emerald-500 text-xs">
            {t("addMapping")}
          </Button>
        </div>
      </div>

      <Card className="bg-card border-border text-foreground">
        <CardHeader>
          <CardTitle className="text-sm font-semibold">{t("cardTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-6 text-xs text-muted-foreground font-mono">{t("loading")}</div>
          ) : (
            <Table className="text-xs">
              <TableHeader className="border-b border-border">
                <TableRow>
                  <TableHead className="text-muted-foreground">{t("colExternalSystem")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colMappingType")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colExternalCode")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colInternalCode")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colStatus")}</TableHead>
                  <TableHead className="text-muted-foreground">{t("colCreatedAt")}</TableHead>
                  <TableHead className="text-muted-foreground text-right">{t("colActions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {mappings.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center py-6 text-muted-foreground">
                      {t("empty")}
                    </TableCell>
                  </TableRow>
                ) : (
                  mappings.map((m) => (
                    <TableRow key={m.id} className="hover:bg-muted/30">
                      <TableCell className="font-semibold">{m.externalSystem}</TableCell>
                      <TableCell>{getTypeLabel(m.mappingType)}</TableCell>
                      <TableCell className="font-mono text-amber-400">{m.externalCode}</TableCell>
                      <TableCell className="font-mono text-emerald-400">{m.internalCode}</TableCell>
                      <TableCell>
                        <Badge variant={m.status === "active" ? "default" : "secondary"} className={m.status === "active" ? "bg-emerald-600/20 text-emerald-400 border border-emerald-500/30" : ""}>
                          {m.status === "active" ? t("statusActive") : t("statusInactive")}
                        </Badge>
                      </TableCell>
                      <TableCell>{new Date(m.createdAt).toLocaleDateString("vi-VN")}</TableCell>
                      <TableCell className="text-right flex justify-end gap-2">
                        <Button
                          size="xs"
                          variant="outline"
                          onClick={() => {
                            setIsEditing(m);
                            setEditForm({ internalCode: m.internalCode, status: m.status });
                          }}
                          className="border-border text-muted-foreground hover:text-foreground hover:bg-muted text-[10px] h-7"
                        >
                          {tc("edit")}
                        </Button>
                        <Button
                          size="xs"
                          variant="destructive"
                          onClick={() => handleDelete(m.id)}
                          className="text-[10px] h-7 bg-rose-950/40 text-rose-400 border border-rose-900/50 hover:bg-rose-900/30"
                        >
                          {tc("delete")}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          )}

          <div className="flex justify-between items-center mt-4">
            <div className="text-[10px] text-muted-foreground">{t("totalMappings", { total })}</div>
            <div className="flex gap-2">
              <Button
                size="xs"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
                className="bg-muted border border-zinc-750 text-foreground text-[10px] h-7 disabled:opacity-50"
              >
                {tc("previous")}
              </Button>
              <Button
                size="xs"
                disabled={page * pageSize >= total}
                onClick={() => setPage(page + 1)}
                className="bg-muted border border-zinc-750 text-foreground text-[10px] h-7 disabled:opacity-50"
              >
                {tc("next")}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={isAddOpen} onOpenChange={setIsAddOpen}>
        <DialogContent className="bg-background border-zinc-850 text-foreground text-xs max-w-md">
          <DialogHeader>
            <DialogTitle className="text-sm font-bold">{t("addDialogTitle")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleAdd} className="space-y-4">
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("externalSystemLabel")}</Label>
              <Input
                value={newMapping.externalSystem}
                onChange={(e) => setNewMapping({ ...newMapping, externalSystem: e.target.value })}
                className="bg-card border-border text-foreground text-xs h-9"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("mappingTypeLabel")}</Label>
              <Select
                value={newMapping.mappingType}
                onValueChange={(val) => setNewMapping({ ...newMapping, mappingType: val })}
              >
                <SelectTrigger className="bg-card border-border text-foreground text-xs h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="bg-card border-border text-foreground text-xs">
                  <SelectItem value="item">{t("typeItemFull")}</SelectItem>
                  <SelectItem value="warehouse">{t("typeWarehouseFull")}</SelectItem>
                  <SelectItem value="partner">{t("typePartnerFull")}</SelectItem>
                  <SelectItem value="uom">{t("typeUomFull")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("externalCodeLabel")}</Label>
              <Input
                placeholder={t("externalCodePlaceholder")}
                value={newMapping.externalCode}
                onChange={(e) => setNewMapping({ ...newMapping, externalCode: e.target.value })}
                className="bg-card border-border text-foreground text-xs h-9"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("internalCodeLabel")}</Label>
              <Input
                placeholder={t("internalCodePlaceholder")}
                value={newMapping.internalCode}
                onChange={(e) => setNewMapping({ ...newMapping, internalCode: e.target.value })}
                className="bg-card border-border text-foreground text-xs h-9"
              />
            </div>
            <DialogFooter className="pt-2">
              <Button type="button" variant="ghost" onClick={() => setIsAddOpen(false)} className="text-xs">{tc("cancel")}</Button>
              <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500 text-xs">{tc("save")}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={isEditing !== null} onOpenChange={() => setIsEditing(null)}>
        <DialogContent className="bg-background border-zinc-850 text-foreground text-xs max-w-md">
          <DialogHeader>
            <DialogTitle className="text-sm font-bold">{t("editDialogTitle")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleUpdate} className="space-y-4">
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("externalCodeReadonly")}</Label>
              <Input
                disabled
                value={isEditing?.externalCode || ""}
                className="bg-card/50 border-border text-muted-foreground text-xs h-9 cursor-not-allowed"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("internalCodeLabel")}</Label>
              <Input
                value={editForm.internalCode}
                onChange={(e) => setEditForm({ ...editForm, internalCode: e.target.value })}
                className="bg-card border-border text-foreground text-xs h-9"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-muted-foreground">{t("colStatus")}</Label>
              <Select
                value={editForm.status}
                onValueChange={(val: "active" | "inactive") => setEditForm({ ...editForm, status: val })}
              >
                <SelectTrigger className="bg-card border-border text-foreground text-xs h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="bg-card border-border text-foreground text-xs">
                  <SelectItem value="active">{t("statusActiveFull")}</SelectItem>
                  <SelectItem value="inactive">{t("statusInactiveFull")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <DialogFooter className="pt-2">
              <Button type="button" variant="ghost" onClick={() => setIsEditing(null)} className="text-xs">{tc("cancel")}</Button>
              <Button type="submit" className="bg-emerald-600 hover:bg-emerald-500 text-xs">{t("saveChanges")}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </PageShell>
  );
}
