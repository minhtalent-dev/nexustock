"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import clsx from "clsx";
import { useState, useEffect } from "react";
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
} from "lucide-react";
import { Button } from "@/components/ui/button";

type LinkItem = {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  permission?: string;
};

type NavGroup = {
  title: string;
  links: LinkItem[];
};

const navGroups: NavGroup[] = [
  {
    title: "Tổng quan",
    links: [
      { href: "/", label: "Trang chủ", icon: Home },
      { href: "/health-ui", label: "Sức khỏe hệ thống", icon: Activity },
    ],
  },
  {
    title: "Vật tư & Đơn vị",
    links: [
      { href: "/master-data/products", label: "Vật tư", icon: Package, permission: "MasterData.Products.View" },
      { href: "/master-data/uoms", label: "Đơn vị tính", icon: Ruler, permission: "MasterData.Uoms.View" },
    ],
  },
  {
    title: "Kho bãi & Kệ",
    links: [
      { href: "/master-data/warehouses", label: "Nhà kho", icon: Warehouse, permission: "MasterData.Warehouses.View" },
      { href: "/master-data/zones", label: "Vùng kho", icon: Grid3X3, permission: "MasterData.Zones.View" },
      { href: "/master-data/locations", label: "Vị trí kệ", icon: MapPin, permission: "MasterData.Locations.View" },
    ],
  },
  {
    title: "Đối tác & Nghiệp vụ",
    links: [
      { href: "/master-data/partners", label: "Đối tác", icon: Users, permission: "MasterData.Partners.View" },
      { href: "/master-data/reasons", label: "Mã lý do", icon: Tag, permission: "MasterData.Reasons.View" },
      { href: "/admin/rma", label: "Trả hàng (RMA)", icon: RefreshCw, permission: "rma.read" },
    ],
  },
  {
    title: "Tiện ích",
    links: [
      { href: "/master-data/import", label: "Nhập dữ liệu", icon: Upload, permission: "MasterData.Imports.Preview" },
    ],
  },
  {
    title: "Nhập kho",
    links: [
      { href: "/admin/inbound", label: "Phiếu nhập hàng", icon: ClipboardList, permission: "Inbound.Orders.View" },
      { href: "/admin/lots", label: "Tra cứu lô hàng", icon: Archive, permission: "Inbound.Lots.View" },
      { href: "/admin/qc", label: "Kiểm định chất lượng", icon: CheckSquare, permission: "Qc.Queue.View" },
      { href: "/admin/putaway", label: "Cất hàng tự động", icon: MapPin, permission: "putaway_slotting.read" },
    ],
  },
  {
    title: "Xuất kho",
    links: [
      { href: "/admin/outbound", label: "Đơn xuất kho", icon: Truck, permission: "Outbound.Shipments.View" },
      { href: "/admin/allocation", label: "Phân bổ giữ hàng", icon: Layers, permission: "allocation_reservation.read" },
      { href: "/admin/waves", label: "Lấy hàng Wave", icon: Layers, permission: "Wave.Manage" },
    ],
  },
  {
    title: "Tồn kho",
    links: [
      { href: "/admin/inventory", label: "Hàng tồn kho", icon: Box, permission: "Inventory.Balances.View" },
      { href: "/admin/inventory/stocktakes", label: "Kiểm kê chu kỳ", icon: ClipboardCheck, permission: "Inventory.CycleCount.View" },
      { href: "/admin/exceptions", label: "Sự cố vận hành", icon: AlertCircle, permission: "exception_framework_mvp.read" },
      { href: "/admin/replenishment", label: "Bổ sung hàng", icon: RefreshCw, permission: "replenishment.read" },
      { href: "/admin/lpn", label: "Quản lý Pallet (LPN)", icon: Layers, permission: "lpn.read" },
      { href: "/admin/serial", label: "Truy vết mã Serial", icon: ClipboardList, permission: "serial.read" },
      { href: "/admin/genealogy", label: "Phả hệ vật tư", icon: GitFork, permission: "material_genealogy.read" },
    ],
  },
  {
    title: "Tích hợp ERP",
    links: [
      { href: "/admin/integrations/messages", label: "Nhật ký tích hợp", icon: FileText, permission: "integration.view" },
      { href: "/admin/integrations/mappings", label: "Ánh xạ dữ liệu", icon: GitFork, permission: "integration.view" },
      { href: "/admin/integrations/import", label: "Import tích hợp", icon: Upload, permission: "integration.import" },
      { href: "/admin/webhooks/subscriptions", label: "Webhook Subscriptions", icon: Layers, permission: "webhook.manage" },
      { href: "/admin/webhooks/deliveries", label: "Webhook Deliveries", icon: ClipboardList, permission: "webhook.manage" },
    ],
  },
  {
    title: "Hệ thống & Quyền",
    links: [
      { href: "/admin/users", label: "Người dùng", icon: Shield, permission: "Identity.Users.View" },
      { href: "/admin/roles", label: "Vai trò & Quyền", icon: Lock, permission: "Identity.Roles.View" },
      { href: "/admin/rules", label: "Cấu hình luật", icon: Sliders, permission: "rule_engine_foundation.read" },
      { href: "/admin/audit", label: "Nhật ký hệ thống", icon: FileText, permission: "Identity.Audit.View" },
      { href: "/admin/local-agent", label: "Local Agent", icon: Monitor, permission: "local_agent.view" },
      { href: "/admin/observability", label: "Giám sát vận hành", icon: Activity, permission: "observability.read" },
      { href: "/admin/observability/alerts", label: "Trung tâm cảnh báo", icon: AlertCircle, permission: "observability.read" },
      { href: "/admin/observability/timeline", label: "Dòng thời gian hoạt động", icon: ClipboardList, permission: "observability.read" },
    ],
  },
];

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
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>(() => {
    const saved = loadCollapsed();
    const initial: Record<string, boolean> = {};
    navGroups.forEach((g) => {
      if (g.title in saved) {
        initial[g.title] = saved[g.title];
      } else {
        initial[g.title] = true; // Safe fallback, defer active expansion to useEffect
      }
    });
    return initial;
  });

  useEffect(() => {
    queueMicrotask(() => {
      const saved = loadCollapsed();
      const initial: Record<string, boolean> = {};
      navGroups.forEach((g) => {
        if (g.title in saved) {
          initial[g.title] = saved[g.title];
        } else {
          initial[g.title] = !isGroupActive(g, pathname, permissions);
        }
      });
      setCollapsed(initial);
    });
  }, [permissions, pathname]);

  const toggle = (title: string) => {
    setCollapsed((prev) => {
      const next = { ...prev, [title]: !prev[title] };
      saveCollapsed(next);
      return next;
    });
  };

  // Lọc các group và link dựa trên permissions của user
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
      {/* Logo / Brand */}
      <Link href="/" className="flex items-center gap-2 mb-6 px-1 pt-1">
        <span className="text-lg font-bold text-white tracking-tight">
          Nexustock
        </span>
        <span className="text-[10px] text-emerald-400 bg-emerald-400/10 px-1.5 py-0.5 rounded font-semibold uppercase tracking-wider">
          WMS
        </span>
      </Link>

      {/* Navigation groups */}
      <div className="flex-1 flex flex-col gap-1 overflow-y-auto pr-1">
        {filteredGroups.map((group) => {
          const active = isGroupActive(group, pathname, permissions);
          const isOpen = !collapsed[group.title];

          return (
            <div key={group.title} className="mb-1">
              {/* Group header — click toggle */}
              <Button
                onClick={() => toggle(group.title)}
                variant="ghost"
                size="sm"
                className={clsx(
                  "w-full justify-between px-1 text-xs font-semibold uppercase tracking-wider h-8 hover:bg-transparent",
                  active
                    ? "text-emerald-400"
                    : "text-zinc-500 hover:text-zinc-300"
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

              {/* Collapsible submenu */}
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

      {/* User Info & Logout Button */}
      {user && (
        <div className="mt-auto pt-4 border-t border-zinc-800/60 flex flex-col gap-2">
          <div className="flex flex-col px-2">
            <span className="text-sm font-medium text-white truncate">{user.fullName}</span>
            <span className="text-[10px] text-zinc-500 truncate font-mono">{user.email}</span>
          </div>
          <Button
            onClick={logout}
            variant="ghost"
            size="sm"
            className="w-full justify-start text-red-400 hover:text-red-300 hover:bg-red-500/10 gap-3 px-2 h-9"
          >
            <LogOut className="h-4 w-4 flex-shrink-0" />
            Đăng xuất
          </Button>
        </div>
      )}

      {/* Footer */}
      <div className="mt-2 text-[10px] text-zinc-600 font-mono text-center">
        <span>v0.19.0 (Phase 19 Genealogy)</span>
      </div>
    </aside>
  );
}
