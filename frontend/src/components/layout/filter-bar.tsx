import { cn } from "@/lib/utils";

type FilterBarProps = {
  className?: string;
  children: React.ReactNode;
};

export function FilterBar({ className, children }: FilterBarProps) {
  return (
    <div
      data-slot="filter-bar"
      className={cn(
        "flex flex-wrap items-end gap-2 rounded-lg border border-border bg-card/40 p-3",
        className
      )}
    >
      {children}
    </div>
  );
}
