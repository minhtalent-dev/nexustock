"use client";

import { PageShell } from "@/components/layout/page-shell";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { GitFork, Search } from "lucide-react";

export default function GenealogyIndexPage() {
  const t = useTranslations("Admin.genealogy");
  const [lotNo, setLotNo] = useState("");
  const router = useRouter();

  const handleSearch = () => {
    const trimmed = lotNo.trim();
    if (!trimmed) return;
    router.push(`/admin/genealogy/${encodeURIComponent(trimmed)}`);
  };

  return (
    <PageShell className="gap-6">
      <div className="flex items-center gap-3">
        <GitFork className="h-6 w-6 text-indigo-400" />
        <h1 className="text-2xl font-bold">{t("title")}</h1>
      </div>
      <p className="text-muted-foreground text-sm">{t("subtitle")}</p>

      <div className="flex gap-3 max-w-md">
        <Input
          placeholder={t("searchPlaceholder")}
          value={lotNo}
          onChange={(e) => setLotNo(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && handleSearch()}
          className="bg-card border-border text-foreground placeholder:text-muted-foreground"
        />
        <Button onClick={handleSearch} className="bg-indigo-600 hover:bg-indigo-500 text-foreground">
          <Search className="h-4 w-4 mr-2" />
          {t("searchBtn")}
        </Button>
      </div>
    </PageShell>
  );
}
