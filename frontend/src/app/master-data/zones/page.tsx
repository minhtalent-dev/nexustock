"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import type { StorageZone, Warehouse, PagedResult } from "@/types/master-data";

type ZoneForm = {
  warehouseId: string;
  code: string;
  name: string;
  zoneType: string;
  temperatureLimit: number;
  isLocked: boolean;
};

const defaultForm: ZoneForm = {
  warehouseId: "",
  code: "",
  name: "",
  zoneType: "STORAGE",
  temperatureLimit: 25,
  isLocked: false,
};

const zoneTypes = [
  { value: "STORAGE", label: "STORAGE" },
  { value: "QC", label: "QC" },
  { value: "STAGING", label: "STAGING" },
  { value: "SHIPPING", label: "SHIPPING" },
  { value: "QUARANTINE", label: "QUARANTINE" },
];

export default function ZonesPage() {
  const [warehouseOptions, setWarehouseOptions] = useState<{ value: string; label: string }[]>([]);

  useEffect(() => {
    const fetchWarehouses = async () => {
      try {
        const res = await api.get<PagedResult<Warehouse>>("/master-data/warehouses", {
          params: { page: 1, pageSize: 100 },
        });
        setWarehouseOptions(
          res.data.items.map((w) => ({ value: w.id, label: `${w.code} - ${w.name}` }))
        );
      } catch (err) {
        console.error("Lỗi lấy danh sách nhà kho:", err);
      }
    };
    fetchWarehouses();
  }, []);

  const fields: CrudField<ZoneForm>[] = [
    {
      name: "warehouseId",
      label: "Nhà kho",
      type: "select",
      required: true,
      options: warehouseOptions,
    },
    { name: "code", label: "Mã vùng", type: "text", required: true, placeholder: "ZONE-STORAGE" },
    { name: "name", label: "Tên vùng", type: "text", required: true, placeholder: "Khu vực lưu trữ chính" },
    {
      name: "zoneType",
      label: "Loại vùng",
      type: "select",
      required: true,
      options: zoneTypes,
    },
    { name: "temperatureLimit", label: "Giới hạn nhiệt độ (°C)", type: "number", required: true, step: "0.1" },
    { name: "isLocked", label: "Khóa vùng kho", type: "checkbox" },
  ];

  return (
    <MasterDataCrudPage<StorageZone, ZoneForm>
      title="Vùng kho"
      endpoint="/master-data/storage-zones"
      searchPlaceholder="Tìm kiếm mã, tên..."
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({
        warehouseId: item.warehouseId,
        code: item.code,
        name: item.name,
        zoneType: item.zoneType,
        temperatureLimit: item.temperatureLimit ?? 25,
        isLocked: item.isLocked,
      })}
      columns={[
        { key: "code", label: "Mã", render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "name", label: "Tên", render: (item) => item.name },
        { key: "warehouse", label: "Nhà kho", render: (item) => <span className="text-white/50">{item.warehouseCode}</span> },
        { key: "zoneType", label: "Loại vùng", render: (item) => item.zoneType },
        {
          key: "isLocked",
          label: "Trạng thái",
          render: (item) => (
            <span className={item.isLocked ? "text-yellow-400" : "text-white/40"}>
              {item.isLocked ? "Đã khóa" : "Mở"}
            </span>
          ),
        },
      ]}
    />
  );
}
