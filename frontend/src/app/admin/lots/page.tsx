"use client";

import { useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast } from "@/lib/toast";
import { Search, Tag, AlertCircle } from "lucide-react";

interface LotResponseDto {
  id: string;
  lotNo: string;
  itemId: string;
  itemName: string;
  itemCode: string;
  expiryDate: string | null;
  productionDate: string | null;
  qcStatus: string;
}

export default function LotsPage() {
  const t = useTranslations("Admin.lots");
  const tc = useTranslations("Admin.common");
  const tErrors = useTranslations("Errors");

  const [searchLotNo, setSearchLotNo] = useState("");
  const [lots, setLots] = useState<LotResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchLotNo.trim()) {
      showApiErrorToast("", t("errors.lotRequired"));
      return;
    }

    setLoading(true);
    setSearched(true);
    try {
      const res = await api.get<LotResponseDto[]>(`/lots/${searchLotNo.trim()}`);
      setLots(res.data);
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.searchFailed"));
    } finally {
      setLoading(false);
    }
  };

  const getQcBadge = (status: string) => {
    switch (status.toUpperCase()) {
      case "RELEASE":
        return <Badge className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">{t("statusRelease")}</Badge>;
      case "HOLD":
        return <Badge className="bg-amber-500/10 text-amber-500 border-amber-500/20">{t("statusHold")}</Badge>;
      case "REJECT":
        return <Badge className="bg-red-500/10 text-red-500 border-red-500/20">{t("statusReject")}</Badge>;
      case "UNSPEC":
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">{t("statusUnspec")}</Badge>;
      default:
        return <Badge className="bg-zinc-500/10 text-zinc-500 border-zinc-500/20">{status}</Badge>;
    }
  };

  return (
    <div className="flex flex-col gap-6 font-sans">
      <div>
        <h1 className="text-2xl font-bold text-white flex items-center gap-3">
          <Tag className="h-6 w-6 text-emerald-500" />
          {t("title")}
        </h1>
        <p className="text-xs text-zinc-400 mt-1">{t("subtitle")}</p>
      </div>

      <Card className="bg-[#111] border-zinc-800/80">
        <CardContent className="p-6">
          <form onSubmit={handleSearch} className="flex gap-3 max-w-md">
            <Input
              placeholder={t("searchPlaceholder")}
              value={searchLotNo}
              onChange={(e) => setSearchLotNo(e.target.value)}
              className="bg-zinc-900 border-zinc-800 text-white focus-visible:ring-emerald-500 text-sm h-10"
            />
            <Button type="submit" disabled={loading} className="bg-emerald-600 hover:bg-emerald-500 text-white h-10 px-5 gap-2 shrink-0">
              {loading ? (
                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
              ) : (
                <Search className="h-4 w-4" />
              )}
              {tc("search")}
            </Button>
          </form>
        </CardContent>
      </Card>

      {searched && (
        <Card className="bg-[#111] border-zinc-800/80">
          <CardHeader className="py-4 border-b border-zinc-800/60">
            <CardTitle className="text-sm font-semibold text-white">{t("resultsTitle")}</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader className="bg-zinc-900/30 border-b border-zinc-800/60">
                <TableRow className="hover:bg-transparent">
                  <TableHead className="text-zinc-400 font-semibold h-11">{t("colLotNo")}</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11">{t("colItem")}</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-center w-40">{t("colProductionDate")}</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-center w-40">{t("colExpiryDate")}</TableHead>
                  <TableHead className="text-zinc-400 font-semibold h-11 text-center w-40">{t("colQcStatus")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {lots.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-zinc-500 py-12">
                      <div className="flex flex-col items-center gap-2">
                        <AlertCircle className="h-8 w-8 text-zinc-600" />
                        <span>{t("empty")}</span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  lots.map((l) => (
                    <TableRow key={l.id} className="border-b border-zinc-800/50 hover:bg-zinc-900/20">
                      <TableCell className="text-white font-semibold">
                        <Link href={`/admin/genealogy/${l.lotNo}`} className="text-indigo-400 hover:underline">
                          {l.lotNo}
                        </Link>
                      </TableCell>
                      <TableCell className="text-zinc-300">
                        <div>
                          <p>{l.itemName}</p>
                          <p className="text-[10px] text-zinc-500 font-normal">{l.itemCode}</p>
                        </div>
                      </TableCell>
                      <TableCell className="text-center text-zinc-400 font-mono">
                        {l.productionDate ? new Date(l.productionDate).toLocaleDateString("vi-VN") : "—"}
                      </TableCell>
                      <TableCell className="text-center text-zinc-400 font-mono">
                        {l.expiryDate ? new Date(l.expiryDate).toLocaleDateString("vi-VN") : "—"}
                      </TableCell>
                      <TableCell className="text-center">{getQcBadge(l.qcStatus)}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
