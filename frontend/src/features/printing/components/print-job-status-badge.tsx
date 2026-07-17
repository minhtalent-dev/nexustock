import { Badge } from "@/components/ui/badge";
import type { PrintJobStatus } from "../types";

const statusLabels: Record<PrintJobStatus, string> = {
  queued: "Queued",
  sending: "Sending",
  printed: "Printed",
  failed: "Failed",
  cancelled: "Cancelled",
};

const statusVariants: Record<PrintJobStatus, "default" | "secondary" | "destructive" | "outline"> = {
  queued: "secondary",
  sending: "outline",
  printed: "default",
  failed: "destructive",
  cancelled: "outline",
};

export function PrintJobStatusBadge({ status }: { status: PrintJobStatus }) {
  return <Badge variant={statusVariants[status] ?? "secondary"}>{statusLabels[status] ?? status}</Badge>;
}
