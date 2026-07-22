# Function Index — Phase 35 Admin Nav Ops ↔ Modules Lens

> Index chức năng để executor triển khai không bỏ sót.  
> **`rp2` 2026-07-22:** Runtime reindex + icon map + MUST NOT + parity machine-readable.  
> Nguồn disk: `frontend/src/components/app-sidebar.tsx` + phase_35 §21–§22.

## A. Registry links (id ổn định)

| id | href | labelKey | permission |
|---|---|---|---|
| home | `/` | home | — |
| healthUi | `/health-ui` | healthUi | — |
| products | `/master-data/products` | products | MasterData.Products.View |
| uoms | `/master-data/uoms` | uoms | MasterData.Uoms.View |
| warehouses | `/master-data/warehouses` | warehouses | MasterData.Warehouses.View |
| zones | `/master-data/zones` | zones | MasterData.Zones.View |
| locations | `/master-data/locations` | locations | MasterData.Locations.View |
| partners | `/master-data/partners` | partners | MasterData.Partners.View |
| reasons | `/master-data/reasons` | reasons | MasterData.Reasons.View |
| import | `/master-data/import` | import | MasterData.Imports.Preview |
| inbound | `/admin/inbound` | inbound | Inbound.Orders.View |
| lots | `/admin/lots` | lots | Inbound.Lots.View |
| qc | `/admin/qc` | qc | Qc.Queue.View |
| putaway | `/admin/putaway` | putaway | putaway_slotting.read |
| outbound | `/admin/outbound` | outbound | Outbound.Shipments.View |
| allocation | `/admin/allocation` | allocation | allocation_reservation.read |
| waves | `/admin/waves` | waves | Wave.Manage |
| crossDocking | `/admin/cross-docking` | crossDocking | cross_docking.read |
| rma | `/admin/rma` | rma | rma.read |
| inventory | `/admin/inventory` | inventory | Inventory.Balances.View |
| stocktakes | `/admin/inventory/stocktakes` | stocktakes | Inventory.CycleCount.View |
| exceptions | `/admin/exceptions` | exceptions | exception_framework_mvp.read |
| replenishment | `/admin/replenishment` | replenishment | replenishment.read |
| lpn | `/admin/lpn` | lpn | lpn.read |
| serial | `/admin/serial` | serial | serial.read |
| genealogy | `/admin/genealogy` | genealogy | material_genealogy.read |
| labor | `/admin/labor` | labor | labor_tracking.read |
| laborSessions | `/admin/labor/sessions` | laborSessions | labor_tracking.read |
| taskInterleaving | `/admin/task-interleaving` | taskInterleaving | task_interleaving.read |
| integrationMessages | `/admin/integrations/messages` | integrationMessages | integration.view |
| integrationMappings | `/admin/integrations/mappings` | integrationMappings | integration.view |
| integrationImport | `/admin/integrations/import` | integrationImport | integration.import |
| webhookSubscriptions | `/admin/webhooks/subscriptions` | webhookSubscriptions | webhook.manage |
| webhookDeliveries | `/admin/webhooks/deliveries` | webhookDeliveries | webhook.manage |
| users | `/admin/users` | users | Identity.Users.View |
| roles | `/admin/roles` | roles | Identity.Roles.View |
| rules | `/admin/rules` | rules | rule_engine_foundation.read |
| audit | `/admin/audit` | audit | Identity.Audit.View |
| localAgent | `/admin/local-agent` | localAgent | local_agent.view |
| observability | `/admin/observability` | observability | observability.read |
| alerts | `/admin/observability/alerts` | alerts | observability.read |
| timeline | `/admin/observability/timeline` | timeline | observability.read |
| readiness | `/admin/readiness` | readiness | readiness.read |
| cutover | `/admin/cutover` | cutover | readiness.read |

**Count = 44.**

## A2. Icon map (lucide — copy từ disk sidebar)

| id | icon |
|---|---|
| home | Home |
| healthUi | Activity |
| products | Package |
| uoms | Ruler |
| warehouses | Warehouse |
| zones | Grid3X3 |
| locations | MapPin |
| partners | Users |
| reasons | Tag |
| import | Upload |
| inbound | ClipboardList |
| lots | Archive |
| qc | CheckSquare |
| putaway | MapPin |
| outbound | Truck |
| allocation | Layers |
| waves | Layers |
| crossDocking | Zap |
| rma | RefreshCw |
| inventory | Box |
| stocktakes | ClipboardCheck |
| exceptions | AlertCircle |
| replenishment | RefreshCw |
| lpn | Layers |
| serial | ClipboardList |
| genealogy | GitFork |
| labor | BarChart3 |
| laborSessions | Clock |
| taskInterleaving | Layers |
| integrationMessages | FileText |
| integrationMappings | GitFork |
| integrationImport | Upload |
| webhookSubscriptions | Layers |
| webhookDeliveries | ClipboardList |
| users | Shield |
| roles | Lock |
| rules | Sliders |
| audit | FileText |
| localAgent | Monitor |
| observability | Activity |
| alerts | AlertCircle |
| timeline | ClipboardList |
| readiness | ShieldCheck |
| cutover | GitBranch |

## B. Modules map (polish A)

