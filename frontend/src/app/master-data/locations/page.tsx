"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";
import MasterDataCrudPage, { type CrudField } from "@/features/master-data/master-data-crud";
import type { StorageLocation, StorageZone, PagedResult } from "@/types/master-data";

type LocationForm = {
  zoneId: string;
  code: string;
  maxCapacity: number;
  maxVolume: number;
  xCoord: number;
  yCoord: number;
  zCoord: number;
  length: number;
  width: number;
  height: number;
  isLocked: boolean;
  lockReasonCode: string;
  isActive: boolean;
};

const defaultForm: LocationForm = {
  zoneId: "",
  code: "",
  maxCapacity: 999999,
  maxVolume: 999999,
  xCoord: 0,
  yCoord: 0,
  zCoord: 0,
  length: 0,
  width: 0,
  height: 0,
  isLocked: false,
  lockReasonCode: "",
  isActive: true,
};

export default function LocationsPage() {
  const [zoneOptions, setZoneOptions] = useState<{ value: string; label: string }[]>([]);

  useEffect(() => {
    const fetchZones = async () => {
      try {
        const res = await api.get<PagedResult<StorageZone>>("/master-data/storage-zones", {
          params: { page: 1, pageSize: 100 },
        });
        setZoneOptions(
          res.data.items.map((z) => ({ value: z.id, label: `${z.warehouseCode} / ${z.code} - ${z.name}` }))
        );
      } catch (err) {
        console.error("Lỗi lấy danh sách vùng kho:", err);
      }
    };
    fetchZones();
  }, []);

  const fields: CrudField<LocationForm>[] = [
    { name: "zoneId", label: "Vùng kho", type: "select", required: true, options: zoneOptions },
    { name: "code", label: "Mã vị trí", type: "text", required: true, placeholder: "A-01-01" },
    { name: "maxCapacity", label: "Sức chứa tối đa", type: "number", required: true, step: "0.01" },
    { name: "maxVolume", label: "Thể tích tối đa", type: "number", required: true, step: "0.01" },
    { name: "xCoord", label: "Tọa độ X", type: "number", required: true },
    { name: "yCoord", label: "Tọa độ Y", type: "number", required: true },
    { name: "zCoord", label: "Tọa độ Z", type: "number", required: true },
    { name: "length", label: "Chiều dài", type: "number", required: true, step: "0.01" },
    { name: "width", label: "Chiều rộng", type: "number", required: true, step: "0.01" },
    { name: "height", label: "Chiều cao", type: "number", required: true, step: "0.01" },
    { name: "isLocked", label: "Khóa vị trí", type: "checkbox" },
    { name: "lockReasonCode", label: "Mã lý do khóa", type: "text" },
    { name: "isActive", label: "Hoạt động", type: "checkbox" },
  ];

  return (
    <MasterDataCrudPage<StorageLocation, LocationForm>
      title="Vị trí kệ"
      endpoint="/master-data/storage-locations"
      searchPlaceholder="Tìm kiếm mã vị trí..."
      defaultForm={defaultForm}
      fields={fields}
      toForm={(item) => ({
        zoneId: item.zoneId,
        code: item.code,
        maxCapacity: item.maxCapacity,
        maxVolume: item.maxVolume,
        xCoord: item.xCoord,
        yCoord: item.yCoord,
        zCoord: item.zCoord,
        length: item.length,
        width: item.width,
        height: item.height,
        isLocked: item.isLocked,
        lockReasonCode: item.lockReasonCode || "",
        isActive: item.isActive,
      })}
      columns={[
        { key: "code", label: "Mã", render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "warehouse", label: "Nhà kho", render: (item) => <span className="text-white/50">{item.warehouseCode}</span> },
        { key: "zone", label: "Vùng", render: (item) => `${item.zoneCode} - ${item.zoneName}` },
        { key: "coord", label: "Tọa độ", render: (item) => <span className="font-mono text-xs">({item.xCoord}, {item.yCoord}, {item.zCoord})</span> },
        {
          key: "locked",
          label: "Trạng thái",
          render: (item) => <span className={item.isLocked ? "text-yellow-400" : "text-white/40"}>{item.isLocked ? "Đã khóa" : "Mở"}</span>,
        },
      ]}
    />
  );
}
