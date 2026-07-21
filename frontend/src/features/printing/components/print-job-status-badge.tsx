"use client";

import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import type { PrintJobStatus } from "../types";

const statusVariants: Record<PrintJobStatus, "default" | "secondary" | "destructive" | "outline"> = {
  queued: "secondary",
  sending: "outline",
  printed: "default",
  failed: "destructive",
  cancelled: "outline",
};

export function PrintJobStatusBadge({ status }: { status: PrintJobStatus }) {
  const t = useTranslations("Features.printing");

  const label = t(`jobStatus.${status}`);

  return <Badge variant={statusVariants[status] ?? "secondary"}>{label}</Badge>;
}
