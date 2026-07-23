"use client";

import { PageShell } from "@/components/layout/page-shell";

import { use, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { resolveApiError } from "@/lib/api-error-i18n";
import { showApiErrorToast, showSuccess } from "@/lib/toast";
import { ArrowLeft, GitFork, ShieldAlert } from "lucide-react";

interface GenealogyNode {
  lotId: string;
  lotNo: string;
  productCode: string;
  productName: string;
  qtyOnHand: number;
  status: string;
  children: GenealogyNode[];
  parents: GenealogyNode[];
}

export default function GenealogyTreePage({ params }: { params: Promise<{ lotNo: string }> }) {
  const resolvedParams = use(params);
  const lotNo = resolvedParams.lotNo;
  const t = useTranslations("Admin.genealogy");
  const tErrors = useTranslations("Errors");

  const [tree, setTree] = useState<GenealogyNode | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchTree = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<GenealogyNode>(`/genealogy/lots/${lotNo}/tree`);
      setTree(res.data);
    } catch {
      showApiErrorToast("", t("errors.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [lotNo, t]);

  const handleHoldBranch = async () => {
    if (!confirm(t("confirmHold", { lotNo }))) return;
    try {
      await api.post("/genealogy/hold-branch", {
        targetLotNo: lotNo,
        reasonCode: "QUALITY_ISSUE",
        description: "Phong tỏa khẩn cấp phòng chống lây lan lỗi chất lượng",
      });
      showSuccess(t("toastHoldSuccess"));
      fetchTree();
    } catch (err: unknown) {
      const { codeLabel, message } = resolveApiError(err, tErrors);
      showApiErrorToast(codeLabel, message || t("errors.holdFailed"));
    }
  };

  useEffect(() => {
    queueMicrotask(() => void fetchTree());
  }, [fetchTree]);

  const renderNode = (node: GenealogyNode) => {
    const isHold = node.status === "HOLD";
    return (
      <div key={node.lotId} className="flex flex-col items-center gap-2">
        <Card className={`bg-card border-2 ${isHold ? "border-red-500 shadow-[0_0_15px_rgba(239,68,68,0.2)]" : "border-border"} text-foreground w-64`}>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-bold flex justify-between items-center">
              <span>{t("lotLabel", { lotNo: node.lotNo })}</span>
              <Badge className={isHold ? "bg-red-600 hover:bg-red-600" : "bg-emerald-600 hover:bg-emerald-600"}>{node.status}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs space-y-1">
            <div className="text-muted-foreground">
              {t("productLabel", { code: node.productCode, name: node.productName })}
            </div>
            <div className="text-muted-foreground">
              {t("stockLabel")} <span className="text-foreground font-bold">{node.qtyOnHand}</span>
            </div>
          </CardContent>
        </Card>
        {node.children && node.children.length > 0 && (
          <div className="flex flex-col items-center mt-2 w-full">
            <div className="h-4 w-0.5 bg-zinc-700"></div>
            <div className="flex gap-6 border-t border-border pt-4 w-full justify-center">
              {node.children.map((child) => renderNode(child))}
            </div>
          </div>
        )}
      </div>
    );
  };

  if (loading) return <div className="text-muted-foreground p-6 font-mono text-center text-xs">{t("loadingTree")}</div>;
  if (!tree) return <div className="text-muted-foreground p-6 font-mono text-center text-xs">{t("notFound")}</div>;

  return (
    <PageShell className="gap-6">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-3">
          <Link href="/admin/lots">
            <Button variant="outline" className="border-border text-muted-foreground hover:bg-muted">
              <ArrowLeft className="h-4 w-4 mr-2" /> {t("back")}
            </Button>
          </Link>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <GitFork className="h-6 w-6 text-indigo-400" /> {t("detailTitle")}
          </h1>
        </div>
        <Button onClick={handleHoldBranch} className="bg-red-600 hover:bg-red-500 text-foreground font-bold flex items-center gap-2">
          <ShieldAlert className="h-4 w-4" /> {t("holdBranch")}
        </Button>
      </div>

      <div className="overflow-auto border border-border bg-background/40 rounded-xl p-8 min-h-[500px] flex justify-center items-start">
        {renderNode(tree)}
      </div>
    </PageShell>
  );
}
