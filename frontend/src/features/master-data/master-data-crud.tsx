"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
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
}: MasterDataCrudPageProps<TItem, TForm>) {
  const t = useTranslations("MasterData.common");
  const confirm = useConfirmDialog();
  const [data, setData] = useState<PagedResult<TItem> | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<TItem | null>(null);
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
    setForm({ ...defaultForm });
    setIsDialogOpen(true);
  };

  const openEdit = (item: TItem) => {
    setEditing(item);
    setForm(toForm(item));
    setIsDialogOpen(true);
  };

  const closeDialog = () => {
    setIsDialogOpen(false);
    setEditing(null);
    setForm({ ...defaultForm });
  };

  const setFieldValue = (name: keyof TForm & string, value: unknown) => {
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);

    const payload = editing ? { ...form, rowVersion: editing.rowVersion } : form;

    try {
      if (editing) {
        await api.put(`${endpoint}/${editing.id}`, payload);
        showSuccess(t("toast.updateSuccess"));
      } else {
        await api.post(endpoint, payload);
        showSuccess(t("toast.createSuccess"));
      }
      closeDialog();
      await fetchData();
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
    <div>
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-white/90">{title}</h1>
          <p className="text-xs text-white/40 mt-1">{t("page.subtitle")}</p>
        </div>
        <Button onClick={openCreate} size="lg">
          {t("actions.add")}
        </Button>
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          queueMicrotask(() => setPage(1));
          fetchData();
        }}
        className="flex gap-3 mb-4"
      >
        <input
          className="bg-[#1a1a1a] border border-[#333] rounded-md px-3 py-2 text-sm w-80 text-white/90 placeholder:text-white/30 focus:outline-none focus:border-[#555]"
          placeholder={searchPlaceholder}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Button type="submit" variant="secondary">
          {t("actions.search")}
        </Button>
      </form>

      {loading && <p className="text-white/40 text-sm">{t("states.loading")}</p>}

      {data && data.items.length === 0 && !loading && (
        <div className="border border-dashed border-[#333] rounded-lg p-10 text-center text-white/50">
          <p className="font-medium text-white/80">{t("states.emptyTitle")}</p>
          <p className="text-sm mt-1">{t("states.emptyHint")}</p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <>
          <div className="rounded-lg border border-[#222] overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-[#111] text-white/50 text-xs uppercase tracking-wider">
                <tr>
                  {columns.map((column) => (
                    <th key={column.key} className="text-left p-3 font-medium">{column.label}</th>
                  ))}
                  <th className="text-right p-3 font-medium">{t("columns.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#222]">
                {data.items.map((item) => (
                  <tr key={item.id} className="hover:bg-[#1a1a1a] transition text-white/80">
                    {columns.map((column) => (
                      <td key={column.key} className="p-3">{column.render(item)}</td>
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
          </div>

          <div className="flex items-center justify-between mt-4 text-xs text-white/40">
            <span>{t("states.resultCount", { count: data.totalCount })}</span>
            <div className="flex gap-2">
              <Button variant="outline" size="xs" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                {t("actions.prev")}
              </Button>
              <span className="px-3 py-1">{page}</span>
              <Button variant="outline" size="xs" disabled={page * 10 >= data.totalCount} onClick={() => setPage(page + 1)}>
                {t("actions.next")}
              </Button>
            </div>
          </div>
        </>
      )}

      {dialogOpen && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 z-50">
          <div className="bg-[#111] border border-[#333] rounded-xl w-full max-w-2xl max-h-[90vh] overflow-auto shadow-2xl">
            <div className="flex items-center justify-between p-5 border-b border-[#222]">
              <h2 className="font-semibold text-white/90">
                {editing ? t("dialog.editTitle") : t("dialog.createTitle")}
              </h2>
              <Button onClick={closeDialog} variant="ghost" size="sm">{t("actions.close")}</Button>
            </div>
            <form onSubmit={submitForm} className="p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
              {fields.map((field) => (
                <label key={field.name} className="flex flex-col gap-2 text-sm text-white/80">
                  <span>{field.label}{field.required ? " *" : ""}</span>
                  {field.type === "select" ? (
                    <select
                      required={field.required}
                      className="bg-[#1a1a1a] border border-[#333] rounded-md px-3 py-2 text-sm text-white/90 focus:outline-none focus:border-[#555]"
                      value={String(form[field.name] ?? "")}
                      onChange={(e) => setFieldValue(field.name, e.target.value)}
                    >
                      <option value="">{t("dialog.selectPlaceholder")}</option>
                      {(field.options ?? []).map((option) => (
                        <option key={option.value} value={option.value}>{option.label}</option>
                      ))}
                    </select>
                  ) : field.type === "checkbox" ? (
                    <input
                      type="checkbox"
                      checked={Boolean(form[field.name])}
                      onChange={(e) => setFieldValue(field.name, e.target.checked)}
                      className="size-5 accent-[#2563eb]"
                    />
                  ) : (
                    <input
                      required={field.required}
                      type={field.type}
                      step={field.step}
                      placeholder={field.placeholder}
                      className="bg-[#1a1a1a] border border-[#333] rounded-md px-3 py-2 text-sm text-white/90 placeholder:text-white/30 focus:outline-none focus:border-[#555]"
                      value={String(form[field.name] ?? "")}
                      onChange={(e) => setFieldValue(field.name, field.type === "number" ? Number(e.target.value) : e.target.value)}
                    />
                  )}
                </label>
              ))}
              <div className="md:col-span-2 flex justify-end gap-3 pt-3 border-t border-[#222]">
                <Button type="button" onClick={closeDialog} variant="outline">
                  {t("actions.cancel")}
                </Button>
                <Button disabled={saving}>
                  {saving ? t("actions.saving") : t("actions.save")}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
