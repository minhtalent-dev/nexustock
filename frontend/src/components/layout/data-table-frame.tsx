import { cn } from "@/lib/utils";
import { EmptyState } from "@/components/states/empty-state";
import { LoadingState } from "@/components/states/loading-state";
import { ErrorState } from "@/components/states/error-state";

type DataTableFrameProps = {
  loading?: boolean;
  empty?: boolean;
  error?: string | null;
  onRetry?: () => void;
  emptyTitle?: string;
  emptyDescription?: string;
  emptyAction?: React.ReactNode;
  className?: string;
  children: React.ReactNode;
};

export function DataTableFrame({
  loading,
  empty,
  error,
  onRetry,
  emptyTitle = "No data",
  emptyDescription,
  emptyAction,
  className,
  children,
}: DataTableFrameProps) {
  if (error) {
    return <ErrorState message={error} onRetry={onRetry} />;
  }
  if (loading) {
    return <LoadingState />;
  }
  if (empty) {
    return (
      <EmptyState title={emptyTitle} description={emptyDescription} action={emptyAction} />
    );
  }
  return (
    <div
      data-slot="data-table-frame"
      className={cn("overflow-x-auto rounded-lg border border-border bg-card", className)}
    >
      {children}
    </div>
  );
}
