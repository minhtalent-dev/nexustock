"use client";

import { useCallback, useEffect, useState } from "react";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { showError, showSuccess } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { Users, UserPlus, Check, X } from "lucide-react";

interface UserDto {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  tenantId: string;
  roles: string[];
}

interface RoleDto {
  id: string;
  name: string;
  description: string;
}

export default function UsersPage() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(false);
  
  // Dialog state
  const [isOpen, setIsOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);

  // Form state
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [usersRes, rolesRes] = await Promise.all([
        api.get<UserDto[]>("/users"),
        api.get<RoleDto[]>("/roles"),
      ]);
      setUsers(usersRes.data);
      setRoles(rolesRes.data);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Không thể tải dữ liệu."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    queueMicrotask(() => void fetchData());
  }, [fetchData]);

  const openCreate = () => {
    setEditingUser(null);
    setEmail("");
    setPassword("");
    setFullName("");
    setIsActive(true);
    setSelectedRoles([]);
    setIsOpen(true);
  };

  const openEdit = (user: UserDto) => {
    setEditingUser(user);
    setEmail(user.email);
    setPassword("");
    setFullName(user.fullName);
    setIsActive(user.isActive);
    setSelectedRoles(user.roles);
    setIsOpen(true);
  };

  const handleRoleToggle = (roleName: string) => {
    setSelectedRoles((prev) =>
      prev.includes(roleName) ? prev.filter((r) => r !== roleName) : [...prev, roleName]
    );
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);

    try {
      if (editingUser) {
        // Update user profile & status
        await api.put(`/users/${editingUser.id}`, { fullName, isActive });
        // Update user roles
        await api.post(`/users/${editingUser.id}/roles`, { roles: selectedRoles });
        showSuccess("Cập nhật người dùng thành công.");
      } else {
        // Create user
        await api.post("/users", { email, password, fullName, roles: selectedRoles });
        showSuccess("Tạo người dùng thành công.");
      }
      setIsOpen(false);
      fetchData();
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Lỗi thao tác."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            <Users className="h-6 w-6 text-emerald-500" />
            Quản lý người dùng
          </h1>
          <p className="text-xs text-zinc-400 mt-1">Quản lý tài khoản vận hành, phân quyền vai trò thuộc Tenant.</p>
        </div>
        <Button onClick={openCreate} className="bg-emerald-600 hover:bg-emerald-500 text-white gap-2 h-9 text-sm">
          <UserPlus className="h-4 w-4" />
          Thêm người dùng
        </Button>
      </div>

      <Card className="bg-[#111] border-zinc-800/80">
        <CardHeader className="py-4 border-b border-zinc-800/60 flex flex-row items-center justify-between">
          <CardTitle className="text-sm font-semibold text-white">Danh sách tài khoản</CardTitle>
          {loading && <div className="h-4 w-4 animate-spin rounded-full border-2 border-emerald-500 border-t-transparent" />}
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader className="bg-zinc-900/30 border-b border-zinc-800/60">
              <TableRow className="hover:bg-transparent">
                <TableHead className="text-zinc-400 font-semibold h-11">Họ tên</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Email</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11">Vai trò</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11 text-center w-36">Trạng thái</TableHead>
                <TableHead className="text-zinc-400 font-semibold h-11 text-right w-24 pr-6">Thao tác</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center py-8 text-zinc-550 text-sm">
                    {loading ? "Đang tải dữ liệu..." : "Không có người dùng nào."}
                  </TableCell>
                </TableRow>
              ) : (
                users.map((user) => (
                  <TableRow key={user.id} className="border-b border-zinc-800/30 hover:bg-zinc-900/10">
                    <TableCell className="font-medium text-white h-12">{user.fullName}</TableCell>
                    <TableCell className="text-zinc-300 font-mono text-sm h-12">{user.email}</TableCell>
                    <TableCell className="h-12">
                      <div className="flex flex-wrap gap-1">
                        {user.roles.length === 0 ? (
                          <span className="text-xs text-zinc-600">—</span>
                        ) : (
                          user.roles.map((r) => (
                            <Badge key={r} variant="secondary" className="bg-zinc-800 text-zinc-300 border-zinc-700/50 hover:bg-zinc-800">
                              {r}
                            </Badge>
                          ))
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-center h-12">
                      {user.isActive ? (
                        <span className="inline-flex items-center gap-1.5 px-2 py-1 rounded-md text-[10px] font-semibold bg-green-500/10 text-green-400 border border-green-500/20">
                          <Check className="h-3 w-3" /> Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1.5 px-2 py-1 rounded-md text-[10px] font-semibold bg-red-500/10 text-red-400 border border-red-500/20">
                          <X className="h-3 w-3" /> Vô hiệu
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-right h-12 pr-6">
                      <Button onClick={() => openEdit(user)} variant="ghost" size="sm" className="text-emerald-400 hover:text-emerald-300 hover:bg-emerald-500/10 h-8 px-2 text-xs">
                        Chỉnh sửa
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Dialog Form */}
      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="bg-[#111] border border-zinc-800 text-zinc-100 max-w-md">
          <DialogHeader>
            <DialogTitle className="text-base font-semibold text-white">
              {editingUser ? "Chỉnh sửa người dùng" : "Thêm người dùng mới"}
            </DialogTitle>
          </DialogHeader>

          <form onSubmit={handleSave} className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="email" className="text-xs text-zinc-400">Email</Label>
              <Input
                id="email"
                type="email"
                disabled={!!editingUser}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="bg-zinc-900 border-zinc-800 text-sm"
                placeholder="user@nexustock.com"
                required
              />
            </div>

            {!editingUser && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="pass" className="text-xs text-zinc-400">Mật khẩu</Label>
                <Input
                  id="pass"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="bg-zinc-900 border-zinc-800 text-sm"
                  placeholder="Mật khẩu ít nhất 8 ký tự"
                  required
                />
              </div>
            )}

            <div className="flex flex-col gap-2">
              <Label htmlFor="name" className="text-xs text-zinc-400">Họ và tên</Label>
              <Input
                id="name"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="bg-zinc-900 border-zinc-800 text-sm"
                placeholder="Họ và tên người dùng"
                required
              />
            </div>

            <div className="flex flex-col gap-2 mt-1">
              <Label className="text-xs text-zinc-400">Vai trò hệ thống</Label>
              <div className="grid grid-cols-2 gap-2 p-3 bg-zinc-900/50 border border-zinc-800/80 rounded-lg">
                {roles.map((role) => (
                  <div key={role.id} className="flex items-center gap-2">
                    <Checkbox
                      id={`role-${role.id}`}
                      checked={selectedRoles.includes(role.name)}
                      onCheckedChange={() => handleRoleToggle(role.name)}
                      className="border-zinc-700 data-[state=checked]:bg-emerald-600 data-[state=checked]:border-emerald-600"
                    />
                    <label htmlFor={`role-${role.id}`} className="text-xs text-zinc-300 font-medium cursor-pointer select-none">
                      {role.name}
                    </label>
                  </div>
                ))}
              </div>
            </div>

            <div className="flex items-center gap-2 mt-2">
              <Checkbox
                id="active"
                checked={isActive}
                onCheckedChange={(checked) => setIsActive(!!checked)}
                className="border-zinc-700 data-[state=checked]:bg-emerald-600 data-[state=checked]:border-emerald-600"
              />
              <label htmlFor="active" className="text-xs text-zinc-300 font-medium cursor-pointer select-none">
                Kích hoạt tài khoản hoạt động
              </label>
            </div>

            <DialogFooter className="mt-4 gap-2">
              <Button type="button" onClick={() => setIsOpen(false)} variant="ghost" className="text-zinc-400 hover:text-zinc-200 h-9 text-sm">
                Hủy bỏ
              </Button>
              <Button type="submit" disabled={saving} className="bg-emerald-600 hover:bg-emerald-500 text-white h-9 text-sm">
                {saving ? "Đang lưu..." : "Xác nhận"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
