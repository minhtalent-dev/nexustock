"use client";

import { PageShell } from "@/components/layout/page-shell";

import React, { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useAuth } from "@/hooks/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { KeyRound, Mail, LogIn } from "lucide-react";
import { LanguageSwitcher } from "@/components/language-switcher";

export default function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const router = useRouter();
  const t = useTranslations("Login");
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
    <div className="flex h-screen w-screen items-center justify-center bg-background text-foreground font-sans">
      <div className="w-full max-w-md p-8 bg-card/60 border border-border/80 backdrop-blur-md rounded-2xl shadow-2xl flex flex-col gap-6">
        <div className="flex justify-end">
          <LanguageSwitcher />
        </div>
        <div className="flex flex-col gap-1 text-center">
          <h1 className="text-2xl font-bold tracking-tight text-foreground flex items-center justify-center gap-2">
            <span>Nexustock</span>
            <span className="text-xs text-emerald-400 bg-emerald-400/10 px-1.5 py-0.5 rounded font-semibold uppercase tracking-wider">
              WMS
            </span>
          </h1>
          <p className="text-xs text-muted-foreground uppercase tracking-widest font-mono mt-1">{t("title")}</p>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="email" className="text-xs font-medium text-muted-foreground">
              {t("username")}
            </Label>
            <div className="relative">
              <Mail className="absolute left-3 top-3 h-4 w-4 text-zinc-650" />
              <Input
                id="email"
                type="email"
                placeholder="name@nexustock.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="pl-10 bg-card/50 border-border text-sm focus:border-emerald-500/80 focus:ring-emerald-500/20"
                required
                autoComplete="email"
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="password" className="text-xs font-medium text-muted-foreground">
              {t("password")}
            </Label>
            <div className="relative">
              <KeyRound className="absolute left-3 top-3 h-4 w-4 text-zinc-650" />
              <Input
                id="password"
                type="password"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="pl-10 bg-card/50 border-border text-sm focus:border-emerald-500/80 focus:ring-emerald-500/20"
                required
                autoComplete="current-password"
              />
            </div>
          </div>

          <Button
            type="submit"
            disabled={loading}
            className="w-full bg-emerald-600 hover:bg-emerald-500 text-foreground font-medium h-10 transition-colors mt-2"
          >
            {loading ? (
              <span className="flex items-center gap-2">
                <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                {t("authenticating")}
              </span>
            ) : (
              <span className="flex items-center gap-2">
                <LogIn className="h-4 w-4" />
                {t("submit")}
              </span>
            )}
          </Button>
        </form>
      </div>
    </div>
  );
}
