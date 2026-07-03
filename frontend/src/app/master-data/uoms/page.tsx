"use client";

import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import type { Uom } from "@/types/master-data";

type UomForm = {
  code: string;
  name: string;
  isActive: boolean;
};

const defaultForm: UomForm = { code: "", name: "", isActive: true };

const fields: CrudField<UomForm>[] = [
  { name: "code", label: "Mã", type: "text", required: true, placeholder: "PCS" },
  { name: "name", label: "Tên", type: "text", required: true, placeholder: "Piece" },
  { name: "isActive", label: "Hoạt động", type: "checkbox" },
];

export default function UomsPage() {
  return (
    <MasterDataCrudPage<Uom, UomForm>
      title="Đơn vị tính"
      endpoint="/master-data/uoms"
      searchPlaceholder="Tìm kiếm mã, tên..."
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({ code: item.code, name: item.name, isActive: item.isActive })}
      columns={[
        { key: "code", label: "Mã", render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "name", label: "Tên", render: (item) => item.name },
        { key: "status", label: "Trạng thái", render: (item) => <span className={item.isActive ? "text-green-400" : "text-red-400"}>{item.isActive ? "Hoạt động" : "Vô hiệu"}</span> },
      ]}
    />
  );
}
