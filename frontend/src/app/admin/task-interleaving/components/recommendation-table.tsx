"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { TaskRecommendationListItemDto } from "@/lib/task-interleaving-api";
import { Eye } from "lucide-react";

type TableProps = {
  items: TaskRecommendationListItemDto[];
  onViewDetail: (id: string) => void;
};

export function RecommendationTable({ items, onViewDetail }: TableProps) {
  const t = useTranslations("Admin.taskInterleaving");
  const tc = useTranslations("Admin.common");

  const getStatusBadge = (status: string) => {
    const label = t(`recommendationStatus.${status}` as "recommendationStatus.Open");

    switch (status) {
      case "Accepted":
        return <Badge className="bg-emerald-500 hover:bg-emerald-600 text-foreground border-0">{label}</Badge>;
      case "Rejected":
        return <Badge variant="destructive">{label}</Badge>;
      case "Expired":
        return <Badge variant="secondary">{label}</Badge>;
      case "Superseded":
        return <Badge className="bg-amber-500 hover:bg-amber-600 text-foreground border-0">{label}</Badge>;
      case "NoCandidate":
        return <Badge variant="outline">{label}</Badge>;
      default:
        return <Badge variant="outline">{label}</Badge>;
    }
  };

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  };

  return (
    <div className="rounded-md border bg-card">
      <Table id="task-interleaving-recommendation-table">
        <TableHeader>
          <TableRow>
            <TableHead>{t("colCreatedAt")}</TableHead>
            <TableHead>{t("colUserId")}</TableHead>
            <TableHead>{t("colSourceTask")}</TableHead>
            <TableHead>{t("colSuggestedTask")}</TableHead>
            <TableHead>{t("colScore")}</TableHead>
            <TableHead>{t("colStatus")}</TableHead>
            <TableHead>{t("colReason")}</TableHead>
            <TableHead className="text-right">{tc("actions")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.length === 0 ? (
            <TableRow>
              <TableCell colSpan={8} className="text-center py-8 text-muted-foreground">
                {t("emptyTable")}
              </TableCell>
            </TableRow>
          ) : (
            items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="font-medium">{formatDate(item.createdAt)}</TableCell>
                <TableCell className="font-mono text-xs">{item.userId.substring(0, 8)}...</TableCell>
                <TableCell>
                  {item.sourceTaskId ? (
                    <span className="text-xs">
                      {item.sourceTaskType} ({item.sourceTaskId.substring(0, 8)}...)
                    </span>
                  ) : (
                    <span className="text-muted-foreground text-xs">--</span>
                  )}
                </TableCell>
                <TableCell>
                  {item.selectedTaskId ? (
                    <span className="text-xs">
                      {item.selectedTaskType} ({item.selectedTaskId.substring(0, 8)}...)
                    </span>
                  ) : (
                    <span className="text-muted-foreground text-xs">--</span>
                  )}
                </TableCell>
                <TableCell className="font-mono text-xs">
                  {item.selectedScore ? item.selectedScore.toFixed(1) : "--"}
                </TableCell>
                <TableCell>{getStatusBadge(item.status)}</TableCell>
                <TableCell>
                  {item.reasonCode ? (
                    <Badge variant="outline" className="text-xs font-mono">
                      {item.reasonCode}
                    </Badge>
                  ) : (
                    <span className="text-muted-foreground text-xs">--</span>
                  )}
                </TableCell>
                <TableCell className="text-right">
                  <Button
                    id="task-interleaving-detail-button"
                    variant="ghost"
                    size="icon"
                    onClick={() => onViewDetail(item.id)}
                  >
                    <Eye className="size-4" />
                  </Button>
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
