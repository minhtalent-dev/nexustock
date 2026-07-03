"use client";

import { usePathname } from "next/navigation";
import { Fragment, useMemo } from "react";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";

const labelMap: Record<string, string> = {
  "": "Trang chủ",
  "master-data": "Master data",
  "products": "Vật tư",
  "uoms": "Đơn vị tính",
  "warehouses": "Nhà kho",
  "zones": "Vùng kho",
  "locations": "Vị trí kệ",
  "partners": "Đối tác",
  "reasons": "Mã lý do",
  "import": "Nhập dữ liệu",
  "health-ui": "Sức khỏe hệ thống",
};

export default function BreadcrumbNav() {
  const pathname = usePathname();

  const crumbs = useMemo(() => {
    const segments = pathname.split("/").filter(Boolean);
    if (segments.length === 0) {
      return [{ href: "/", label: "Trang chủ", isLast: true }];
    }
    return [
      { href: "/", label: "Trang chủ", isLast: false },
      ...segments.map((seg, i) => {
        const href = "/" + segments.slice(0, i + 1).join("/");
        const isLast = i === segments.length - 1;
        return {
          href,
          label: labelMap[seg] ?? seg.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase()),
          isLast,
        };
      }),
    ];
  }, [pathname]);

  return (
    <Breadcrumb className="mb-4">
      <BreadcrumbList>
        {crumbs.map((crumb) => (
          <Fragment key={crumb.href}>
            <BreadcrumbItem>
              {crumb.isLast ? (
                <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
              ) : (
                <BreadcrumbLink href={crumb.href}>{crumb.label}</BreadcrumbLink>
              )}
            </BreadcrumbItem>
            {!crumb.isLast && <BreadcrumbSeparator />}
          </Fragment>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  );
}
