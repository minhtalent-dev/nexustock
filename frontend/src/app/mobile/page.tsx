"use client";

import Link from "next/link";
import MobileShell from "@/components/mobile/mobile-shell";
import { Card, CardContent } from "@/components/ui/card";
import { ArrowRight, Box, ClipboardCheck, CornerDownLeft, Move, PackageOpen, RefreshCw, Layers } from "lucide-react";

export default function MobileMenuPage() {
  const menuItems = [
    {
      title: "Nhận hàng (Inbound)",
      description: "Nhập kho thực tế từ PO",
      icon: <CornerDownLeft className="h-6 w-6 text-emerald-500" />,
      href: "/mobile/receiving",
      disabled: true, // Placeholder cho phase sau
    },
    {
      title: "Dịch chuyển (Movement)",
      description: "Chuyển vị trí kệ tồn kho",
      icon: <Move className="h-6 w-6 text-blue-500" />,
      href: "/mobile/movement",
      disabled: false,
    },
    {
      title: "Lấy hàng (Picking)",
      description: "Lấy hàng xuất từ đơn xuất",
      icon: <ClipboardCheck className="h-6 w-6 text-orange-500" />,
      href: "/mobile/picking",
      disabled: false,
    },
    {
      title: "Bổ sung (Replenishment)",
      description: "Bổ sung hàng kệ Pick Face hụt",
      icon: <RefreshCw className="h-6 w-6 text-emerald-500" />,
      href: "/mobile/replenishment",
      disabled: false,
    },
    {
      title: "Di chuyển Pallet (LPN)",
      description: "Quét di chuyển nguyên khối Pallet",
      icon: <Layers className="h-6 w-6 text-indigo-500" />,
      href: "/mobile/lpn",
      disabled: false,
    },
    {
      title: "Kiểm kê (Cycle count)",
      description: "Thực hiện kiểm đếm thực tế",
      icon: <Box className="h-6 w-6 text-yellow-500" />,
      href: "/mobile/counting",
      disabled: true,
    },
    {
      title: "Đóng gói (Packing)",
      description: "Đóng thùng dán tem xuất",
      icon: <PackageOpen className="h-6 w-6 text-purple-500" />,
      href: "/mobile/packing",
      disabled: true,
    },
  ];

  return (
    <MobileShell>
      <div className="space-y-4">
        <div className="text-center py-2">
          <h2 className="text-xl font-bold">Danh mục chức năng</h2>
          <p className="text-xs text-slate-400">Chọn nhiệm vụ thao tác kho cầm tay</p>
        </div>

        <div className="grid grid-cols-1 gap-3">
          {menuItems.map((item, idx) => {
            const cardContent = (
              <Card className={`border-slate-800 bg-slate-800/50 hover:bg-slate-800 transition ${item.disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}>
                <CardContent className="p-4 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="p-2 bg-slate-900 rounded-lg">{item.icon}</div>
                    <div className="text-left">
                      <div className="font-semibold text-sm text-white">{item.title}</div>
                      <div className="text-xs text-slate-400">{item.description}</div>
                    </div>
                  </div>
                  {!item.disabled && <ArrowRight className="h-4 w-4 text-slate-500" />}
                </CardContent>
              </Card>
            );

            if (item.disabled) {
              return <div key={idx}>{cardContent}</div>;
            }

            return (
              <Link href={item.href} key={idx}>
                {cardContent}
              </Link>
            );
          })}
        </div>
      </div>
    </MobileShell>
  );
}
