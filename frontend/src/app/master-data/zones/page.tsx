"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
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
  const t = useTranslations("MasterData.zones");
  const ts = useTranslations("MasterData.common");
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
      label: t("fields.warehouseId.label"),
      type: "select",
      required: true,
      options: warehouseOptions,
    },
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "name", label: t("fields.name.label"), type: "text", required: true, placeholder: t("fields.name.placeholder") },
    {
      name: "zoneType",
      label: t("fields.zoneType.label"),
      type: "select",
      required: true,
      options: zoneTypes,
    },
    { name: "temperatureLimit", label: t("fields.temperatureLimit.label"), type: "number", required: true, step: "0.1" },
    { name: "isLocked", label: t("fields.isLocked.label"), type: "checkbox" },
  ];

  return (
    <MasterDataCrudPage<StorageZone, ZoneForm>
      title={t("page.title")}
      endpoint="/master-data/storage-zones"
      searchPlaceholder={t("page.searchPlaceholder")}
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
        { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "name", label: t("columns.name"), render: (item) => item.name },
        { key: "warehouse", label: t("columns.warehouse"), render: (item) => <span className="text-white/50">{item.warehouseCode}</span> },
        { key: "zoneType", label: t("columns.zoneType"), render: (item) => item.zoneType },
        {
          key: "isLocked",
          label: t("columns.status"),
          render: (item) => (
            <span className={item.isLocked ? "text-yellow-400" : "text-white/40"}>
              {item.isLocked ? ts("status.locked") : ts("status.open")}
            </span>
          ),
        },
      ]}
    />
  );
}
