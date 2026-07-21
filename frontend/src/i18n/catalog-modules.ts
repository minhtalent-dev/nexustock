/** Danh sách module catalog PascalCase = tên file JSON (không .json). */
export const CATALOG_MODULES = [
  'Common',
  'Language',
  'Sidebar',
  'Breadcrumb',
  'Errors',
  'Home',
  'Login',
  'HealthUi',
  'Admin',
  'Features',
  'MasterData',
] as const;

export type CatalogModule = (typeof CATALOG_MODULES)[number];
