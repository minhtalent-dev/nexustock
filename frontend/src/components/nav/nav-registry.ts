// @nav-registry-count 44
// SoT: planning/function_index_phase35_admin_nav_lens.md §A + §A2

import type { LucideIcon } from "lucide-react";
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
  Shield,
  Lock,
  FileText,
  ShieldCheck,
  GitBranch,
} from "lucide-react";

export type NavLinkDef = {
  id: string;
  href: string;
  labelKey: string;
  icon: LucideIcon;
  permission?: string;
};

export type NavGroupSpec = {
  titleKey: string;
  linkIds: string[];
};

/** Số link cố định Phase 35 — verify_nav_lens assert. */
export const NAV_LINK_COUNT = 44;

export const NAV_LINKS: NavLinkDef[] = [
  { id: "home", href: "/", labelKey: "home", icon: Home },
  { id: "healthUi", href: "/health-ui", labelKey: "healthUi", icon: Activity },
  { id: "products", href: "/master-data/products", labelKey: "products", icon: Package, permission: "MasterData.Products.View" },
  { id: "uoms", href: "/master-data/uoms", labelKey: "uoms", icon: Ruler, permission: "MasterData.Uoms.View" },
  { id: "warehouses", href: "/master-data/warehouses", labelKey: "warehouses", icon: Warehouse, permission: "MasterData.Warehouses.View" },
  { id: "zones", href: "/master-data/zones", labelKey: "zones", icon: Grid3X3, permission: "MasterData.Zones.View" },
  { id: "locations", href: "/master-data/locations", labelKey: "locations", icon: MapPin, permission: "MasterData.Locations.View" },
  { id: "partners", href: "/master-data/partners", labelKey: "partners", icon: Users, permission: "MasterData.Partners.View" },
  { id: "reasons", href: "/master-data/reasons", labelKey: "reasons", icon: Tag, permission: "MasterData.Reasons.View" },
  { id: "import", href: "/master-data/import", labelKey: "import", icon: Upload, permission: "MasterData.Imports.Preview" },
  { id: "inbound", href: "/admin/inbound", labelKey: "inbound", icon: ClipboardList, permission: "Inbound.Orders.View" },
  { id: "lots", href: "/admin/lots", labelKey: "lots", icon: Archive, permission: "Inbound.Lots.View" },
  { id: "qc", href: "/admin/qc", labelKey: "qc", icon: CheckSquare, permission: "Qc.Queue.View" },
  { id: "putaway", href: "/admin/putaway", labelKey: "putaway", icon: MapPin, permission: "putaway_slotting.read" },
  { id: "outbound", href: "/admin/outbound", labelKey: "outbound", icon: Truck, permission: "Outbound.Shipments.View" },
  { id: "allocation", href: "/admin/allocation", labelKey: "allocation", icon: Layers, permission: "allocation_reservation.read" },
  { id: "waves", href: "/admin/waves", labelKey: "waves", icon: Layers, permission: "Wave.Manage" },
  { id: "crossDocking", href: "/admin/cross-docking", labelKey: "crossDocking", icon: Zap, permission: "cross_docking.read" },
  { id: "rma", href: "/admin/rma", labelKey: "rma", icon: RefreshCw, permission: "rma.read" },
  { id: "inventory", href: "/admin/inventory", labelKey: "inventory", icon: Box, permission: "Inventory.Balances.View" },
  { id: "stocktakes", href: "/admin/inventory/stocktakes", labelKey: "stocktakes", icon: ClipboardCheck, permission: "Inventory.CycleCount.View" },
  { id: "exceptions", href: "/admin/exceptions", labelKey: "exceptions", icon: AlertCircle, permission: "exception_framework_mvp.read" },
  { id: "replenishment", href: "/admin/replenishment", labelKey: "replenishment", icon: RefreshCw, permission: "replenishment.read" },
  { id: "lpn", href: "/admin/lpn", labelKey: "lpn", icon: Layers, permission: "lpn.read" },
  { id: "serial", href: "/admin/serial", labelKey: "serial", icon: ClipboardList, permission: "serial.read" },
  { id: "genealogy", href: "/admin/genealogy", labelKey: "genealogy", icon: GitFork, permission: "material_genealogy.read" },
  { id: "labor", href: "/admin/labor", labelKey: "labor", icon: BarChart3, permission: "labor_tracking.read" },
  { id: "laborSessions", href: "/admin/labor/sessions", labelKey: "laborSessions", icon: Clock, permission: "labor_tracking.read" },
  { id: "taskInterleaving", href: "/admin/task-interleaving", labelKey: "taskInterleaving", icon: Layers, permission: "task_interleaving.read" },
  { id: "integrationMessages", href: "/admin/integrations/messages", labelKey: "integrationMessages", icon: FileText, permission: "integration.view" },
  { id: "integrationMappings", href: "/admin/integrations/mappings", labelKey: "integrationMappings", icon: GitFork, permission: "integration.view" },
  { id: "integrationImport", href: "/admin/integrations/import", labelKey: "integrationImport", icon: Upload, permission: "integration.import" },
  { id: "webhookSubscriptions", href: "/admin/webhooks/subscriptions", labelKey: "webhookSubscriptions", icon: Layers, permission: "webhook.manage" },
  { id: "webhookDeliveries", href: "/admin/webhooks/deliveries", labelKey: "webhookDeliveries", icon: ClipboardList, permission: "webhook.manage" },
  { id: "users", href: "/admin/users", labelKey: "users", icon: Shield, permission: "Identity.Users.View" },
  { id: "roles", href: "/admin/roles", labelKey: "roles", icon: Lock, permission: "Identity.Roles.View" },
  { id: "rules", href: "/admin/rules", labelKey: "rules", icon: Sliders, permission: "rule_engine_foundation.read" },
  { id: "audit", href: "/admin/audit", labelKey: "audit", icon: FileText, permission: "Identity.Audit.View" },
  { id: "localAgent", href: "/admin/local-agent", labelKey: "localAgent", icon: Monitor, permission: "local_agent.view" },
  { id: "observability", href: "/admin/observability", labelKey: "observability", icon: Activity, permission: "observability.read" },
  { id: "alerts", href: "/admin/observability/alerts", labelKey: "alerts", icon: AlertCircle, permission: "observability.read" },
  { id: "timeline", href: "/admin/observability/timeline", labelKey: "timeline", icon: ClipboardList, permission: "observability.read" },
  { id: "readiness", href: "/admin/readiness", labelKey: "readiness", icon: ShieldCheck, permission: "readiness.read" },
  { id: "cutover", href: "/admin/cutover", labelKey: "cutover", icon: GitBranch, permission: "readiness.read" },
];

export const NAV_LINKS_BY_ID: Record<string, NavLinkDef> = Object.fromEntries(
  NAV_LINKS.map((link) => [link.id, link])
);

/** Resolve danh sách id → NavLinkDef; thiếu id thì throw (fail-fast). */
export function resolveLinks(ids: string[]): NavLinkDef[] {
  return ids.map((id) => {
    const link = NAV_LINKS_BY_ID[id];
    if (!link) {
      throw new Error(`Nav registry thiếu id: ${id}`);
    }
    return link;
  });
}
