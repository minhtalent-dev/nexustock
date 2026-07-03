"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import clsx from "clsx";
import { useState, useEffect } from "react";
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
} from "lucide-react";
import { Button } from "@/components/ui/button";

type NavGroup = {
  title: string;
  links: { href: string; label: string; icon: React.ComponentType<{ className?: string }> }[];
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
      { href: "/master-data/products", label: "Vật tư", icon: Package },
      { href: "/master-data/uoms", label: "Đơn vị tính", icon: Ruler },
    ],
  },
  {
    title: "Kho bãi & Kệ",
    links: [
      { href: "/master-data/warehouses", label: "Nhà kho", icon: Warehouse },
      { href: "/master-data/zones", label: "Vùng kho", icon: Grid3X3 },
      { href: "/master-data/locations", label: "Vị trí kệ", icon: MapPin },
    ],
  },
  {
    title: "Đối tác & Nghiệp vụ",
    links: [
      { href: "/master-data/partners", label: "Đối tác", icon: Users },
      { href: "/master-data/reasons", label: "Mã lý do", icon: Tag },
    ],
  },
  {
    title: "Tiện ích",
    links: [
      { href: "/master-data/import", label: "Nhập dữ liệu", icon: Upload },
    ],
  },
];

function isGroupActive(group: NavGroup, pathname: string): boolean {
  return group.links.some((link) =>
    link.href === "/" ? pathname === "/" : pathname.startsWith(link.href)
  );
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

  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const saved = loadCollapsed();
    const initial: Record<string, boolean> = {};
    navGroups.forEach((g) => {
      if (g.title in saved) {
        initial[g.title] = saved[g.title];
      } else {
        initial[g.title] = !isGroupActive(g, pathname);
      }
    });
    setCollapsed(initial);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const toggle = (title: string) => {
    setCollapsed((prev) => {
      const next = { ...prev, [title]: !prev[title] };
      saveCollapsed(next);
      return next;
    });
  };

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
      {navGroups.map((group) => {
        const active = isGroupActive(group, pathname);
        const isOpen = !collapsed[group.title];

        return (
          <div key={group.title} className="mb-1">
            {/* Group header — click toggle */}
            <Button
              onClick={() => toggle(group.title)}
              variant="ghost"
              size="sm"
              className={clsx(
                "w-full justify-between px-1 text-xs font-semibold uppercase tracking-wider",
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
                "overflow-hidden transition-all duration-250 ease-in-out",
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
                          ? "bg-zinc-800 text-white"
                          : "text-zinc-400 hover:text-white hover:bg-zinc-900"
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

      {/* Footer */}
      <div className="mt-auto pt-4 border-t border-zinc-800/40 text-xs text-zinc-500">
        <span>Phiên bản v0.2.0 (Phase 02)</span>
      </div>
    </aside>
  );
}
