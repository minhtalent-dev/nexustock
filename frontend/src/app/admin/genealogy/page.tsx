"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { GitFork, Search } from "lucide-react";

export default function GenealogyIndexPage() {
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
        <h1 className="text-2xl font-bold">Phả hệ vật tư</h1>
      </div>
      <p className="text-zinc-400 text-sm">
        Tra cứu cây phả hệ Lot cha/con, khoanh vùng và phong tỏa nhánh lỗi chất lượng.
      </p>

      <div className="flex gap-3 max-w-md">
        <Input
          placeholder="Nhập số lô hàng (ví dụ: LOT-...)"
          value={lotNo}
          onChange={(e) => setLotNo(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && handleSearch()}
          className="bg-zinc-900 border-zinc-700 text-white placeholder:text-zinc-500"
        />
        <Button onClick={handleSearch} className="bg-indigo-600 hover:bg-indigo-500 text-white">
          <Search className="h-4 w-4 mr-2" />
          Tìm kiếm
        </Button>
      </div>
    </div>
  );
}
