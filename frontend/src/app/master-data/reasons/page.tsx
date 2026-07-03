"use client";

import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
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

const fields: CrudField<ReasonForm>[] = [
  { name: "code", label: "Mã", type: "text", required: true, placeholder: "RCV-DAMAGED" },
  { name: "reasonType", label: "Loại lý do", type: "text", required: true, placeholder: "ADJUSTMENT" },
  { name: "description", label: "Mô tả", type: "text", required: true, placeholder: "Lý do sai lệch hàng tồn" },
  { name: "isActive", label: "Hoạt động", type: "checkbox" },
];

export default function ReasonsPage() {
  return (
    <MasterDataCrudPage<ReasonCode, ReasonForm>
      title="Mã lý do"
      endpoint="/master-data/reason-codes"
      searchPlaceholder="Tìm kiếm mã, loại lý do..."
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({
        code: item.code,
        reasonType: item.reasonType,
        description: item.description,
        isActive: item.isActive,
      })}
      columns={[
        { key: "code", label: "Mã", render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "reasonType", label: "Loại lý do", render: (item) => item.reasonType },
        { key: "description", label: "Mô tả", render: (item) => item.description },
        { key: "status", label: "Trạng thái", render: (item) => <span className={item.isActive ? "text-green-400" : "text-red-400"}>{item.isActive ? "Hoạt động" : "Vô hiệu"}</span> },
      ]}
    />
  );
}
