/** Preference chế độ nhóm sidebar — localStorage, SSR-safe default modules. */

export type NavMode = "modules" | "ops";

export const NAV_MODE_KEY = "nexustock:sidebar:navMode";

export function loadNavMode(): NavMode {
  if (typeof window === "undefined") return "modules";
  try {
    const value = localStorage.getItem(NAV_MODE_KEY);
    return value === "ops" ? "ops" : "modules";
  } catch {
    return "modules";
  }
}

export function saveNavMode(mode: NavMode): void {
  try {
    localStorage.setItem(NAV_MODE_KEY, mode);
  } catch {
    // quota / private mode — bỏ qua
  }
}

export function collapseKey(mode: NavMode, titleKey: string): string {
  return `${mode}:${titleKey}`;
}
