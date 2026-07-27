"use client";

import type { ReactNode } from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageShell } from "@/components/layout/page-shell";
import { FilterBar } from "@/components/layout/filter-bar";
import { DataTableFrame } from "@/components/layout/data-table-frame";
import api from "@/lib/api";
import { showError, showInfo, showSuccess, showWarning } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { useConfirmDialog } from "@/lib/confirm-dialog";
import type { PagedResult } from "@/types/master-data";

type FieldType = "text" | "number" | "checkbox" | "select";

export interface CrudOption {
  value: string;
  label: string;
}

export interface CrudField<TForm extends Record<string, unknown>> {
  name: keyof TForm & string;
  label: string;
  type: FieldType;
  required?: boolean;
  placeholder?: string;
  options?: CrudOption[];
  step?: string;
}

interface MasterDataCrudPageProps<TItem extends { id: string; rowVersion: number }, TForm extends Record<string, unknown>> {
  title: string;
  endpoint: string;
  searchPlaceholder: string;
  fields: CrudField<TForm>[];
  defaultForm: TForm;
  columns: Array<{
    key: string;
    label: string;
    render: (item: TItem) => React.ReactNode;
  }>;
  toForm: (item: TItem) => TForm;
  filters?: Record<string, string | number | boolean | undefined>;
  renderDialogExtra?: (ctx: { editing: TItem | null; createdItem: TItem | null }) => ReactNode;
  onCreated?: (item: TItem) => void | Promise<void | { keepOpen?: boolean }>;
  transformPayload?: (form: TForm) => Record<string, unknown>;
}

