"use client";

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
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <div className="flex items-center gap-3">
        <GitFork className="h-6 w-6 text-indigo-400" />
        <h1 className="text-2xl font-bold">{t("title")}</h1>
      </div>
      <p className="text-zinc-400 text-sm">{t("subtitle")}</p>

      <div className="flex gap-3 max-w-md">
        <Input
          placeholder={t("searchPlaceholder")}
          value={lotNo}
          onChange={(e) => setLotNo(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && handleSearch()}
          className="bg-zinc-900 border-zinc-700 text-white placeholder:text-zinc-500"
        />
        <Button onClick={handleSearch} className="bg-indigo-600 hover:bg-indigo-500 text-white">
          <Search className="h-4 w-4 mr-2" />
          {t("searchBtn")}
        </Button>
      </div>
    </div>
  );
}
