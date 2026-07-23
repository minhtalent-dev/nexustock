"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { PageShell } from "@/components/layout/page-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { showError, showSuccess, showWarning } from "@/lib/toast";
import { getHttpErrorMessage } from "@/lib/http-error";
import { getStorageSettings, saveStorageSettings, testStorageSettings, type StorageSettings } from "@/features/files/api";

export default function StorageSettingsPage() {
  const t = useTranslations("Admin.storage");
  const [settings, setSettings] = useState<StorageSettings | null>(null);
  const [provider, setProvider] = useState("LOCAL");
  const [publicBaseUrl, setPublicBaseUrl] = useState("");
  const [bucket, setBucket] = useState("");
  const [region, setRegion] = useState("ap-southeast-1");
  const [accessKeyId, setAccessKeyId] = useState("");
  const [secretAccessKey, setSecretAccessKey] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        const data = await getStorageSettings();
        setSettings(data);
        setProvider(data.activeProvider);
        setPublicBaseUrl(data.publicBaseUrl ?? "");
      } catch (err: unknown) {
        showError(getHttpErrorMessage(err, t("toast.loadFailed")));
      }
    })();
  }, [t]);

  const buildBody = (activate: boolean) => ({
    activeProvider: provider,
    publicBaseUrl: publicBaseUrl || null,
    activate,
    config:
      provider === "LOCAL"
        ? undefined
        : {
            bucket: bucket || undefined,
            region: region || undefined,
            accessKeyId: accessKeyId || "********",
            secretAccessKey: secretAccessKey || "********",
          },
  });

  const onTest = async () => {
    setLoading(true);
    try {
      const res = await testStorageSettings(buildBody(false));
      if (res.ok) showSuccess(t("toast.testOk"));
      else showWarning(res.message);
      setSettings(await getStorageSettings());
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.testFailed")));
    } finally {
      setLoading(false);
    }
  };

  const onSave = async () => {
    setLoading(true);
    try {
      const data = await saveStorageSettings(buildBody(true));
      setSettings(data);
      showSuccess(t("toast.saveOk"));
    } catch (err: unknown) {
      showError(getHttpErrorMessage(err, t("toast.saveFailed")));
    } finally {
      setLoading(false);
    }
  };

  return (
    <PageShell title={t("page.title")} description={t("page.subtitle")} className="max-w-3xl gap-6">
      {settings?.lastTestOk === false ? (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {settings.lastTestMessage ?? t("banner.testFailed")}
        </div>
      ) : null}

      <div className="space-y-4 rounded-lg border border-border bg-card p-6">
        <label className="flex flex-col gap-2 text-sm">
          <span>{t("fields.provider")}</span>
          <select
            className="rounded-md border border-input bg-background px-3 py-2"
            value={provider}
            onChange={(e) => setProvider(e.target.value)}
          >
            {(settings?.providers ?? [{ id: "LOCAL", label: "Local disk" }]).map((p) => (
              <option key={p.id} value={p.id}>
                {p.label}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-2 text-sm">
          <span>{t("fields.publicBaseUrl")}</span>
          <Input
            value={publicBaseUrl}
            onChange={(e) => setPublicBaseUrl(e.target.value)}
            placeholder="https://cdn.example.com/files"
          />
          <span className="text-xs text-muted-foreground">{t("fields.publicBaseUrlHint")}</span>
        </label>

        {provider !== "LOCAL" ? (
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <label className="flex flex-col gap-2 text-sm">
              <span>Bucket / Container</span>
              <Input value={bucket} onChange={(e) => setBucket(e.target.value)} />
            </label>
            <label className="flex flex-col gap-2 text-sm">
              <span>Region</span>
              <Input value={region} onChange={(e) => setRegion(e.target.value)} />
            </label>
            <label className="flex flex-col gap-2 text-sm">
              <span>Access Key</span>
              <Input value={accessKeyId} onChange={(e) => setAccessKeyId(e.target.value)} placeholder="********" />
            </label>
            <label className="flex flex-col gap-2 text-sm">
              <span>Secret</span>
              <Input
                type="password"
                value={secretAccessKey}
                onChange={(e) => setSecretAccessKey(e.target.value)}
                placeholder="********"
              />
            </label>
          </div>
        ) : null}

        <div className="flex justify-end gap-2 border-t border-border pt-4">
          <Button type="button" variant="outline" disabled={loading} onClick={() => void onTest()}>
            {t("actions.test")}
          </Button>
          <Button type="button" disabled={loading} onClick={() => void onSave()} className="bg-emerald-600 text-white hover:bg-emerald-500">
            {t("actions.saveActivate")}
          </Button>
        </div>
      </div>
    </PageShell>
  );
}
