"use client";

import { useTranslations } from "next-intl";
import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import type { Partner } from "@/types/master-data";

type PartnerForm = {
  code: string;
  name: string;
  partnerType: string;
  address: string;
  taxCode: string;
  isActive: boolean;
};

const defaultForm: PartnerForm = {
  code: "",
  name: "",
  partnerType: "VENDOR",
  address: "",
  taxCode: "",
  isActive: true,
};

export default function PartnersPage() {
  const t = useTranslations("MasterData.partners");
  const ts = useTranslations("MasterData.common");

  const fields: CrudField<PartnerForm>[] = [
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "name", label: t("fields.name.label"), type: "text", required: true, placeholder: t("fields.name.placeholder") },
    {
      name: "partnerType",
      label: t("fields.partnerType.label"),
      type: "select",
      required: true,
      options: [
        { value: "VENDOR", label: "VENDOR" },
        { value: "CUSTOMER", label: "CUSTOMER" },
        { value: "CARRIER", label: "CARRIER" },
      ],
    },
    { name: "address", label: t("fields.address.label"), type: "text" },
    { name: "taxCode", label: t("fields.taxCode.label"), type: "text" },
    { name: "isActive", label: t("fields.isActive.label"), type: "checkbox" },
  ];

  return (
    <MasterDataCrudPage<Partner, PartnerForm>
      title={t("page.title")}
      endpoint="/master-data/partners"
      searchPlaceholder={t("page.searchPlaceholder")}
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({
        code: item.code,
        name: item.name,
        partnerType: item.partnerType,
        address: item.address || "",
        taxCode: item.taxCode || "",
        isActive: item.isActive,
      })}
      columns={[
        { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "name", label: t("columns.name"), render: (item) => item.name },
        { key: "partnerType", label: t("columns.partnerType"), render: (item) => item.partnerType },
        { key: "taxCode", label: t("columns.taxCode"), render: (item) => <span className="font-mono text-white/50">{item.taxCode || "-"}</span> },
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
  );
}
