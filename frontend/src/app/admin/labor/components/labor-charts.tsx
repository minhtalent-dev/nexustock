"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  Cell,
  LabelList,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  XAxis,
  YAxis,
} from "recharts";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  ChartConfig,
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
} from "@/components/ui/chart";
import { LaborKpiPointDto } from "@/lib/labor-api";

type ChartProps = {
  data: LaborKpiPointDto[];
  loading?: boolean;
};

export function LaborThroughputTrendChart({ data, loading }: ChartProps) {
  const t = useTranslations("Admin.labor");

  const throughputConfig = {
    value: {
      label: t("chartCompletedTasks"),
      color: "hsl(var(--primary))",
    },
  } satisfies ChartConfig;

  if (loading) return <div className="h-[300px] w-full animate-pulse bg-muted rounded-lg" />;
  if (!data || data.length === 0) {
    return (
      <div className="flex h-[300px] items-center justify-center border border-dashed rounded-lg text-muted-foreground">
        {t("noThroughputData")}
      </div>
    );
  }

  const formattedData = data.map((d) => ({
    ...d,
    shortLabel: d.label.length > 10 ? d.label.substring(5) : d.label,
  }));

  return (
    <Card className="col-span-3">
      <CardHeader>
        <CardTitle>{t("throughputTitle")}</CardTitle>
        <CardDescription>{t("throughputDesc")}</CardDescription>
      </CardHeader>
      <CardContent>
        <ChartContainer config={throughputConfig} className="h-[300px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={formattedData} margin={{ top: 20, right: 20, left: -10, bottom: 0 }}>
              <XAxis dataKey="shortLabel" tickLine={false} axisLine={false} tickMargin={8} />
              <YAxis tickLine={false} axisLine={false} tickMargin={8} />
              <ChartTooltip content={<ChartTooltipContent />} />
              <Line
                type="monotone"
                dataKey="value"
                stroke="var(--color-value)"
                strokeWidth={2}
                dot={{ r: 4 }}
                activeDot={{ r: 6 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </ChartContainer>
      </CardContent>
    </Card>
  );
}

export function LaborTasksPerHourTrendChart({ data, loading }: ChartProps) {
  const t = useTranslations("Admin.labor");

  const tphConfig = {
    value: {
      label: t("chartTasksPerHour"),
      color: "hsl(var(--chart-2, 142 70% 45%))",
    },
  } satisfies ChartConfig;

  if (loading) return <div className="h-[300px] w-full animate-pulse bg-muted rounded-lg" />;
  if (!data || data.length === 0) {
    return (
      <div className="flex h-[300px] items-center justify-center border border-dashed rounded-lg text-muted-foreground">
        {t("noTphData")}
      </div>
    );
  }

  const formattedData = data.map((d) => ({
    ...d,
    shortLabel: d.label.length > 10 ? d.label.substring(5) : d.label,
  }));

  return (
    <Card className="col-span-3">
      <CardHeader>
        <CardTitle>{t("tphTitle")}</CardTitle>
        <CardDescription>{t("tphDesc")}</CardDescription>
      </CardHeader>
      <CardContent>
        <ChartContainer config={tphConfig} className="h-[300px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={formattedData} margin={{ top: 20, right: 20, left: -10, bottom: 0 }}>
              <defs>
                <linearGradient id="colorTph" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="var(--color-value)" stopOpacity={0.8} />
                  <stop offset="95%" stopColor="var(--color-value)" stopOpacity={0.1} />
                </linearGradient>
              </defs>
              <XAxis dataKey="shortLabel" tickLine={false} axisLine={false} tickMargin={8} />
              <YAxis tickLine={false} axisLine={false} tickMargin={8} />
              <ChartTooltip content={<ChartTooltipContent />} />
              <Area
                type="monotone"
                dataKey="value"
                stroke="var(--color-value)"
                fillOpacity={1}
                fill="url(#colorTph)"
              />
            </AreaChart>
          </ResponsiveContainer>
        </ChartContainer>
      </CardContent>
    </Card>
  );
}

const operationMixColors = [
  "hsl(var(--chart-1, 221 83% 53%))",
  "hsl(var(--chart-2, 142 70% 45%))",
  "hsl(var(--chart-3, 47 95% 45%))",
  "hsl(var(--chart-4, 346 87% 43%))",
  "hsl(var(--chart-5, 262 80% 50%))",
];

const mixConfig = {} satisfies ChartConfig;

export function LaborOperationMixChart({ data, loading }: ChartProps) {
  const t = useTranslations("Admin.labor");

  if (loading) return <div className="h-[300px] w-full animate-pulse bg-muted rounded-lg" />;
  if (!data || data.length === 0) {
    return (
      <div className="flex h-[300px] items-center justify-center border border-dashed rounded-lg text-muted-foreground">
        {t("noOperationMixData")}
      </div>
    );
  }

  return (
    <Card className="col-span-2">
      <CardHeader>
        <CardTitle>{t("operationMixTitle")}</CardTitle>
        <CardDescription>{t("operationMixDesc")}</CardDescription>
      </CardHeader>
      <CardContent className="flex items-center justify-center">
        <ChartContainer config={mixConfig} className="h-[250px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <ChartTooltip
                cursor={false}
                content={<ChartTooltipContent hideLabel />}
              />
              <Pie
                data={data}
                dataKey="value"
                nameKey="label"
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={85}
                paddingAngle={4}
              >
                {data.map((_, index) => (
                  <Cell
                    key={`cell-${index}`}
                    fill={operationMixColors[index % operationMixColors.length]}
                  />
                ))}
              </Pie>
            </PieChart>
          </ResponsiveContainer>
        </ChartContainer>
        <div className="flex flex-col gap-2 ml-4 text-xs">
          {data.map((item, index) => (
            <div key={item.label} className="flex items-center gap-2">
              <div
                className="h-3 w-3 rounded-full"
                style={{ backgroundColor: operationMixColors[index % operationMixColors.length] }}
              />
              <span className="font-medium text-muted-foreground">{item.label}</span>
              <span className="text-foreground ml-auto font-bold">{item.value}</span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

export function LaborUserProductivityChart({ data, loading }: ChartProps) {
  const t = useTranslations("Admin.labor");

  const rankingConfig = {
    value: {
      label: t("chartTph"),
      color: "hsl(var(--primary))",
    },
  } satisfies ChartConfig;

  if (loading) return <div className="h-[350px] w-full animate-pulse bg-muted rounded-lg" />;
  if (!data || data.length === 0) {
    return (
      <div className="flex h-[350px] items-center justify-center border border-dashed rounded-lg text-muted-foreground">
        {t("noUserProductivityData")}
      </div>
    );
  }

  return (
    <Card className="col-span-3">
      <CardHeader>
        <CardTitle>{t("userProductivityTitle")}</CardTitle>
        <CardDescription>{t("userProductivityDesc")}</CardDescription>
      </CardHeader>
      <CardContent>
        <ChartContainer config={rankingConfig} className="h-[280px] w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={data}
              layout="vertical"
              margin={{ top: 5, right: 30, left: 40, bottom: 5 }}
            >
              <XAxis type="number" tickLine={false} axisLine={false} />
              <YAxis
                dataKey="label"
                type="category"
                tickLine={false}
                axisLine={false}
                width={80}
              />
              <ChartTooltip content={<ChartTooltipContent />} />
              <Bar dataKey="value" fill="var(--color-value)" radius={[0, 4, 4, 0]}>
                <LabelList dataKey="value" position="right" className="fill-foreground text-xs" />
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </ChartContainer>
      </CardContent>
    </Card>
  );
}

export function LaborZoneProductivityGrid({ data, loading }: ChartProps) {
  const t = useTranslations("Admin.labor");

  if (loading) return <div className="h-[350px] w-full animate-pulse bg-muted rounded-lg" />;
  if (!data || data.length === 0) {
    return (
      <div className="flex h-[350px] items-center justify-center border border-dashed rounded-lg text-muted-foreground">
        {t("noZoneProductivityData")}
      </div>
    );
  }

  return (
    <Card className="col-span-3">
      <CardHeader>
        <CardTitle>{t("zoneProductivityTitle")}</CardTitle>
        <CardDescription>{t("zoneProductivityDesc")}</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
          {data.map((item) => (
            <div
              key={item.label}
              className="flex flex-col justify-between p-4 border rounded-xl bg-card hover:bg-accent/30 transition-colors"
            >
              <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                {item.label === "null" || !item.label ? t("unassigned") : item.label}
              </span>
              <div className="flex items-baseline gap-1 mt-2">
                <span className="text-2xl font-bold text-foreground">
                  {item.value}
                </span>
                <span className="text-xs text-muted-foreground">{t("secPerTask")}</span>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
