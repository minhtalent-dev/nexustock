"use client";

import { useEffect, useState } from "react";
import { Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { useTranslations } from "next-intl";
import {
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import clsx from "clsx";

type ThemeValue = "system" | "light" | "dark";

/** Compact horizontal theme row for SidebarUserMenu (System · Light · Dark). */
export function ThemeMenuSection() {
  const t = useTranslations("Sidebar.account");
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;

  const value = (theme as ThemeValue) || "system";
  const opts: { id: ThemeValue; icon: typeof Sun; label: string }[] = [
    { id: "system", icon: Monitor, label: t("themeSystem") },
    { id: "light", icon: Sun, label: t("themeLight") },
    { id: "dark", icon: Moon, label: t("themeDark") },
  ];

  return (
    <>
      <DropdownMenuSeparator />
      <DropdownMenuGroup>
        <DropdownMenuLabel className="px-2 py-1 text-[11px] font-normal text-muted-foreground">
          {t("themeLabel")}
        </DropdownMenuLabel>
        <div
          role="group"
          aria-label={t("themeLabel")}
          className="mx-2 mb-1.5 flex items-stretch gap-0.5 rounded-md border border-border bg-muted/40 p-0.5"
        >
          {opts.map((o) => {
            const Icon = o.icon;
            const active = value === o.id;
            return (
              <Button
                key={o.id}
                type="button"
                size="sm"
                variant={active ? "default" : "ghost"}
                data-testid={`theme-option-${o.id}`}
                aria-pressed={active}
                title={o.label}
                className={clsx(
                  "h-7 min-w-0 flex-1 gap-1 px-1 text-[10px] font-medium",
                  !active && "text-muted-foreground"
                )}
                onClick={(e) => {
                  e.preventDefault();
                  setTheme(o.id);
                }}
              >
                <Icon className="size-3 shrink-0" />
                <span className="truncate">{o.label}</span>
              </Button>
            );
          })}
        </div>
      </DropdownMenuGroup>
    </>
  );
}

/** Compact 3-button control for MobileShell. */
export function ThemeSwitcherInline({ className }: { className?: string }) {
  const t = useTranslations("Sidebar.account");
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    return (
      <div
        className={clsx("inline-flex h-8 w-[6.5rem] rounded-md border border-border", className)}
        aria-hidden
      />
    );
  }

  const value = (theme as ThemeValue) || "system";
  const opts: { id: ThemeValue; icon: typeof Sun; label: string }[] = [
    { id: "system", icon: Monitor, label: t("themeSystem") },
    { id: "light", icon: Sun, label: t("themeLight") },
    { id: "dark", icon: Moon, label: t("themeDark") },
  ];

  return (
    <div
      role="group"
      aria-label={t("themeLabel")}
      data-testid="theme-switcher-inline"
      className={clsx(
        "inline-flex items-center gap-0.5 rounded-md border border-border bg-muted/40 p-0.5",
        className
      )}
    >
      {opts.map((o) => {
        const Icon = o.icon;
        const active = value === o.id;
        return (
          <Button
            key={o.id}
            type="button"
            size="sm"
            variant={active ? "default" : "ghost"}
            data-testid={`theme-option-${o.id}`}
            aria-pressed={active}
            title={o.label}
            className="h-7 w-7 px-0"
            onClick={() => setTheme(o.id)}
          >
            <Icon className="size-3.5" />
            <span className="sr-only">{o.label}</span>
          </Button>
        );
      })}
    </div>
  );
}
