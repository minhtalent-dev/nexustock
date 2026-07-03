"use client";

import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import type { ProductDto } from "@/types/master-data";

type ProductForm = {
  code: string;
  name: string;
  description: string;
  barcode: string;
  baseUomId: string;
  isActive: boolean;
};

const defaultForm: ProductForm = { code: "", name: "", description: "", barcode: "", baseUomId: "", isActive: true };

const fields: CrudField<ProductForm>[] = [
  { name: "code", label: "Mã", type: "text", required: true, placeholder: "PROD-001" },
  { name: "name", label: "Tên", type: "text", required: true, placeholder: "Tên vật tư" },
  { name: "barcode", label: "Barcode", type: "text", placeholder: "Bar-001" },
  { name: "baseUomId", label: "Đơn vị cơ bản", type: "text", required: true, placeholder: "UUID của UOM" },
  { name: "description", label: "Mô tả", type: "text", placeholder: "Mô tả..." },
  { name: "isActive", label: "Hoạt động", type: "checkbox" },
];

export default function ProductsPage() {
  return (
    <MasterDataCrudPage<ProductDto, ProductForm>
      title="Vật tư"
      endpoint="/master-data/products"
      searchPlaceholder="Tìm kiếm mã, tên, barcode..."
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({
        code: item.code,
        name: item.name,
        description: item.description ?? "",
        barcode: item.barcode ?? "",
        baseUomId: item.baseUomId,
        isActive: item.isActive,
      })}
      columns={[
        { key: "code", label: "Mã", render: (item) => <span className="font-mono text-blue-400">{item.code}</span> },
        { key: "name", label: "Tên", render: (item) => item.name },
        { key: "barcode", label: "Barcode", render: (item) => <span className="font-mono text-sm text-white/60">{item.barcode ?? "—"}</span> },
        { key: "baseUom", label: "Đơn vị cơ bản", render: (item) => <span className="text-sm text-white/50">{item.baseUomCode} ({item.baseUomName})</span> },
        { key: "isActive", label: "Trạng thái", render: (item) => <span className={item.isActive ? "text-green-400" : "text-red-400"}>{item.isActive ? "Hoạt động" : "Vô hiệu"}</span> },
      ]}
    />
  );
}
