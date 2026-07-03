"use client";

import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import type { Warehouse } from "@/types/master-data";

type WarehouseForm = {
  code: string;
  name: string;
  description: string;
  isActive: boolean;
};

const defaultForm: WarehouseForm = { code: "", name: "", description: "", isActive: true };

const fields: CrudField<WarehouseForm>[] = [
  { name: "code", label: "Mã", type: "text", required: true, placeholder: "WH-MAIN" },
  { name: "name", label: "Tên", type: "text", required: true, placeholder: "Main warehouse" },
  { name: "description", label: "Mô tả", type: "text" },
  { name: "isActive", label: "Hoạt động", type: "checkbox" },
];

export default function WarehousesPage() {
  return (
    <MasterDataCrudPage<Warehouse, WarehouseForm>
      title="Nhà kho"
      endpoint="/master-data/warehouses"
      searchPlaceholder="Tìm kiếm mã, tên..."
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({ code: item.code, name: item.name, description: item.description || "", isActive: item.isActive })}
      columns={[
        { key: "code", label: "Mã", render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "name", label: "Tên", render: (item) => item.name },
        { key: "description", label: "Mô tả", render: (item) => <span className="text-white/50">{item.description || "-"}</span> },
        { key: "status", label: "Trạng thái", render: (item) => <span className={item.isActive ? "text-green-400" : "text-red-400"}>{item.isActive ? "Hoạt động" : "Vô hiệu"}</span> },
      ]}
    />
  );
}
