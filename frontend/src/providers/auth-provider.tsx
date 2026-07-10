"use client";

import React, { createContext, useState, useEffect, useCallback } from "react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import { showError, showSuccess } from "@/lib/toast";

export interface User {
  email: string;
  fullName: string;
  tenantId: string;
}

interface AuthContextType {
  user: User | null;
  permissions: string[];
  isAuthenticated: boolean;
  loading: boolean;
  login: (email: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  hasPermission: (permission: string) => boolean;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [permissions, setPermissions] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const router = useRouter();

  const parseJwt = (token: string): User | null => {
    try {
      const base64Url = token.split(".")[1];
      const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
      const jsonPayload = decodeURIComponent(
        window
          .atob(base64)
          .split("")
          .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
          .join("")
      );
      const parsed = JSON.parse(jsonPayload);
      return {
        email: parsed.email || parsed.sub || "",
        fullName: parsed.fullName || parsed.unique_name || "User",
        tenantId: parsed.tenantId || "",
      };
    } catch {
      return null;
    }
  };

  const fetchPermissions = useCallback(async () => {
    try {
      const res = await api.get<string[]>("/me/permissions");
      setPermissions(res.data);
    } catch {
      setPermissions([]);
    }
  }, []);

  const restoreSession = useCallback(async () => {
    const token = localStorage.getItem("accessToken");
    if (token) {
      const parsedUser = parseJwt(token);
      if (parsedUser) {
        setUser(parsedUser);
        await fetchPermissions();
      } else {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("refreshToken");
      }
    }
    setLoading(false);
  }, [fetchPermissions]);

  useEffect(() => {
    restoreSession();
  }, [restoreSession]);

  const login = async (email: string, password: string): Promise<boolean> => {
    try {
      const res = await api.post("/auth/login", { email, password });
      const { token, refreshToken } = res.data;

      localStorage.setItem("accessToken", token);
      localStorage.setItem("refreshToken", refreshToken);

      const parsedUser = parseJwt(token);
      setUser(parsedUser);

      // Fetch permissions ngay sau khi login
      try {
        const permRes = await api.get<string[]>("/me/permissions");
        setPermissions(permRes.data);
      } catch (e) {
        setPermissions([]);
      }

      showSuccess("Đăng nhập thành công!");
      router.push("/");
      return true;
    } catch (err: any) {
      const msg = err.response?.data?.message || "Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.";
      showError(msg);
      return false;
    }
  };

  const logout = async () => {
    const refreshToken = localStorage.getItem("refreshToken");
    if (refreshToken) {
      try {
        await api.post("/auth/logout", { refreshToken });
      } catch {
        // Bỏ qua lỗi logout API khi token đã expired
      }
    }
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    setUser(null);
    setPermissions([]);
    showSuccess("Đã đăng xuất.");
    router.push("/login");
  };

  const hasPermission = (permission: string): boolean => {
    return permissions.includes(permission);
  };

  const value: AuthContextType = {
    user,
    permissions,
    isAuthenticated: !!user,
    loading,
    login,
    logout,
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
