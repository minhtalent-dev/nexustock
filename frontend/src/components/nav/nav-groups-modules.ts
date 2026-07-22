// SoT: phase_35 §23.4 MODULES_GROUPS — polish A

import type { NavGroupSpec } from "./nav-registry";

export const MODULES_GROUPS: NavGroupSpec[] = [
  { titleKey: "overview", linkIds: ["home", "healthUi"] },
  { titleKey: "materials", linkIds: ["products", "uoms", "import"] },
  { titleKey: "warehouse", linkIds: ["warehouses", "zones", "locations"] },
  { titleKey: "partners", linkIds: ["partners", "reasons"] },
  { titleKey: "inbound", linkIds: ["inbound", "lots", "qc", "putaway"] },
  { titleKey: "outbound", linkIds: ["outbound", "allocation", "waves", "crossDocking", "rma"] },
  { titleKey: "inventory", linkIds: ["inventory", "stocktakes", "exceptions", "replenishment", "lpn", "serial", "genealogy"] },
  { titleKey: "labor", linkIds: ["labor", "laborSessions", "taskInterleaving"] },
  { titleKey: "integration", linkIds: ["integrationMessages", "integrationMappings", "integrationImport", "webhookSubscriptions", "webhookDeliveries"] },
  { titleKey: "system", linkIds: ["users", "roles", "rules", "audit", "localAgent", "observability", "alerts", "timeline", "readiness", "cutover"] },
];
