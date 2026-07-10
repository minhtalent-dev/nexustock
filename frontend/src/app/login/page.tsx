"use client";

import React, { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { KeyRound, Mail, LogIn } from "lucide-react";

export default function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isAuthenticated) {
      router.push("/");
    }
  }, [isAuthenticated, router]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) return;

    setLoading(true);
    const success = await login(email, password);
    setLoading(false);

    if (success) {
      router.push("/");
    }
  };

  return (
    <div className="flex h-screen w-screen items-center justify-center bg-[#0a0a0a] text-zinc-100 font-sans">
      <div className="w-full max-w-md p-8 bg-[#111]/60 border border-zinc-800/80 backdrop-blur-md rounded-2xl shadow-2xl flex flex-col gap-6">
        {/* Brand/Title */}
        <div className="flex flex-col gap-1 text-center">
          <h1 className="text-2xl font-bold tracking-tight text-white flex items-center justify-center gap-2">
            <span>Nexustock</span>
            <span className="text-xs text-emerald-400 bg-emerald-400/10 px-1.5 py-0.5 rounded font-semibold uppercase tracking-wider">
              WMS
            </span>
          </h1>
          <p className="text-xs text-zinc-500 uppercase tracking-widest font-mono mt-1">Đăng nhập hệ thống</p>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="email" className="text-xs font-medium text-zinc-400">
              Email
            </Label>
            <div className="relative">
              <Mail className="absolute left-3 top-3 h-4 w-4 text-zinc-650" />
              <Input
                id="email"
                type="email"
                placeholder="name@nexustock.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="pl-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-emerald-500/80 focus:ring-emerald-500/20"
                required
                autoComplete="email"
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="password" className="text-xs font-medium text-zinc-400">
              Mật khẩu
            </Label>
            <div className="relative">
              <KeyRound className="absolute left-3 top-3 h-4 w-4 text-zinc-650" />
              <Input
                id="password"
                type="password"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="pl-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-emerald-500/80 focus:ring-emerald-500/20"
                required
                autoComplete="current-password"
              />
            </div>
          </div>

          <Button
            type="submit"
            disabled={loading}
            className="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-medium h-10 transition-colors mt-2"
          >
            {loading ? (
              <span className="flex items-center gap-2">
                <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                Đang xác thực...
              </span>
            ) : (
              <span className="flex items-center gap-2">
                <LogIn className="h-4 w-4" />
                Xác nhận đăng nhập
              </span>
            )}
          </Button>
        </form>

        {/* Hint (Boring over clever) */}
        <div className="pt-2 border-t border-zinc-800/40 text-center">
          <p className="text-[10px] text-zinc-600">
            Tài khoản mặc định: <span className="font-mono text-zinc-550 selection:bg-zinc-800">admin@nexustock.com</span> / <span className="font-mono text-zinc-550 selection:bg-zinc-800">AdminSecret123!</span>
          </p>
        </div>
      </div>
    </div>
  );
}
