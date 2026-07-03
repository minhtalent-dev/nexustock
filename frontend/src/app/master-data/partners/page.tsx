"use client";

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

const fields: CrudField<PartnerForm>[] = [
  { name: "code", label: "Mã", type: "text", required: true, placeholder: "VEN-001" },
  { name: "name", label: "Tên", type: "text", required: true, placeholder: "Nhà cung cấp A" },
  {
    name: "partnerType",
    label: "Loại đối tác",
    type: "select",
    required: true,
    options: [
      { value: "VENDOR", label: "VENDOR" },
      { value: "CUSTOMER", label: "CUSTOMER" },
      { value: "CARRIER", label: "CARRIER" },
    ],
  },
  { name: "address", label: "Địa chỉ", type: "text" },
  { name: "taxCode", label: "Mã số thuế", type: "text" },
  { name: "isActive", label: "Hoạt động", type: "checkbox" },
];

export default function PartnersPage() {
  return (
    <MasterDataCrudPage<Partner, PartnerForm>
      title="Đối tác"
      endpoint="/master-data/partners"
      searchPlaceholder="Tìm kiếm mã, tên..."
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
        { key: "code", label: "Mã", render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "name", label: "Tên", render: (item) => item.name },
        { key: "partnerType", label: "Loại", render: (item) => item.partnerType },
        { key: "taxCode", label: "Mã số thuế", render: (item) => <span className="font-mono text-white/50">{item.taxCode || "-"}</span> },
        { key: "status", label: "Trạng thái", render: (item) => <span className={item.isActive ? "text-green-400" : "text-red-400"}>{item.isActive ? "Hoạt động" : "Vô hiệu"}</span> },
      ]}
    />
  );
}
