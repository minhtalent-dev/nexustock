"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import api from "@/lib/api";
import { showError } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";

export type OpsExportType =
  | "INBOUND_ORDERS"
  | "SHIPMENTS"
  | "STOCKTAKES"
  | "RMA"
  | "LOTS"
  | "EXCEPTIONS"
  | "LPNS"
  | "INVENTORY_BALANCES"
  | "WAVES"
  | "PUTAWAY_PROPOSALS"
  | "CROSS_DOCK_CANDIDATES"
  | "REPLENISHMENT_TASKS";

export function OpsExportButtons({ type }: { type: OpsExportType }) {
  const [loadingFormat, setLoadingFormat] = useState<"csv" | "xlsx" | null>(null);

  const download = async (format: "csv" | "xlsx") => {
    if (loadingFormat) return;
    setLoadingFormat(format);
    let url = "";
    try {
      const res = await api.get(`/ops/exports`, {
        params: { type, format },
        responseType: "blob",
      });
      const blob = new Blob([res.data], {
        type:
          format === "xlsx"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv;charset=utf-8;",
      });
      url = URL.createObjectURL(blob);
      const disposition = res.headers["content-disposition"] as string | undefined;
      const encodedName = disposition?.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
      const quotedName = disposition?.match(/filename="([^"]+)"/i)?.[1];
      const fileName = encodedName
        ? decodeURIComponent(encodedName)
        : quotedName ?? `${type.toLowerCase()}.${format}`;
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Export failed"));
    } finally {
      if (url) {
        URL.revokeObjectURL(url);
      }
      setLoadingFormat(null);
    }
  };

  return (
    <div className="flex gap-2">
      <Button
        id={`ops-export-${type.toLowerCase()}-csv`}
        type="button"
        variant="outline"
        size="sm"
        disabled={loadingFormat !== null}
        onClick={() => void download("csv")}
      >
        {loadingFormat === "csv" ? "Exporting..." : "Export CSV"}
      </Button>
      <Button
        id={`ops-export-${type.toLowerCase()}-xlsx`}
        type="button"
        variant="outline"
        size="sm"
        disabled={loadingFormat !== null}
        onClick={() => void download("xlsx")}
      >
        {loadingFormat === "xlsx" ? "Exporting..." : "Export Excel"}
      </Button>
    </div>
  );
}
