"use client";

import { usePathname } from "next/navigation";
import { Fragment, useMemo } from "react";
import { useTranslations } from "next-intl";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";

export default function BreadcrumbNav() {
  const pathname = usePathname();
  const t = useTranslations("Breadcrumb");

  const crumbs = useMemo(() => {
    const segments = pathname.split("/").filter(Boolean);
    /** URL kebab (master-data) → key camelCase (masterData). */
    const segmentToKey = (seg: string) =>
      seg.replace(/-([a-z])/g, (_, c: string) => c.toUpperCase());
    const labelFor = (seg: string) => {
      try {
        return t(segmentToKey(seg) as never);
      } catch {
        return seg.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
      }
    };

    if (segments.length === 0) {
      return [{ href: "/", label: t("home"), isLast: true }];
    }
    return [
      { href: "/", label: t("home"), isLast: false },
      ...segments.map((seg, i) => {
        const href = "/" + segments.slice(0, i + 1).join("/");
        const isLast = i === segments.length - 1;
        return {
          href,
          label: labelFor(seg),
          isLast,
        };
      }),
    ];
  }, [pathname, t]);

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
