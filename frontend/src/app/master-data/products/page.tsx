"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import MasterDataCrudPage, { type CrudField, type CrudOption } from "@/features/master-data/master-data-crud";
import { EntityAttachmentsPanel } from "@/features/files/entity-attachments-panel";
import { bindAttachment, type UploadResult } from "@/features/files/api";
import { MasterDataExportButtons } from "@/features/master-data/export-buttons";
import api from "@/lib/api";
import type { PagedResult, ProductDto, Uom } from "@/types/master-data";

type ProductForm = {
  code: string;
  name: string;
  description: string;
  barcode: string;
  baseUomId: string;
  isActive: boolean;
};

const defaultForm: ProductForm = { code: "", name: "", description: "", barcode: "", baseUomId: "", isActive: true };

export default function ProductsPage() {
  const t = useTranslations("MasterData.products");
  const ts = useTranslations("MasterData.common");
  const [pendingUploads, setPendingUploads] = useState<UploadResult[]>([]);
  const [uomOptions, setUomOptions] = useState<CrudOption[]>([]);

  useEffect(() => {
    api
      .get<PagedResult<Uom>>("/master-data/uoms", { params: { pageSize: 100 } })
      .then((res) => {
        const opts = (res.data?.items ?? []).map((u) => ({
          value: u.id,
          label: `${u.code} - ${u.name}`,
        }));
        setUomOptions(opts);
      })
      .catch(() => {});
  }, []);

  const fields: CrudField<ProductForm>[] = [
    { name: "code", label: t("fields.code.label"), type: "text", required: true, placeholder: t("fields.code.placeholder") },
    { name: "name", label: t("fields.name.label"), type: "text", required: true, placeholder: t("fields.name.placeholder") },
    { name: "barcode", label: t("fields.barcode.label"), type: "text", placeholder: t("fields.barcode.placeholder") },
    {
      name: "baseUomId",
      label: t("fields.baseUomId.label"),
      type: "select",
      required: true,
      options: uomOptions,
      placeholder: t("fields.baseUomId.placeholder"),
    },
    { name: "description", label: t("fields.description.label"), type: "text", placeholder: t("fields.description.placeholder") },
    { name: "isActive", label: t("fields.isActive.label"), type: "checkbox" },
  ];

  return (
    <div className="space-y-3 font-sans">
      <div className="flex justify-end px-1">
        <MasterDataExportButtons type="ITEMS" />
      </div>
      <MasterDataCrudPage<ProductDto, ProductForm>
        title={t("page.title")}
        endpoint="/master-data/products"
        searchPlaceholder={t("page.searchPlaceholder")}
        defaultForm={{
          ...defaultForm,
          baseUomId: uomOptions[0]?.value ?? "",
        }}
        fields={fields}
        toForm={(item) => ({
          code: item.code,
          name: item.name,
          description: item.description ?? "",
          barcode: item.barcode ?? "",
          baseUomId: item.baseUomId,
          isActive: item.isActive,
        })}
        transformPayload={(f) => ({
          code: f.code,
          name: f.name,
          description: f.description || null,
          barcode: f.barcode || null,
          baseUomId: f.baseUomId || uomOptions[0]?.value || "",
          isActive: f.isActive,
          isSerialTracked: false,
          config: {
            iqcCheckType: "FULL",
            vendorInnerLotCtl: false,
            isWafer: false,
            lotValidationRegex: null,
            minStock: 0,
            maxStock: 999999,
            weightClass: "MEDIUM",
            rotationSpeed: "SLOW",
            trackSerial: false,
            length: 0,
            width: 0,
            height: 0,
            weight: 0,
          },
          packages: [],
        })}
        onCreated={async (item) => {
          for (const u of pendingUploads) {
            await bindAttachment({
              uploadId: u.uploadId,
              entityType: "PRODUCT",
              entityId: item.id,
            });
          }
          setPendingUploads([]);
        }}
        renderDialogExtra={({ editing }) => (
          <EntityAttachmentsPanel
            entityType="PRODUCT"
            entityId={editing?.id ?? null}
            pendingUploads={pendingUploads}
            onPendingChange={setPendingUploads}
          />
        )}
        columns={[
          { key: "code", label: t("columns.code"), render: (item) => <span className="font-mono text-blue-400">{item.code}</span> },
          { key: "name", label: t("columns.name"), render: (item) => item.name },
          { key: "barcode", label: t("columns.barcode"), render: (item) => <span className="font-mono text-sm text-foreground/60">{item.barcode ?? "—"}</span> },
          { key: "baseUom", label: t("columns.baseUom"), render: (item) => <span className="text-sm text-foreground/50">{item.baseUomCode} ({item.baseUomName})</span> },
          {
            key: "isActive",
            label: t("columns.status"),
            render: (item) => (
              <span className={item.isActive ? "text-green-400" : "text-red-400"}>
                {item.isActive ? ts("status.active") : ts("status.inactive")}
              </span>
            ),
          },
        ]}
      />
    </div>
  );
}
