# Project Index — Phase 39 Theme (rp4)

**Ngày:** 2026-07-23 · Scope: FE theme only

## Topology

| Layer | Artifact |
|---|---|
| Provider | `frontend/src/providers/theme-provider.tsx` → `next-themes` |
| Switch Admin | `theme-switcher.tsx` → `ThemeMenuSection` (horizontal) in `sidebar-user-menu.tsx` |
| Switch Mobile | `ThemeSwitcherInline` in `mobile-shell.tsx` |
| Toaster | `theme-aware-toaster.tsx` |
| Tokens | `globals.css` `:root` light + `.dark` |
| Persist | `localStorage` key `nexustock:theme` |
| Verify | `tests/verify_theme_classes.ps1` (incl. `a111`) |
| DBM | `tests/helpers/dbm_phase39_theme_browser.mjs` |

## Boundaries

- **In:** FE class theme, semantic migrate, Admin+Mobile switcher  
- **Out:** theme sync API, Option C brand, high-contrast, mass `ui/*` rewrite, backend  

## Evidence

- Plan: `planning/phases/phase_39_theme_light_dark_system.md`  
- Index: `planning/function_index_phase39_theme.md`  
- `planning/evidence/phase_39/` · `phase_39_dbm/` · `phase_39_rp45/`  

## Verdict reindex

**Khớp DoD §14 100%** — sẵn sàng `rp4` đóng + `rp5` xác nhận.
