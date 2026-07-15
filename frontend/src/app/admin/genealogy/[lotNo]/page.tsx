"use client";

import { useEffect, useState, use } from "react";
import Link from "next/link";
import api from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { showError, showSuccess } from "@/lib/toast";
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
  const [tree, setTree] = useState<GenealogyNode | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchTree = async () => {
    setLoading(true);
    try {
      const res = await api.get<GenealogyNode>(`/genealogy/lots/${lotNo}/tree`);
      setTree(res.data);
    } catch {
      showError("Không thể tải cây phả hệ Lot.");
    } finally {
      setLoading(false);
    }
  };

  const handleHoldBranch = async () => {
    if (!confirm(`Bạn có chắc chắn muốn phong tỏa toàn bộ nhánh từ Lot ${lotNo} trở xuống?`)) return;
    try {
      await api.post("/genealogy/hold-branch", {
        targetLotNo: lotNo,
        reasonCode: "QUALITY_ISSUE",
        description: "Phong tỏa khẩn cấp phòng chống lây lan lỗi chất lượng"
      });
      showSuccess("Đã phong tỏa toàn bộ nhánh Lot thành công.");
      fetchTree();
    } catch (err: any) {
      showError(err.response?.data?.message || "Lỗi phong tỏa nhánh.");
    }
  };

  useEffect(() => {
    fetchTree();
  }, [lotNo]);

  const renderNode = (node: GenealogyNode) => {
    const isHold = node.status === "HOLD";
    return (
      <div key={node.lotId} className="flex flex-col items-center gap-2">
        <Card className={`bg-zinc-900 border-2 ${isHold ? "border-red-500 shadow-[0_0_15px_rgba(239,68,68,0.2)]" : "border-zinc-800"} text-white w-64`}>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-bold flex justify-between items-center">
              <span>Lot: {node.lotNo}</span>
              <Badge className={isHold ? "bg-red-600 hover:bg-red-600" : "bg-emerald-600 hover:bg-emerald-600"}>{node.status}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="text-xs space-y-1">
            <div className="text-zinc-400">Sản phẩm: {node.productCode} - {node.productName}</div>
            <div className="text-zinc-400">Tồn kho: <span className="text-zinc-200 font-bold">{node.qtyOnHand}</span></div>
          </CardContent>
        </Card>
        {node.children && node.children.length > 0 && (
          <div className="flex flex-col items-center mt-2 w-full">
            <div className="h-4 w-0.5 bg-zinc-700"></div>
            <div className="flex gap-6 border-t border-zinc-700 pt-4 w-full justify-center">
              {node.children.map(child => renderNode(child))}
            </div>
          </div>
        )}
      </div>
    );
  };

  if (loading) return <div className="text-zinc-500 p-6 font-mono text-center text-xs">Đang tải cây phả hệ...</div>;
  if (!tree) return <div className="text-zinc-500 p-6 font-mono text-center text-xs">Không tìm thấy dữ liệu.</div>;

  return (
    <div className="flex flex-col gap-6 text-white p-6 font-sans">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-3">
          <Link href="/admin/lots">
            <Button variant="outline" className="border-zinc-800 text-zinc-300 hover:bg-zinc-800"><ArrowLeft className="h-4 w-4 mr-2" /> Quay lại</Button>
          </Link>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <GitFork className="h-6 w-6 text-indigo-400" /> Truy vết phả hệ Lot
          </h1>
        </div>
        <Button onClick={handleHoldBranch} className="bg-red-600 hover:bg-red-500 text-white font-bold flex items-center gap-2">
          <ShieldAlert className="h-4 w-4" /> Phong tỏa nhánh
        </Button>
      </div>

      <div className="overflow-auto border border-zinc-800 bg-zinc-950/40 rounded-xl p-8 min-h-[500px] flex justify-center items-start">
        {renderNode(tree)}
      </div>
    </div>
  );
}
