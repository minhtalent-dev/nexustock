"use client";

import { PageShell } from "@/components/layout/page-shell";

import Link from "next/link";
import { useTranslations } from "next-intl";
import MobileShell from "@/components/mobile/mobile-shell";
import { Card, CardContent } from "@/components/ui/card";
import { ArrowRight, Box, ClipboardCheck, CornerDownLeft, Move, PackageOpen, RefreshCw, Layers, Smartphone, ListOrdered } from "lucide-react";

export default function MobileMenuPage() {
  const t = useTranslations("Mobile.home");

  const menuItems = [
    {
      title: t("items.nextTask.title"),
      description: t("items.nextTask.description"),
      icon: <ListOrdered className="h-6 w-6 text-cyan-500" />,
      href: "/mobile/tasks/next",
      disabled: false,
    },
    {
      title: t("items.inbound.title"),
      description: t("items.inbound.description"),
      icon: <CornerDownLeft className="h-6 w-6 text-emerald-500" />,
      href: "/mobile/receiving",
      disabled: true,
    },
    {
      title: t("items.movement.title"),
      description: t("items.movement.description"),
      icon: <Move className="h-6 w-6 text-blue-500" />,
      href: "/mobile/movement",
      disabled: false,
    },
    {
      title: t("items.picking.title"),
      description: t("items.picking.description"),
      icon: <ClipboardCheck className="h-6 w-6 text-orange-500" />,
      href: "/mobile/picking",
      disabled: false,
    },
    {
      title: t("items.replenishment.title"),
      description: t("items.replenishment.description"),
      icon: <RefreshCw className="h-6 w-6 text-emerald-500" />,
      href: "/mobile/replenishment",
      disabled: false,
    },
    {
      title: t("items.lpn.title"),
      description: t("items.lpn.description"),
      icon: <Layers className="h-6 w-6 text-indigo-500" />,
      href: "/mobile/lpn",
      disabled: false,
    },
    {
      title: t("items.serial.title"),
      description: t("items.serial.description"),
      icon: <Smartphone className="h-6 w-6 text-emerald-500" />,
      href: "/mobile/serial",
      disabled: false,
    },
    {
      title: t("items.counting.title"),
      description: t("items.counting.description"),
      icon: <Box className="h-6 w-6 text-yellow-500" />,
      href: "/mobile/counting",
      disabled: true,
    },
    {
      title: t("items.packing.title"),
      description: t("items.packing.description"),
      icon: <PackageOpen className="h-6 w-6 text-purple-500" />,
      href: "/mobile/packing",
      disabled: true,
    },
  ];

  return (
    <PageShell className="gap-6">
      <MobileShell>
      <div className="space-y-4">
        <div className="text-center py-2">
          <h2 className="text-xl font-bold">{t("page.title")}</h2>
          <p className="text-xs text-muted-foreground">{t("page.subtitle")}</p>
        </div>

        <div className="grid grid-cols-1 gap-3">
          {menuItems.map((item, idx) => {
            const cardContent = (
              <Card className={`border-border bg-muted/50 hover:bg-muted transition ${item.disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}>
                <CardContent className="p-4 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="p-2 bg-background rounded-lg">{item.icon}</div>
                    <div className="text-left">
                      <div className="font-semibold text-sm text-foreground">{item.title}</div>
                      <div className="text-xs text-muted-foreground">{item.description}</div>
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
    </PageShell>
  );
}
