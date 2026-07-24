"use client";

import { useTranslations } from "next-intl";
import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import { MasterDataExportButtons } from "@/features/master-data/export-buttons";
import type { ReasonCode } from "@/types/master-data";

type ReasonForm = {
  code: string;
  reasonType: string;
  description: string;
  isActive: boolean;
};

const defaultForm: ReasonForm = {
  code: "",
  reasonType: "ADJUSTMENT",
  description: "",
  isActive: true,
};

export default function ReasonsPage() {
  const t = useTranslations("MasterData.reasons");
  const ts = useTranslations("MasterData.common");

  const fields: CrudField<ReasonForm>[] = [
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "reasonType", label: t("fields.reasonType.label"), type: "text", required: true, placeholder: t("fields.reasonType.placeholder") },
    { name: "description", label: t("fields.description.label"), type: "text", required: true, placeholder: t("fields.description.placeholder") },
    { name: "isActive", label: t("fields.isActive.label"), type: "checkbox" },
  ];

  return (
    <div className="space-y-3">
      <div className="flex justify-end px-1">
        <MasterDataExportButtons type="REASONS" />
      </div>
      <MasterDataCrudPage<ReasonCode, ReasonForm>
        title={t("page.title")}
        endpoint="/master-data/reason-codes"
        searchPlaceholder={t("page.searchPlaceholder")}
        defaultForm={defaultForm}
        fields={fields}
        toForm={(item) => ({
          code: item.code,
          reasonType: item.reasonType,
          description: item.description,
          isActive: item.isActive,
        })}
        columns={[
          { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono">{item.code}</span> },
          { key: "reasonType", label: t("columns.reasonType"), render: (item) => item.reasonType },
          { key: "description", label: t("columns.description"), render: (item) => item.description },
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
