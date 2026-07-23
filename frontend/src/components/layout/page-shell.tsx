import { cn } from "@/lib/utils";

type PageShellProps = {
  title?: React.ReactNode;
  description?: React.ReactNode;
  actions?: React.ReactNode;
  filters?: React.ReactNode;
  variant?: "admin" | "mobile";
  className?: string;
  children: React.ReactNode;
};

/** Content-only shell — layout đã có p-6 + breadcrumb. */
export function PageShell({
  title,
  description,
  actions,
  filters,
  variant = "admin",
  className,
  children,
}: PageShellProps) {
  return (
    <div
      data-slot="page-shell"
      data-variant={variant}
      className={cn(
        "flex flex-col gap-4 animate-in fade-in-0 duration-200",
        variant === "mobile" && "gap-3",
        className
      )}
    >
      {(title || actions) && (
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            {title ? (
              <h1
                className={cn(
                  "font-heading font-semibold tracking-tight text-foreground",
                  variant === "mobile" ? "text-lg" : "text-2xl"
                )}
              >
                {title}
              </h1>
            ) : null}
            {description ? (
              <p className="text-sm text-muted-foreground text-balance">{description}</p>
            ) : null}
          </div>
          {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
        </div>
      )}
      {filters ? <div className="w-full">{filters}</div> : null}
      {children}
    </div>
  );
}
