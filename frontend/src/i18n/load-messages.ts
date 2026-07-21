import type { AppLocale } from './config';
import { deepMerge } from './merge-messages';

import viCommon from '../../messages/vi/Common.json';
import viLanguage from '../../messages/vi/Language.json';
import viSidebar from '../../messages/vi/Sidebar.json';
import viBreadcrumb from '../../messages/vi/Breadcrumb.json';
import viErrors from '../../messages/vi/Errors.json';
import viHome from '../../messages/vi/Home.json';
import viLogin from '../../messages/vi/Login.json';
import viHealthUi from '../../messages/vi/HealthUi.json';
import viAdmin from '../../messages/vi/Admin.json';
import viFeatures from '../../messages/vi/Features.json';
import viMasterData from '../../messages/vi/MasterData.json';

import enCommon from '../../messages/en/Common.json';
import enLanguage from '../../messages/en/Language.json';
import enSidebar from '../../messages/en/Sidebar.json';
import enBreadcrumb from '../../messages/en/Breadcrumb.json';
import enErrors from '../../messages/en/Errors.json';
import enHome from '../../messages/en/Home.json';
import enLogin from '../../messages/en/Login.json';
import enHealthUi from '../../messages/en/HealthUi.json';
import enAdmin from '../../messages/en/Admin.json';
import enFeatures from '../../messages/en/Features.json';
import enMasterData from '../../messages/en/MasterData.json';

const CATALOGS: Record<AppLocale, Record<string, unknown>[]> = {
  vi: [
    viCommon,
    viLanguage,
    viSidebar,
    viBreadcrumb,
    viErrors,
    viHome,
    viLogin,
    viHealthUi,
    viAdmin,
    viFeatures,
    viMasterData,
  ] as Record<string, unknown>[],
  en: [
    enCommon,
    enLanguage,
    enSidebar,
    enBreadcrumb,
    enErrors,
    enHome,
    enLogin,
    enHealthUi,
    enAdmin,
    enFeatures,
    enMasterData,
  ] as Record<string, unknown>[],
};

/** Load và deep-merge toàn bộ module catalog cho locale (static import map). */
export function loadMessages(locale: AppLocale): Record<string, unknown> {
  return CATALOGS[locale].reduce(
    (acc, part) => deepMerge(acc, part),
    {} as Record<string, unknown>
  );
}
