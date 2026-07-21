"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import clsx from "clsx";
import { useState, useEffect, useMemo } from "react";
import { useTranslations } from "next-intl";
import { useAuth } from "@/hooks/use-auth";
import {
  Home,
  Activity,
  Package,
  Ruler,
  Warehouse,
  Grid3X3,
  MapPin,
  Users,
  Tag,
  Upload,
  ChevronDown,
  Shield,
  Lock,
  FileText,
  LogOut,
  ClipboardList,
  Archive,
  CheckSquare,
  Box,
  Truck,
  ClipboardCheck,
  AlertCircle,
  Sliders,
  Layers,
  RefreshCw,
  GitFork,
  Monitor,
  Zap,
  Clock,
  BarChart3,
  ShieldCheck,
  GitBranch,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { LanguageSwitcher } from "@/components/language-switcher";

type LinkItem = {
  href: string;
  labelKey: string;
  icon: React.ComponentType<{ className?: string }>;
  permission?: string;
};

type NavGroupDef = {
  titleKey: string;
  links: LinkItem[];
};

const navGroupDefs: NavGroupDef[] = [
  {
    titleKey: "overview",
    links: [
      { href: "/", labelKey: "home", icon: Home },
      { href: "/health-ui", labelKey: "healthUi", icon: Activity },
    ],
  },
  {
    titleKey: "materials",
    links: [
      { href: "/master-data/products", labelKey: "products", icon: Package, permission: "MasterData.Products.View" },
      { href: "/master-data/uoms", labelKey: "uoms", icon: Ruler, permission: "MasterData.Uoms.View" },
    ],
  },
  {
    titleKey: "warehouse",
    links: [
      { href: "/master-data/warehouses", labelKey: "warehouses", icon: Warehouse, permission: "MasterData.Warehouses.View" },
      { href: "/master-data/zones", labelKey: "zones", icon: Grid3X3, permission: "MasterData.Zones.View" },
      { href: "/master-data/locations", labelKey: "locations", icon: MapPin, permission: "MasterData.Locations.View" },
    ],
  },
  {
    titleKey: "partners",
    links: [
      { href: "/master-data/partners", labelKey: "partners", icon: Users, permission: "MasterData.Partners.View" },
      { href: "/master-data/reasons", labelKey: "reasons", icon: Tag, permission: "MasterData.Reasons.View" },
      { href: "/admin/rma", labelKey: "rma", icon: RefreshCw, permission: "rma.read" },
    ],
  },
  {
    titleKey: "utilities",
    links: [
      { href: "/master-data/import", labelKey: "import", icon: Upload, permission: "MasterData.Imports.Preview" },
    ],
  },
  {
    titleKey: "inbound",
    links: [
      { href: "/admin/inbound", labelKey: "inbound", icon: ClipboardList, permission: "Inbound.Orders.View" },
      { href: "/admin/lots", labelKey: "lots", icon: Archive, permission: "Inbound.Lots.View" },
      { href: "/admin/qc", labelKey: "qc", icon: CheckSquare, permission: "Qc.Queue.View" },
      { href: "/admin/putaway", labelKey: "putaway", icon: MapPin, permission: "putaway_slotting.read" },
    ],
  },
  {
    titleKey: "outbound",
    links: [
      { href: "/admin/outbound", labelKey: "outbound", icon: Truck, permission: "Outbound.Shipments.View" },
      { href: "/admin/allocation", labelKey: "allocation", icon: Layers, permission: "allocation_reservation.read" },
      { href: "/admin/waves", labelKey: "waves", icon: Layers, permission: "Wave.Manage" },
      { href: "/admin/cross-docking", labelKey: "crossDocking", icon: Zap, permission: "cross_docking.read" },
    ],
  },
  {
    titleKey: "inventory",
    links: [
      { href: "/admin/inventory", labelKey: "inventory", icon: Box, permission: "Inventory.Balances.View" },
      { href: "/admin/inventory/stocktakes", labelKey: "stocktakes", icon: ClipboardCheck, permission: "Inventory.CycleCount.View" },
      { href: "/admin/exceptions", labelKey: "exceptions", icon: AlertCircle, permission: "exception_framework_mvp.read" },
      { href: "/admin/replenishment", labelKey: "replenishment", icon: RefreshCw, permission: "replenishment.read" },
      { href: "/admin/lpn", labelKey: "lpn", icon: Layers, permission: "lpn.read" },
      { href: "/admin/serial", labelKey: "serial", icon: ClipboardList, permission: "serial.read" },
      { href: "/admin/genealogy", labelKey: "genealogy", icon: GitFork, permission: "material_genealogy.read" },
      { href: "/admin/labor", labelKey: "labor", icon: BarChart3, permission: "labor_tracking.read" },
      { href: "/admin/labor/sessions", labelKey: "laborSessions", icon: Clock, permission: "labor_tracking.read" },
      { href: "/admin/task-interleaving", labelKey: "taskInterleaving", icon: Layers, permission: "task_interleaving.read" },
    ],
  },
  {
    titleKey: "integration",
    links: [
      { href: "/admin/integrations/messages", labelKey: "integrationMessages", icon: FileText, permission: "integration.view" },
      { href: "/admin/integrations/mappings", labelKey: "integrationMappings", icon: GitFork, permission: "integration.view" },
      { href: "/admin/integrations/import", labelKey: "integrationImport", icon: Upload, permission: "integration.import" },
      { href: "/admin/webhooks/subscriptions", labelKey: "webhookSubscriptions", icon: Layers, permission: "webhook.manage" },
      { href: "/admin/webhooks/deliveries", labelKey: "webhookDeliveries", icon: ClipboardList, permission: "webhook.manage" },
    ],
  },
  {
    titleKey: "system",
    links: [
      { href: "/admin/users", labelKey: "users", icon: Shield, permission: "Identity.Users.View" },
      { href: "/admin/roles", labelKey: "roles", icon: Lock, permission: "Identity.Roles.View" },
      { href: "/admin/rules", labelKey: "rules", icon: Sliders, permission: "rule_engine_foundation.read" },
      { href: "/admin/audit", labelKey: "audit", icon: FileText, permission: "Identity.Audit.View" },
      { href: "/admin/local-agent", labelKey: "localAgent", icon: Monitor, permission: "local_agent.view" },
      { href: "/admin/observability", labelKey: "observability", icon: Activity, permission: "observability.read" },
      { href: "/admin/observability/alerts", labelKey: "alerts", icon: AlertCircle, permission: "observability.read" },
      { href: "/admin/observability/timeline", labelKey: "timeline", icon: ClipboardList, permission: "observability.read" },
      { href: "/admin/readiness", labelKey: "readiness", icon: ShieldCheck, permission: "readiness.read" },
      { href: "/admin/cutover", labelKey: "cutover", icon: GitBranch, permission: "readiness.read" },
    ],
  },
];

type NavGroup = {
  titleKey: string;
  title: string;
  links: Array<LinkItem & { label: string }>;
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

  const navGroups: NavGroup[] = useMemo(
    () =>
      navGroupDefs.map((g) => ({
        titleKey: g.titleKey,
        title: t(`groups.${g.titleKey}`),
        links: g.links.map((link) => ({
          ...link,
          label: t(`links.${link.labelKey}`),
        })),
      })),
    [t]
  );

  const [collapsed, setCollapsed] = useState<Record<string, boolean>>(() => {
    const saved = loadCollapsed();
    const initial: Record<string, boolean> = {};
    navGroupDefs.forEach((g) => {
      if (g.titleKey in saved) {
        initial[g.titleKey] = saved[g.titleKey];
      } else {
        initial[g.titleKey] = true;
      }
    });
    return initial;
  });

  useEffect(() => {
    queueMicrotask(() => {
      const saved = loadCollapsed();
      const initial: Record<string, boolean> = {};
      navGroups.forEach((g) => {
        if (g.titleKey in saved) {
          initial[g.titleKey] = saved[g.titleKey];
        } else {
          initial[g.titleKey] = !isGroupActive(g, pathname, permissions);
        }
      });
      setCollapsed(initial);
    });
  }, [permissions, pathname, navGroups]);

  const toggle = (titleKey: string) => {
    setCollapsed((prev) => {
      const next = { ...prev, [titleKey]: !prev[titleKey] };
      saveCollapsed(next);
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
    <aside className="w-60 border-r border-zinc-800/80 bg-[#0a0a0a] p-4 flex-shrink-0 flex flex-col min-h-screen">
      <Link href="/" className="flex items-center gap-2 mb-6 px-1 pt-1">
        <span className="text-lg font-bold text-white tracking-tight">Nexustock</span>
        <span className="text-[10px] text-emerald-400 bg-emerald-400/10 px-1.5 py-0.5 rounded font-semibold uppercase tracking-wider">
          WMS
        </span>
      </Link>

      <div className="flex-1 flex flex-col gap-1 overflow-y-auto pr-1">
        {filteredGroups.map((group) => {
          const active = isGroupActive(group, pathname, permissions);
          const isOpen = !collapsed[group.titleKey];

          return (
            <div key={group.titleKey} className="mb-1">
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
                  isOpen ? "max-h-96 opacity-100 mt-1" : "max-h-0 opacity-0"
                )}
              >
                <nav className="flex flex-col gap-1 pl-1">
                  {group.links.map((link) => {
                    const isActive =
                      pathname === link.href ||
                      (link.href !== "/" && pathname.startsWith(link.href));
                    return (
                      <Link
                        key={link.href}
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
