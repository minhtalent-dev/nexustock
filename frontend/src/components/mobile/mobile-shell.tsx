"use client";

import React, { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Signal, SignalZero } from "lucide-react";
import { LanguageSwitcher } from "@/components/language-switcher";
import { ThemeSwitcherInline } from "@/components/theme-switcher";

export default function MobileShell({ children }: { children: React.ReactNode }) {
  const t = useTranslations("Mobile.shell");
  const [isOnline, setIsOnline] = useState(true);

  useEffect(() => {
    queueMicrotask(() => setIsOnline(navigator.onLine));
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  return (
    <div className="mx-auto flex min-h-screen max-w-md select-none flex-col border-x border-border bg-background text-foreground">
      {!isOnline && (
        <div className="flex animate-pulse items-center justify-center gap-2 bg-destructive py-1.5 text-center text-xs font-semibold text-primary-foreground">
          <SignalZero className="h-3 w-3" />
          {t("status.offline")}
        </div>
      )}

      {isOnline && (
        <div className="flex items-center justify-center gap-2 bg-primary py-1.5 text-center text-xs font-semibold text-primary-foreground">
          <Signal className="h-3 w-3" />
          {t("status.online")}
        </div>
      )}

      <header className="flex items-center justify-between gap-2 border-b border-border bg-card p-4">
        <h1 className="truncate text-lg font-bold">{t("header.title")}</h1>
        <div className="flex shrink-0 items-center gap-2">
          <ThemeSwitcherInline />
          <LanguageSwitcher className="origin-right scale-90" />
          <span className="rounded bg-muted px-2 py-1 text-xs">
            {t("header.userLabel", { user: "NV-KHO" })}
          </span>
        </div>
      </header>

      <main className="flex-1 space-y-4 overflow-y-auto p-4">{children}</main>
    </div>
  );
}
