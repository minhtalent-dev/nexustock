"use client";

import { createContext, useCallback, useContext, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

type ConfirmDialogOptions = {
  title?: string;
  description?: string;
  confirmText?: string;
  cancelText?: string;
  tone?: "default" | "danger";
};

type PendingConfirm = Required<ConfirmDialogOptions> & {
  resolve: (value: boolean) => void;
};

const ConfirmDialogContext = createContext<((options?: ConfirmDialogOptions) => Promise<boolean>) | null>(null);

export function ConfirmDialogProvider({ children }: { children: React.ReactNode }) {
  const [pending, setPending] = useState<PendingConfirm | null>(null);
  const tConfirm = useTranslations("Common.confirm");
  const tActions = useTranslations("Common.actions");

  const confirm = useCallback(
    (options?: ConfirmDialogOptions) => {
      return new Promise<boolean>((resolve) => {
        setPending({
          title: options?.title ?? tConfirm("title"),
          description: options?.description ?? tConfirm("description"),
          confirmText: options?.confirmText ?? tActions("confirm"),
          cancelText: options?.cancelText ?? tActions("cancel"),
          tone: options?.tone ?? "default",
          resolve,
        });
      });
    },
    [tConfirm, tActions]
  );

  const close = useCallback(
    (value: boolean) => {
      pending?.resolve(value);
      setPending(null);
    },
    [pending]
  );

  const value = useMemo(() => confirm, [confirm]);

  return (
    <ConfirmDialogContext.Provider value={value}>
      {children}
      <AlertDialog open={Boolean(pending)} onOpenChange={(open) => !open && close(false)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{pending?.title}</AlertDialogTitle>
            <AlertDialogDescription>{pending?.description}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => close(false)}>{pending?.cancelText}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => close(true)}
              className={pending?.tone === "danger" ? "bg-destructive text-white hover:bg-destructive/90" : undefined}
            >
              {pending?.confirmText}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </ConfirmDialogContext.Provider>
  );
}

export function useConfirmDialog() {
  const confirm = useContext(ConfirmDialogContext);
  if (!confirm) {
    throw new Error("useConfirmDialog must be used within ConfirmDialogProvider");
  }
  return confirm;
}