| groupKey | link ids |
|---|---|
| overview | home, healthUi |
| materials | products, uoms, import |
| warehouse | warehouses, zones, locations |
| partners | partners, reasons |
| inbound | inbound, lots, qc, putaway |
| outbound | outbound, allocation, waves, crossDocking, **rma** |
| inventory | inventory, stocktakes, exceptions, replenishment, lpn, serial, genealogy |
| labor | labor, laborSessions, taskInterleaving |
| integration | integrationMessages, integrationMappings, integrationImport, webhookSubscriptions, webhookDeliveries |
| system | users, roles, rules, audit, localAgent, observability, alerts, timeline, readiness, cutover |

**Đã bỏ:** `utilities`. **Đã chuyển:** rma ← partners; labor* ← inventory; import ← utilities.

## C. Ops map

| groupKey | link ids |
|---|---|
| opsInbound | inbound, lots, qc, putaway |
| opsOutbound | outbound, allocation, waves, crossDocking, rma |
| opsInventory | inventory, stocktakes, replenishment, lpn, serial, genealogy, exceptions |
| opsOther | home, healthUi, products, uoms, import, warehouses, zones, locations, partners, reasons, labor, laborSessions, taskInterleaving, integrationMessages, integrationMappings, integrationImport, webhookSubscriptions, webhookDeliveries, users, roles, rules, audit, localAgent, observability, alerts, timeline, readiness, cutover |

**Parity:** Modules flatten = Ops flatten = **44** hrefs. opsOther = **28**.

## C2. Mount surfaces

| Layout | AppSidebar |
|---|---|
| `app/page.tsx` | ✅ |
| `app/admin/layout.tsx` | ✅ |
| `app/master-data/layout.tsx` | ✅ |
| `app/mobile/**` | ❌ OOS |

## D. Runtime flow (target)

```text
AppSidebar mount (client)
  → navMode state = "modules" (SSR-safe)
  → useEffect: navMode = loadNavMode()
  → specs = navMode==="ops" ? OPS_GROUPS : MODULES_GROUPS
  → groups = specs.map(g => ({ titleKey, title: t(groups.*), links: resolveLinks(g.linkIds)+t(links.*) }))
  → filter by permissions
  → render toggle + collapsible groups
  → onToggleMode: saveNavMode + setState
  → collapseKey = `${navMode}:${titleKey}` ↔ localStorage nexustock:sidebar:collapsed
```

| Fn | File | Responsibility |
|---|---|---|
| `NAV_LINKS` / `resolveLinks` | `nav-registry.ts` | SoT 44 links |
| `MODULES_GROUPS` | `nav-groups-modules.ts` | Polish A |
| `OPS_GROUPS` | `nav-groups-ops.ts` | Ops lens |
| `loadNavMode` / `saveNavMode` | `nav-mode.ts` | Preference |
| `isGroupActive` | `app-sidebar.tsx` | Active (giữ) |
| `collapseKey` | `app-sidebar.tsx` | Prefix mode |

## E. MUST NOT change

| Area | Reason |
|---|---|
| `app/mobile/**` | OOS |
| Backend / seed permissions | OOS |
| 44 href strings | URL parity |
| Permission string values | RBAC |
| Footer LanguageSwitcher / logout | Giữ UX |

## F. Verify surface

| Script | Assert |
|---|---|
| `verify_nav_lens.ps1` | §21.5 + count 44 + R2 parse `linkIds` |
| `verify_i18n.ps1 -Phase 31a` | Sidebar parity (khuyến nghị) |
| `dbm_phase35_nav_browser.mjs` | Toggle + shots + localStorage |

## G. File touch list (execute)

| Action | Path |
|---|---|
| NEW | `frontend/src/components/nav/nav-registry.ts` |
| NEW | `frontend/src/components/nav/nav-groups-modules.ts` |
| NEW | `frontend/src/components/nav/nav-groups-ops.ts` |
| NEW | `frontend/src/components/nav/nav-mode.ts` |
| EDIT | `frontend/src/components/app-sidebar.tsx` |
| EDIT | `frontend/messages/en/Sidebar.json` |
| EDIT | `frontend/messages/vi/Sidebar.json` |
| NEW | `tests/verify_nav_lens.ps1` |
| NEW | `tests/helpers/dbm_phase35_nav_browser.mjs` |

## H. `rp3` locks (2026-07-22)

| Contract | Location |
|---|---|
| MODULES_GROUPS / OPS_GROUPS literal | phase_35 §23.4 |
| Collapse Effect A/B | phase_35 §23.3 |
| Toggle JSX | phase_35 §23.5 |
| `NAV_LINK_COUNT = 44` | nav-registry.ts |
| Residual OOS | active inventory path; role-default Ops |

Brain SoT: `implementation_plan.md` (EP0–EP5 đã refine `rp3`).

## I. `rp4`+`rp5` close (2026-07-22)

| Metric | Value |
|---|---|
| FILE_FAIL | **0** |
| CONTENT_FAIL | **0** |
| verify | verify_nav_lens + i18n 31a PASS (re-run) |
| DBM | 14/14 + `walkthrough-nav-lens.webm` |
| Module DoD | **100%** — phase §26–§27 |

---

**Cập nhật:** 2026-07-22 · JARVIS · Phase 35 · **`rp4`+`rp5` ĐÓNG**