export default function MasterDataCrudPage<TItem extends { id: string; rowVersion: number }, TForm extends Record<string, unknown>>({
  title,
  endpoint,
  searchPlaceholder,
  fields,
  defaultForm,
  columns,
  toForm,
  filters,
  renderDialogExtra,
  onCreated,
  transformPayload,
}: MasterDataCrudPageProps<TItem, TForm>) {
  const t = useTranslations("MasterData.common");
  const tf = useTranslations("Common.files");
  const tc = useTranslations("Common.actions");
  const confirm = useConfirmDialog();
  const [data, setData] = useState<PagedResult<TItem> | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<TItem | null>(null);
  const [createdItem, setCreatedItem] = useState<TItem | null>(null);
  const [form, setForm] = useState<TForm>(defaultForm);
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const params = useMemo(() => ({ search: search || undefined, page, pageSize: 10, ...filters }), [search, page, filters]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<PagedResult<TItem>>(endpoint, { params });
      setData(res.data);
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.loadFailed")));
    } finally {
      setLoading(false);
    }
  }, [endpoint, params, t]);

  useEffect(() => {
    queueMicrotask(() => void fetchData());
  }, [fetchData]);

  const openCreate = () => {
    setEditing(null);
    setCreatedItem(null);
    const initialForm = { ...defaultForm };
    fields.forEach((field) => {
      if (field.type === "select" && field.required && !initialForm[field.name] && field.options && field.options.length > 0) {
        initialForm[field.name] = field.options[0].value as TForm[keyof TForm & string];
      }
    });
    setForm(initialForm);
    setIsDialogOpen(true);
  };

  const openEdit = (item: TItem) => {
    setEditing(item);
    setCreatedItem(null);
    setForm(toForm(item));
    setIsDialogOpen(true);
  };

  const resetDialog = () => {
    setIsDialogOpen(false);
    setEditing(null);
    setCreatedItem(null);
    setForm({ ...defaultForm });
  };

  const closeDialog = async () => {
    if (createdItem) {
      const ok = await confirm({
        title: tf("confirmClosePendingTitle"),
        description: tf("confirmClosePendingDescription"),
        confirmText: tc("confirm"),
        cancelText: tc("cancel"),
        tone: "danger",
      });
      if (!ok) return;
    }
    resetDialog();
  };

  const setFieldValue = (name: keyof TForm & string, value: unknown) => {
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);

    const basePayload = transformPayload ? transformPayload(form) : form;
    const payload = editing ? { ...basePayload, rowVersion: editing.rowVersion } : basePayload;

    try {
      if (editing) {
        await api.put(`${endpoint}/${editing.id}`, payload);
        showSuccess(t("toast.updateSuccess"));
        resetDialog();
        await fetchData();
      } else if (createdItem) {
        // Retry bind cho item đã tạo trước đó
        const res = await onCreated?.(createdItem);
        if (res && res.keepOpen) {
          // Vẫn giữ dialog mở
          await fetchData();
        } else {
          showSuccess(t("toast.createSuccess"));
          resetDialog();
          await fetchData();
        }
      } else {
        const res = await api.post<TItem>(endpoint, payload);
        const created = res.data;
        let bindResult: void | { keepOpen?: boolean } = undefined;
        if (created) {
          bindResult = await onCreated?.(created);
        }
        if (bindResult && bindResult.keepOpen) {
          setCreatedItem(created);
          await fetchData(); // Refresh list phía sau dialog
        } else {
          showSuccess(t("toast.createSuccess"));
          resetDialog();
          await fetchData();
        }
      }
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.saveFailed")));
    } finally {
      setSaving(false);
    }
  };

  const deleteItem = async (item: TItem) => {
    const ok = await confirm({
      title: t("dialog.deleteTitle"),
      description: t("dialog.deleteDescription"),
      confirmText: t("actions.delete"),
      cancelText: t("actions.cancel"),
      tone: "danger",
    });
    if (!ok) {
      showInfo(t("toast.deleteCancelled"));
      return;
    }

    try {
      await api.delete(`${endpoint}/${item.id}`);
      showSuccess(t("toast.deleteSuccess"));
      await fetchData();
    } catch (err: unknown) {
      showWarning(getHttpErrorMessage(err, t("toast.deleteFailed")));
    }
  };

  const dialogOpen = isDialogOpen;

  return (
    <PageShell
      title={title}
      description={t("page.subtitle")}
      actions={
        <Button onClick={openCreate} size="lg">
          {t("actions.add")}
        </Button>
      }
      filters={
        <FilterBar>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              queueMicrotask(() => setPage(1));
              fetchData();
            }}
            className="flex flex-wrap items-center gap-2 w-full"
          >
            <Input
              className="w-80 max-w-full"
              placeholder={searchPlaceholder}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <Button type="submit" variant="secondary">
              {t("actions.search")}
            </Button>
          </form>
        </FilterBar>
      }
    >
      <DataTableFrame
        loading={loading && !data}
        empty={Boolean(data && data.items.length === 0 && !loading)}
        emptyTitle={t("states.emptyTitle")}
        emptyDescription={t("states.emptyHint")}
      >
        {data && data.items.length > 0 ? (
          <>
            <table className="w-full text-sm">
              <thead className="bg-muted/50 text-muted-foreground text-xs uppercase tracking-wider">
                <tr>
                  {columns.map((column) => (
                    <th key={column.key} className="text-left p-3 font-medium">
                      {column.label}
                    </th>
                  ))}
                  <th className="text-right p-3 font-medium">{t("columns.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data.items.map((item) => (
                  <tr key={item.id} className="hover:bg-muted/40 transition text-foreground">
                    {columns.map((column) => (
                      <td key={column.key} className="p-3">
                        {column.render(item)}
                      </td>
                    ))}
                    <td className="p-3 text-right">
                      <div className="flex justify-end gap-2">
                        <Button onClick={() => openEdit(item)} variant="outline" size="xs">
                          {t("actions.edit")}
                        </Button>
                        <Button onClick={() => deleteItem(item)} variant="destructive" size="xs">
                          {t("actions.delete")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="flex items-center justify-between border-t border-border p-3 text-xs text-muted-foreground">
              <span>{t("states.resultCount", { count: data.totalCount })}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="xs" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  {t("actions.prev")}
                </Button>
                <span className="px-3 py-1">{page}</span>
                <Button
                  variant="outline"
                  size="xs"
                  disabled={page * 10 >= data.totalCount}
                  onClick={() => setPage(page + 1)}
                >
                  {t("actions.next")}
                </Button>
              </div>
            </div>
          </>
        ) : null}
      </DataTableFrame>

      {dialogOpen && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 z-50">
          <div className="bg-card border border-border rounded-xl w-full max-w-2xl max-h-[90vh] overflow-auto shadow-2xl">
            <div className="flex items-center justify-between p-5 border-b border-border">
              <h2 className="font-semibold text-foreground">
                {editing ? t("dialog.editTitle") : t("dialog.createTitle")}
              </h2>
              <Button onClick={() => void closeDialog()} variant="ghost" size="sm">
                {t("actions.close")}
              </Button>
            </div>
            <form onSubmit={submitForm} className="p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
              {fields.map((field) => (
                <label key={field.name} className="flex flex-col gap-2 text-sm text-foreground">
                  <span>
                    {field.label}
                    {field.required ? " *" : ""}
                  </span>
                  {field.type === "select" ? (
                    <select
                      required={field.required}
                      className="bg-background border border-input rounded-md px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                      value={String(form[field.name] ?? "")}
                      onChange={(e) => setFieldValue(field.name, e.target.value)}
                    >
                      <option value="">{t("dialog.selectPlaceholder")}</option>
                      {(field.options ?? []).map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  ) : field.type === "checkbox" ? (
                    <input
                      type="checkbox"
                      checked={Boolean(form[field.name])}
                      onChange={(e) => setFieldValue(field.name, e.target.checked)}
                      className="size-5 accent-primary"
                    />
                  ) : (
                    <Input
                      required={field.required}
                      type={field.type}
                      step={field.step}
                      placeholder={field.placeholder}
                      value={String(form[field.name] ?? "")}
                      onChange={(e) =>
                        setFieldValue(field.name, field.type === "number" ? Number(e.target.value) : e.target.value)
                      }
                    />
                  )}
                </label>
              ))}
              <div className="md:col-span-2 flex justify-end gap-3 pt-3 border-t border-border">
                <Button type="button" onClick={() => void closeDialog()} variant="outline">
                  {t("actions.cancel")}
                </Button>
                <Button type="submit" disabled={saving}>{saving ? t("actions.saving") : t("actions.save")}</Button>
              </div>
            </form>
            {renderDialogExtra ? <div className="border-t border-border p-5">{renderDialogExtra({ editing, createdItem })}</div> : null}
          </div>
        </div>
      )}
    </PageShell>
  );
}
