"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import clsx from "clsx";
import { useState, useEffect, useMemo } from "react";
import { useTranslations } from "next-intl";
import { useAuth } from "@/hooks/use-auth";
import { ChevronDown, LogOut } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LanguageSwitcher } from "@/components/language-switcher";
import { MODULES_GROUPS } from "@/components/nav/nav-groups-modules";
import { OPS_GROUPS } from "@/components/nav/nav-groups-ops";
import {
  collapseKey,
  loadNavMode,
  saveNavMode,
  type NavMode,
} from "@/components/nav/nav-mode";
import { resolveLinks, type NavLinkDef } from "@/components/nav/nav-registry";

type NavGroup = {
  titleKey: string;
  title: string;
  links: Array<NavLinkDef & { label: string }>;
};

function isGroupActive(group: NavGroup, pathname: string, userPermissions: string[]): boolean {
  return group.links.some((link) => {
    if (link.permission && !userPermissions.includes(link.permission)) return false;
    return link.href === "/" ? pathname === "/" : pathname.startsWith(link.href);
  });
}

const COLLAPSED_KEY = "nexustock:sidebar:collapsed";

function loadCollapsed(): Record<string, boolean> {
  if (typeof window === "undefined") return {};
  try {
    const raw = localStorage.getItem(COLLAPSED_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

function saveCollapsed(state: Record<string, boolean>) {
  try {
    localStorage.setItem(COLLAPSED_KEY, JSON.stringify(state));
  } catch {
    // quota exceeded — bỏ qua
  }
}

export default function AppSidebar() {
  const pathname = usePathname();
  const { permissions, logout, user } = useAuth();
  const t = useTranslations("Sidebar");
  const tc = useTranslations("Common.actions");

  const [navMode, setNavMode] = useState<NavMode>("modules");

  const navGroups: NavGroup[] = useMemo(() => {
    const specs = navMode === "ops" ? OPS_GROUPS : MODULES_GROUPS;
    return specs.map((g) => ({
      titleKey: g.titleKey,
      title: t(`groups.${g.titleKey}`),
      links: resolveLinks(g.linkIds).map((link) => ({
        ...link,
        label: t(`links.${link.labelKey}`),
      })),
    }));
  }, [navMode, t]);

  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  // Hydrate navMode từ localStorage (SSR-safe)
  useEffect(() => {
    setNavMode(loadNavMode());
  }, []);

  // Effect A: đổi mode → seed collapsed theo prefix mới
  useEffect(() => {
    const saved = loadCollapsed();
    const next: Record<string, boolean> = {};
    for (const g of navGroups) {
      const k = collapseKey(navMode, g.titleKey);
      if (k in saved) next[k] = saved[k];
      else next[k] = !isGroupActive(g, pathname, permissions);
    }
    setCollapsed((prev) => ({ ...prev, ...next }));
    saveCollapsed({ ...saved, ...next });
    // Chỉ re-seed khi đổi mode / danh sách group theo mode
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [navMode, navGroups]);

  // Effect B: pathname/permissions — chỉ bổ sung key thiếu
  useEffect(() => {
    setCollapsed((prev) => {
      const saved = loadCollapsed();
      let changed = false;
      const next = { ...prev };
      for (const g of navGroups) {
        const k = collapseKey(navMode, g.titleKey);
        if (!(k in next)) {
          next[k] = k in saved ? saved[k] : !isGroupActive(g, pathname, permissions);
          changed = true;
        }
      }
      if (changed) saveCollapsed({ ...saved, ...next });
      return changed ? next : prev;
    });
  }, [pathname, permissions, navGroups, navMode]);

  const onSelectMode = (next: NavMode) => {
    setNavMode(next);
    saveNavMode(next);
  };

  const toggle = (titleKey: string) => {
    const k = collapseKey(navMode, titleKey);
    setCollapsed((prev) => {
      const next = { ...prev, [k]: !prev[k] };
      saveCollapsed({ ...loadCollapsed(), ...next });
      return next;
    });
  };

  const filteredGroups = navGroups
    .map((group) => {
      const filteredLinks = group.links.filter(
        (link) => !link.permission || permissions.includes(link.permission)
      );
      return { ...group, links: filteredLinks };
    })
    .filter((group) => group.links.length > 0);

  return (
    <aside className="w-60 border-r border-border bg-sidebar p-4 flex-shrink-0 flex flex-col min-h-screen">
      <Link href="/" className="flex items-center gap-2 mb-6 px-1 pt-1">
        <span className="text-lg font-bold text-white tracking-tight">Nexustock</span>
        <span className="text-[10px] text-emerald-400 bg-emerald-400/10 px-1.5 py-0.5 rounded font-semibold uppercase tracking-wider">
          WMS
        </span>
      </Link>

      <div
        role="group"
        aria-label={t("navMode.ariaLabel")}
        className="mb-4 flex gap-1 rounded-lg border border-zinc-800 p-1"
      >
        <button
          type="button"
          data-testid="nav-mode-modules"
          onClick={() => onSelectMode("modules")}
          className={clsx(
            "flex-1 rounded-md px-2 py-1.5 text-xs font-semibold",
            navMode === "modules"
              ? "bg-emerald-500/15 text-emerald-400"
              : "text-zinc-500 hover:text-zinc-300"
          )}
        >
          {t("navMode.modules")}
        </button>
        <button
          type="button"
          data-testid="nav-mode-ops"
          onClick={() => onSelectMode("ops")}
          className={clsx(
            "flex-1 rounded-md px-2 py-1.5 text-xs font-semibold",
            navMode === "ops"
              ? "bg-emerald-500/15 text-emerald-400"
              : "text-zinc-500 hover:text-zinc-300"
          )}
        >
          {t("navMode.ops")}
        </button>
      </div>

      <div className="flex-1 flex flex-col gap-1 overflow-y-auto pr-1">
        {filteredGroups.map((group) => {
          const active = isGroupActive(group, pathname, permissions);
          const ck = collapseKey(navMode, group.titleKey);
          const isOpen = !collapsed[ck];

          return (
            <div key={`${navMode}:${group.titleKey}`} className="mb-1">
              <Button
                onClick={() => toggle(group.titleKey)}
                variant="ghost"
                size="sm"
                className={clsx(
                  "w-full justify-between px-1 text-xs font-semibold uppercase tracking-wider h-8 hover:bg-transparent",
                  active ? "text-emerald-400" : "text-zinc-500 hover:text-zinc-300"
                )}
              >
                <span>{group.title}</span>
                <ChevronDown
                  className={clsx(
                    "h-3.5 w-3.5 transition-transform duration-200",
                    isOpen && "rotate-180"
                  )}
                />
              </Button>

              <div
                className={clsx(
                  "overflow-hidden transition-all duration-200 ease-in-out",
                  isOpen
                    ? "max-h-[min(28rem,70vh)] overflow-y-auto opacity-100 mt-1"
                    : "max-h-0 opacity-0"
                )}
              >
                <nav className="flex flex-col gap-1 pl-1">
                  {group.links.map((link) => {
                    const isActive =
                      pathname === link.href ||
                      (link.href !== "/" && pathname.startsWith(link.href));
                    return (
                      <Link
                        key={link.id}
                        href={link.href}
                        className={clsx(
                          "flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg transition-colors",
                          isActive
                            ? "bg-zinc-850 text-white border border-zinc-800"
                            : "text-zinc-400 hover:text-white hover:bg-zinc-900/50"
                        )}
                      >
                        <link.icon className="h-4 w-4 flex-shrink-0" />
                        {link.label}
                      </Link>
                    );
                  })}
                </nav>
              </div>
            </div>
          );
        })}
      </div>

      {user && (
        <div className="mt-auto pt-4 border-t border-zinc-800/60 flex flex-col gap-2">
          <div className="flex flex-col px-2">
            <span className="text-sm font-medium text-white truncate">{user.fullName}</span>
            <span className="text-[10px] text-zinc-500 truncate font-mono">{user.email}</span>
          </div>
          <LanguageSwitcher className="px-1" />
          <Button
            onClick={logout}
            variant="ghost"
            size="sm"
            className="w-full justify-start text-red-400 hover:text-red-300 hover:bg-red-500/10 gap-3 px-2 h-9"
          >
            <LogOut className="h-4 w-4 flex-shrink-0" />
            {tc("logout")}
          </Button>
        </div>
      )}

      {!user && (
        <div className="mt-auto pt-4 border-t border-zinc-800/60">
          <LanguageSwitcher className="justify-center w-full" />
        </div>
      )}

      <div className="mt-2 text-[10px] text-zinc-600 font-mono text-center">
        <span>Nexustock</span>
      </div>
    </aside>
  );
}
