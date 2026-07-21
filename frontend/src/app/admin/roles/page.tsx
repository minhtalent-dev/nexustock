"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Table, TableBody, TableCell, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { showSuccess, showApiErrorToast } from "@/lib/toast";
import { resolveApiError } from "@/lib/api-error-i18n";
import { useConfirmDialog } from "@/lib/confirm-dialog";
import { ShieldAlert, ShieldCheck, Plus, Trash2, Save, Sparkles } from "lucide-react";

interface RoleDto {
  id: string;
  name: string;
  description: string;
}

interface Permission {
  id: string;
  name: string;
  displayName: string;
  category: string;
}

export default function RolesPage() {
  const t = useTranslations("Admin.roles");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");
  const confirm = useConfirmDialog();

  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [allPermissions, setAllPermissions] = useState<Permission[]>([]);
  const [selectedRole, setSelectedRole] = useState<RoleDto | null>(null);
  const [rolePermissions, setRolePermissions] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [savingPermissions, setSavingPermissions] = useState(false);

  const [isRoleOpen, setIsRoleOpen] = useState(false);
  const [roleName, setRoleName] = useState("");
  const [roleDesc, setRoleDesc] = useState("");
  const [savingRole, setSavingRole] = useState(false);

  const selectRole = useCallback(async (role: RoleDto) => {
    setSelectedRole(role);
    try {
      const res = await api.get<Permission[]>(`/roles/${role.id}/permissions`);
      setRolePermissions(res.data.map((p) => p.id));
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadPermissionsFailed"));
      setRolePermissions([]);
    }
  }, [t, tErrors]);

  const fetchRoles = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<RoleDto[]>("/roles");
      setRoles(res.data);
      if (res.data.length > 0 && !selectedRole) {
        void selectRole(res.data[0]);
      }
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadRolesFailed"));
    } finally {
      setLoading(false);
    }
  }, [selectRole, selectedRole, t, tErrors]);

  const fetchAllPermissions = useCallback(async () => {
    try {
      const res = await api.get<Permission[]>("/permissions");
      setAllPermissions(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.loadCatalogFailed"));
    }
  }, [t, tErrors]);

  useEffect(() => {
    queueMicrotask(() => {
      void fetchRoles();
      void fetchAllPermissions();
    });
  }, [fetchRoles, fetchAllPermissions]);

  const openCreateRole = () => {
    setRoleName("");
    setRoleDesc("");
    setIsRoleOpen(true);
  };

  const handleCreateRole = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!roleName) return;

    setSavingRole(true);
    try {
      const res = await api.post<RoleDto>("/roles", { name: roleName, description: roleDesc });
      showSuccess(t("toastCreateSuccess"));
      setIsRoleOpen(false);
      await fetchRoles();
      selectRole(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.createFailed"));
    } finally {
      setSavingRole(false);
    }
  };

  const handleDeleteRole = async (role: RoleDto) => {
    const ok = await confirm({
      title: t("confirmDeleteTitle"),
      description: t("confirmDeleteDesc", { name: role.name }),
      confirmText: tc("delete"),
      cancelText: tc("cancel"),
      tone: "danger",
    });

    if (!ok) return;

    try {
      await api.delete(`/roles/${role.id}`);
      showSuccess(t("toastDeleteSuccess"));
      setSelectedRole(null);
      fetchRoles();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.deleteFailed"));
    }
  };

  const handlePermissionToggle = (permId: string) => {
    setRolePermissions((prev) =>
      prev.includes(permId) ? prev.filter((id) => id !== permId) : [...prev, permId]
    );
  };

  const handleSavePermissions = async () => {
    if (!selectedRole) return;
    setSavingPermissions(true);
    try {
      await api.post(`/roles/${selectedRole.id}/permissions`, {
        permissionIds: rolePermissions,
      });
      showSuccess(t("toastSaveSuccess", { name: selectedRole.name }));
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.saveFailed"));
    } finally {
      setSavingPermissions(false);
    }
  };

  const groupedPermissions = allPermissions.reduce((acc, perm) => {
    const cat = perm.category || t("defaultCategory");
    if (!acc[cat]) acc[cat] = [];
    acc[cat].push(perm);
    return acc;
  }, {} as Record<string, Permission[]>);

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-3">
          <ShieldCheck className="h-6 w-6 text-emerald-500" />
          {t("title")}
        </h1>
        <p className="text-xs text-zinc-400 mt-1">{t("subtitle")}</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
        <Card className="bg-[#111] border-zinc-800/80 lg:col-span-1">
          <CardHeader className="py-4 border-b border-zinc-800/60 flex flex-row items-center justify-between">
            <div>
              <CardTitle className="text-sm font-semibold text-white">{t("listTitle")}</CardTitle>
              <CardDescription className="text-[10px] text-zinc-500">{t("listHint")}</CardDescription>
            </div>
            <Button onClick={openCreateRole} variant="ghost" size="sm" className="text-emerald-400 hover:text-emerald-300 hover:bg-emerald-500/10 h-8 px-2 text-xs gap-1">
              <Plus className="h-3.5 w-3.5" /> {t("addRole")}
            </Button>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableBody>
                {roles.length === 0 ? (
                  <TableRow>
                    <TableCell className="text-center py-8 text-zinc-550 text-sm">
                      {loading ? t("loading") : t("empty")}
                    </TableCell>
                  </TableRow>
                ) : (
                  roles.map((role) => {
                    const isSelected = selectedRole?.id === role.id;
                    return (
                      <TableRow
                        key={role.id}
                        onClick={() => selectRole(role)}
                        className={`border-b border-zinc-800/30 cursor-pointer transition-colors ${
                          isSelected
                            ? "bg-zinc-850 hover:bg-zinc-850"
                            : "hover:bg-zinc-900/10"
                        }`}
                      >
                        <TableCell className="py-3 px-4 h-12 flex flex-col justify-center">
                          <span className={`text-sm font-medium ${isSelected ? "text-emerald-400" : "text-zinc-200"}`}>
                            {role.name}
                          </span>
                          <span className="text-[10px] text-zinc-500 line-clamp-1 mt-0.5">{role.description || t("noDescription")}</span>
                        </TableCell>
                        <TableCell className="py-3 px-4 h-12 text-right w-16 align-middle">
                          {role.name !== "Admin" && (
                            <Button
                              onClick={(e) => {
                                e.stopPropagation();
                                handleDeleteRole(role);
                              }}
                              variant="ghost"
                              size="sm"
                              className="text-red-400 hover:text-red-300 hover:bg-red-500/10 h-8 w-8 p-0 rounded-md"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card className="bg-[#111] border-zinc-800/80 lg:col-span-2">
          {selectedRole ? (
            <>
              <CardHeader className="py-4 border-b border-zinc-800/60 flex flex-row items-center justify-between">
                <div>
                  <CardTitle className="text-sm font-semibold text-white">
                    {t("matrixTitle")} <span className="text-emerald-400 font-mono">{selectedRole.name}</span>
                  </CardTitle>
                  <CardDescription className="text-[10px] text-zinc-550">
                    {t("matrixHint")}
                  </CardDescription>
                </div>
                {selectedRole.name === "Admin" ? (
                  <span className="text-[10px] text-yellow-500 bg-yellow-500/10 px-2 py-1 rounded font-semibold border border-yellow-500/20">
                    {t("adminLocked")}
                  </span>
                ) : (
                  <Button
                    onClick={handleSavePermissions}
                    disabled={savingPermissions}
                    className="bg-emerald-600 hover:bg-emerald-500 text-white h-8 text-xs gap-1.5 px-3"
                  >
                    <Save className="h-3.5 w-3.5" />
                    {savingPermissions ? tc("saving") : t("savePermissions")}
                  </Button>
                )}
              </CardHeader>
              <CardContent className="p-6 flex flex-col gap-6 max-h-[60vh] overflow-y-auto">
                {Object.keys(groupedPermissions).map((category) => (
                  <div key={category} className="flex flex-col gap-2.5 border-b border-zinc-800/30 pb-4 last:border-b-0 last:pb-0">
                    <h3 className="text-xs font-semibold text-zinc-400 uppercase tracking-wider flex items-center gap-2">
                      <Sparkles className="h-3.5 w-3.5 text-emerald-500" />
                      {category}
                    </h3>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mt-1.5">
                      {groupedPermissions[category].map((perm) => (
                        <div
                          key={perm.id}
                          className="flex items-start gap-2.5 p-3 rounded-lg border border-zinc-800/50 bg-zinc-900/10 hover:border-zinc-800 transition-colors"
                        >
                          <Checkbox
                            id={`perm-${perm.id}`}
                            checked={rolePermissions.includes(perm.id) || selectedRole.name === "Admin"}
                            disabled={selectedRole.name === "Admin"}
                            onCheckedChange={() => handlePermissionToggle(perm.id)}
                            className="border-zinc-700 data-[state=checked]:bg-emerald-600 data-[state=checked]:border-emerald-600 mt-0.5"
                          />
                          <div className="flex flex-col">
                            <label
                              htmlFor={`perm-${perm.id}`}
                              className="text-xs text-zinc-200 font-semibold cursor-pointer select-none"
                            >
                              {perm.displayName}
                            </label>
                            <span className="text-[10px] text-zinc-550 font-mono mt-0.5">{perm.name}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </CardContent>
            </>
          ) : (
            <CardContent className="py-16 text-center text-zinc-550 text-sm">
              <ShieldAlert className="h-10 w-10 text-zinc-650 mx-auto mb-2" />
              {t("selectRoleHint")}
            </CardContent>
          )}
        </Card>
      </div>

      <Dialog open={isRoleOpen} onOpenChange={setIsRoleOpen}>
        <DialogContent className="bg-[#111] border border-zinc-800 text-zinc-100 max-w-sm font-sans">
          <DialogHeader>
            <DialogTitle className="text-sm font-semibold text-white">{t("createDialogTitle")}</DialogTitle>
          </DialogHeader>

          <form onSubmit={handleCreateRole} className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="rname" className="text-xs text-zinc-400">{t("labelRoleName")}</Label>
              <Input
                id="rname"
                value={roleName}
                onChange={(e) => setRoleName(e.target.value)}
                className="bg-zinc-900 border-zinc-800 text-sm"
                placeholder={t("roleNamePlaceholder")}
                required
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="rdesc" className="text-xs text-zinc-400">{t("labelRoleDesc")}</Label>
              <Input
                id="rdesc"
                value={roleDesc}
                onChange={(e) => setRoleDesc(e.target.value)}
                className="bg-zinc-900 border-zinc-800 text-sm"
                placeholder={t("roleDescPlaceholder")}
              />
            </div>

            <DialogFooter className="mt-4 gap-2">
              <Button type="button" onClick={() => setIsRoleOpen(false)} variant="ghost" className="text-zinc-400 hover:text-zinc-200 h-9 text-sm">
                {tc("cancel")}
              </Button>
              <Button type="submit" disabled={savingRole} className="bg-emerald-600 hover:bg-emerald-500 text-white h-9 text-sm">
                {savingRole ? tc("saving") : t("createRole")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
