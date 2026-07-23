"use client";

import { useMemo, useState } from "react";
import { ChevronsUpDown, LogOut, Search } from "lucide-react";
import { useTranslations } from "next-intl";
import { LanguageSwitcher } from "@/components/language-switcher";
import { ThemeMenuSection } from "@/components/theme-switcher";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import api from "@/lib/api";
import type { User } from "@/providers/auth-provider";

type PermissionCatalogItem = {
  id: string;
  name: string;
  displayName: string;
  category: string;
};

type PermissionRow = {
  name: string;
  displayName: string;
  category: string;
};

function initials(name: string, email: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return `${parts[0]![0]!}${parts[parts.length - 1]![0]!}`.toUpperCase();
  }
  if (parts.length === 1 && parts[0]!.length >= 2) {
    return parts[0]!.slice(0, 2).toUpperCase();
  }
  const local = email.split("@")[0] || "U";
  return local.slice(0, 2).toUpperCase();
}

type Props = {
  user: User;
  roles: string[];
  permissions: string[];
  onLogout: () => void | Promise<void>;
};

export function SidebarUserMenu({
  user,
  roles,
  permissions,
  onLogout,
}: Props) {
  const t = useTranslations("Sidebar.account");
  const tc = useTranslations("Common.actions");
  const [menuOpen, setMenuOpen] = useState(false);
  const [permOpen, setPermOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [rows, setRows] = useState<PermissionRow[]>([]);
  const [loadingCatalog, setLoadingCatalog] = useState(false);

  const roleLabel =
    roles.length === 0 ? t("noRoles") : roles.join(", ");

  const filteredGroups = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = !q
      ? rows
      : rows.filter(
          (r) =>
            r.displayName.toLowerCase().includes(q) ||
            r.name.toLowerCase().includes(q) ||
            r.category.toLowerCase().includes(q)
        );

    const byCat = new Map<string, PermissionRow[]>();
    for (const row of list) {
      const key = row.category || t("uncategorized");
      const bucket = byCat.get(key) ?? [];
      bucket.push(row);
      byCat.set(key, bucket);
    }

    return [...byCat.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([category, items]) => ({
        category,
        items: items.sort((a, b) => a.displayName.localeCompare(b.displayName)),
      }));
  }, [query, rows, t]);

  async function loadPermissionRows() {
    setLoadingCatalog(true);
    try {
      const res = await api.get<PermissionCatalogItem[]>("/permissions");
      const catalog = new Map(
        (res.data ?? []).map((p) => [p.name, p] as const)
      );
      setRows(
        permissions.map((name) => {
          const hit = catalog.get(name);
          return {
            name,
            displayName: hit?.displayName?.trim() || name,
            category: hit?.category?.trim() || "",
          };
        })
      );
    } catch {
      setRows(
        permissions.map((name) => ({
          name,
          displayName: name,
          category: "",
        }))
      );
    } finally {
      setLoadingCatalog(false);
    }
  }

  function openPermissionsPreview() {
    setMenuOpen(false);
    setQuery("");
    window.setTimeout(() => {
      setPermOpen(true);
      void loadPermissionRows();
    }, 50);
  }

  return (
    <>
      <DropdownMenu open={menuOpen} onOpenChange={setMenuOpen}>
        <DropdownMenuTrigger
          data-testid="sidebar-user-menu-trigger"
          className="flex h-9 w-full items-center gap-2 rounded-md px-1.5 text-left outline-none transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-sidebar-ring"
        >
          <Avatar size="sm" className="bg-emerald-500/15 text-emerald-600 dark:text-emerald-400">
            <AvatarFallback className="bg-transparent text-[10px] font-semibold text-emerald-600 dark:text-emerald-400">
              {initials(user.fullName, user.email)}
            </AvatarFallback>
          </Avatar>
          <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
            {user.fullName}
          </span>
          <ChevronsUpDown className="size-3.5 shrink-0 text-muted-foreground" />
        </DropdownMenuTrigger>

        <DropdownMenuContent
          data-testid="sidebar-user-menu"
          side="top"
          align="start"
          sideOffset={6}
          className="w-56 min-w-56"
        >
          <DropdownMenuGroup>
            <DropdownMenuLabel className="space-y-1 px-2 py-1.5 font-normal">
              <p className="truncate text-sm font-medium text-foreground">
                {user.fullName}
              </p>
              <p className="truncate font-mono text-[11px] text-muted-foreground">
                {user.email}
              </p>
            </DropdownMenuLabel>
          </DropdownMenuGroup>

          <DropdownMenuSeparator />

          <DropdownMenuGroup>
            <div className="space-y-1.5 px-2 py-1.5 text-[11px]">
              <div className="flex items-start justify-between gap-2">
                <span className="shrink-0 text-muted-foreground">{t("role")}</span>
                <span
                  className="max-w-[9.5rem] text-right font-medium text-foreground"
                  title={roleLabel}
                >
                  {roleLabel}
                </span>
              </div>
              <div className="flex items-center justify-between gap-2">
                <span className="text-muted-foreground">{t("tenant")}</span>
                <span
                  className="max-w-[9rem] truncate font-mono text-muted-foreground"
                  title={user.tenantId || "—"}
                >
                  {user.tenantId || "—"}
                </span>
              </div>
              <div className="flex items-center justify-between gap-2">
                <span className="text-muted-foreground">{t("permissions")}</span>
                <button
                  type="button"
                  data-testid="sidebar-permissions-preview"
                  onClick={openPermissionsPreview}
                  className="rounded px-1.5 py-0.5 tabular-nums text-emerald-600 underline-offset-2 hover:bg-emerald-500/10 hover:underline dark:text-emerald-400"
                >
                  {t("permissionCount", { count: permissions.length })}
                </button>
              </div>
            </div>
          </DropdownMenuGroup>

          <ThemeMenuSection />

          <DropdownMenuSeparator />

          <div className="flex items-center justify-between gap-2 px-2 py-1.5">
            <span className="text-[11px] text-muted-foreground">
              {t("language")}
            </span>
            <LanguageSwitcher size="compact" />
          </div>

          <DropdownMenuSeparator />

          <DropdownMenuItem
            variant="destructive"
            data-testid="sidebar-user-logout"
            onClick={() => void onLogout()}
            className="gap-2"
          >
            <LogOut className="size-3.5" />
            {tc("logout")}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog open={permOpen} onOpenChange={setPermOpen}>
        <DialogContent
          className="sm:max-w-md"
          data-testid="sidebar-permissions-dialog"
        >
          <DialogHeader>
            <DialogTitle>{t("permissionsPreviewTitle")}</DialogTitle>
            <DialogDescription>
              {t("permissionsPreviewDesc", { count: permissions.length })}
            </DialogDescription>
          </DialogHeader>

          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t("permissionsSearch")}
              className="h-8 pl-8 text-xs"
              data-testid="sidebar-permissions-search"
            />
          </div>

          <div className="max-h-72 overflow-y-auto rounded-md border border-border/60 bg-muted/40">
            {loadingCatalog ? (
              <p className="px-3 py-6 text-center text-xs text-muted-foreground">
                {t("permissionsLoading")}
              </p>
            ) : filteredGroups.length === 0 ? (
              <p className="px-3 py-6 text-center text-xs text-muted-foreground">
                {t("permissionsEmpty")}
              </p>
            ) : (
              <div className="divide-y divide-border/40">
                {filteredGroups.map((group) => (
                  <div key={group.category}>
                    <div className="sticky top-0 z-10 bg-popover/95 px-3 py-1.5 text-[10px] font-semibold tracking-wide text-emerald-600 uppercase dark:text-emerald-400/90">
                      {group.category}
                    </div>
                    <ul>
                      {group.items.map((perm) => (
                        <li
                          key={perm.name}
                          className="px-3 py-1.5"
                          title={perm.name}
                        >
                          <p className="text-xs font-medium text-foreground">
                            {perm.displayName}
                          </p>
                          <p className="font-mono text-[10px] text-muted-foreground">
                            {perm.name}
                          </p>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="flex justify-end">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setPermOpen(false)}
            >
              {tc("close")}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
