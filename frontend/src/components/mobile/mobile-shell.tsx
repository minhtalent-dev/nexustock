"use client";

import React, { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Signal, SignalZero } from "lucide-react";
import { LanguageSwitcher } from "@/components/language-switcher";

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
    <div className="flex flex-col min-h-screen bg-background text-foreground select-none max-w-md mx-auto border-x border-border">
      {!isOnline && (
        <div className="bg-destructive text-center py-1.5 text-xs font-semibold flex items-center justify-center gap-2 animate-pulse text-white">
          <SignalZero className="h-3 w-3" />
          {t("status.offline")}
        </div>
      )}

      {isOnline && (
        <div className="bg-primary text-primary-foreground text-center py-1.5 text-xs font-semibold flex items-center justify-center gap-2">
          <Signal className="h-3 w-3" />
          {t("status.online")}
        </div>
      )}

      <header className="bg-card p-4 flex items-center justify-between gap-2 border-b border-border">
        <h1 className="text-lg font-bold truncate">{t("header.title")}</h1>
        <div className="flex items-center gap-2 shrink-0">
          <LanguageSwitcher className="scale-90 origin-right" />
          <span className="text-xs bg-muted px-2 py-1 rounded">{t("header.userLabel", { user: "NV-KHO" })}</span>
        </div>
      </header>

      <main className="flex-1 p-4 overflow-y-auto space-y-4">{children}</main>
    </div>
  );
}
