"use client";

import { useTranslations } from "next-intl";
import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import { MasterDataExportButtons } from "@/features/master-data/export-buttons";
import type { Warehouse } from "@/types/master-data";

type WarehouseForm = {
  code: string;
  name: string;
  description: string;
  isActive: boolean;
};

const defaultForm: WarehouseForm = { code: "", name: "", description: "", isActive: true };

export default function WarehousesPage() {
  const t = useTranslations("MasterData.warehouses");
  const ts = useTranslations("MasterData.common");

  const fields: CrudField<WarehouseForm>[] = [
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "name", label: t("fields.name.label"), type: "text", required: true, placeholder: t("fields.name.placeholder") },
    { name: "description", label: t("fields.description.label"), type: "text" },
    { name: "isActive", label: t("fields.isActive.label"), type: "checkbox" },
  ];

  return (
    <div className="space-y-3">
      <div className="flex justify-end px-1">
        <MasterDataExportButtons type="WAREHOUSES" />
      </div>
      <MasterDataCrudPage<Warehouse, WarehouseForm>
        title={t("page.title")}
        endpoint="/master-data/warehouses"
        searchPlaceholder={t("page.searchPlaceholder")}
        defaultForm={defaultForm}
        fields={fields}
        toForm={(item) => ({ code: item.code, name: item.name, description: item.description || "", isActive: item.isActive })}
        columns={[
          { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono">{item.code}</span> },
          { key: "name", label: t("columns.name"), render: (item) => item.name },
          { key: "description", label: t("columns.description"), render: (item) => <span className="text-foreground/50">{item.description || "-"}</span> },
          {
            key: "status",
            label: t("columns.status"),
            render: (item) => (
              <span className={item.isActive ? "text-green-400" : "text-red-400"}>
                {item.isActive ? ts("status.active") : ts("status.inactive")}
              </span>
            ),
          },
        ]}
      />
    </div>
  );
}
