// SoT: phase_35 §23.4 OPS_GROUPS

import type { NavGroupSpec } from "./nav-registry";

export const OPS_GROUPS: NavGroupSpec[] = [
  { titleKey: "opsInbound", linkIds: ["inbound", "lots", "qc", "putaway"] },
  { titleKey: "opsOutbound", linkIds: ["outbound", "allocation", "waves", "crossDocking", "rma"] },
  { titleKey: "opsInventory", linkIds: ["inventory", "stocktakes", "replenishment", "lpn", "serial", "genealogy", "exceptions"] },
  { titleKey: "opsOther", linkIds: ["home", "healthUi", "products", "uoms", "import", "warehouses", "zones", "locations", "partners", "reasons", "labor", "laborSessions", "taskInterleaving", "integrationMessages", "integrationMappings", "integrationImport", "webhookSubscriptions", "webhookDeliveries", "users", "roles", "rules", "audit", "localAgent", "observability", "alerts", "timeline", "readiness", "cutover"] },
];
