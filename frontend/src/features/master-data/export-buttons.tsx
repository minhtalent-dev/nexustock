"use client";

import { Button } from "@/components/ui/button";
import api from "@/lib/api";
import { showError } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";

type ExportType = "ITEMS" | "LOCATIONS" | "PARTNERS";

export function MasterDataExportButtons({ type }: { type: ExportType }) {
  const download = async (format: "csv" | "xlsx") => {
    try {
      const res = await api.get(`/exports`, {
        params: { type, format },
        responseType: "blob",
      });
      const blob = new Blob([res.data], {
        type:
          format === "xlsx"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv",
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${type.toLowerCase()}.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, "Export failed"));
    }
  };

  return (
    <div className="flex gap-2">
      <Button type="button" variant="outline" size="sm" onClick={() => void download("csv")}>
        Export CSV
      </Button>
      <Button type="button" variant="outline" size="sm" onClick={() => void download("xlsx")}>
        Export Excel
      </Button>
    </div>
  );
}
