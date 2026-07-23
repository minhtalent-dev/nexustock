"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
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
  const t = useTranslations("MasterData.locations");
  const ts = useTranslations("MasterData.common");
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
    { name: "zoneId", label: t("fields.zoneId.label"), type: "select", required: true, options: zoneOptions },
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "maxCapacity", label: t("fields.maxCapacity.label"), type: "number", required: true, step: "0.01" },
    { name: "maxVolume", label: t("fields.maxVolume.label"), type: "number", required: true, step: "0.01" },
    { name: "xCoord", label: t("fields.xCoord.label"), type: "number", required: true },
    { name: "yCoord", label: t("fields.yCoord.label"), type: "number", required: true },
    { name: "zCoord", label: t("fields.zCoord.label"), type: "number", required: true },
    { name: "length", label: t("fields.length.label"), type: "number", required: true, step: "0.01" },
    { name: "width", label: t("fields.width.label"), type: "number", required: true, step: "0.01" },
    { name: "height", label: t("fields.height.label"), type: "number", required: true, step: "0.01" },
    { name: "isLocked", label: t("fields.isLocked.label"), type: "checkbox" },
    { name: "lockReasonCode", label: t("fields.lockReasonCode.label"), type: "text" },
    { name: "isActive", label: t("fields.isActive.label"), type: "checkbox" },
  ];

  return (
    <MasterDataCrudPage<StorageLocation, LocationForm>
      title={t("page.title")}
      endpoint="/master-data/storage-locations"
      searchPlaceholder={t("page.searchPlaceholder")}
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
        { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono">{item.code}</span> },
        { key: "warehouse", label: t("columns.warehouse"), render: (item) => <span className="text-foreground/50">{item.warehouseCode}</span> },
        { key: "zone", label: t("columns.zone"), render: (item) => `${item.zoneCode} - ${item.zoneName}` },
        { key: "coord", label: t("columns.coord"), render: (item) => <span className="font-mono text-xs">({item.xCoord}, {item.yCoord}, {item.zCoord})</span> },
        {
          key: "locked",
          label: t("columns.status"),
          render: (item) => (
            <span className={item.isLocked ? "text-yellow-400" : "text-foreground/40"}>
              {item.isLocked ? ts("status.locked") : ts("status.open")}
            </span>
          ),
        },
      ]}
    />
  );
}
