"use client";

import { useTranslations } from "next-intl";
import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import { MasterDataExportButtons } from "@/features/master-data/export-buttons";
import type { Uom } from "@/types/master-data";

type UomForm = {
  code: string;
  name: string;
  isActive: boolean;
};

const defaultForm: UomForm = { code: "", name: "", isActive: true };

export default function UomsPage() {
  const t = useTranslations("MasterData.uoms");
  const ts = useTranslations("MasterData.common");

  const fields: CrudField<UomForm>[] = [
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "name", label: t("fields.name.label"), type: "text", required: true, placeholder: t("fields.name.placeholder") },
    { name: "isActive", label: t("fields.isActive.label"), type: "checkbox" },
  ];

  return (
    <div className="space-y-3">
      <div className="flex justify-end px-1">
        <MasterDataExportButtons type="UOMS" />
      </div>
      <MasterDataCrudPage<Uom, UomForm>
        title={t("page.title")}
        endpoint="/master-data/uoms"
        searchPlaceholder={t("page.searchPlaceholder")}
        defaultForm={defaultForm}
        fields={fields}
        toForm={(item) => ({ code: item.code, name: item.name, isActive: item.isActive })}
        columns={[
          { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono">{item.code}</span> },
          { key: "name", label: t("columns.name"), render: (item) => item.name },
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
