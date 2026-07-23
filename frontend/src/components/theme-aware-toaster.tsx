"use client";

import { useTheme } from "next-themes";
import { Toaster } from "sonner";

export function ThemeAwareToaster() {
  const { resolvedTheme } = useTheme();
  return (
    <Toaster
      position="bottom-right"
      theme={resolvedTheme === "dark" ? "dark" : "light"}
      closeButton
      richColors
    />
  );
}
