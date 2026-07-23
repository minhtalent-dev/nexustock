import { Spinner } from "@/components/ui/spinner";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

type LoadingStateProps = {
  rows?: number;
  className?: string;
  variant?: "spinner" | "skeleton";
};

export function LoadingState({ rows = 5, className, variant = "skeleton" }: LoadingStateProps) {
  if (variant === "spinner") {
    return (
      <div className={cn("flex min-h-40 items-center justify-center", className)}>
        <Spinner className="size-6" />
      </div>
    );
  }
  return (
    <div className={cn("space-y-2 rounded-lg border border-border bg-card p-4", className)}>
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-8 w-full" />
      ))}
    </div>
  );
}
