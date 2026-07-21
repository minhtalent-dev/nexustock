import Link from "next/link";
import { getTranslations } from "next-intl/server";
import AppSidebar from "@/components/app-sidebar";
import BreadcrumbNav from "@/components/breadcrumb-nav";

export default async function Home() {
  const t = await getTranslations("Home");
  const tc = await getTranslations("Common.states");

  return (
    <div className="flex h-screen w-screen bg-[#0a0a0a] text-zinc-100 font-sans overflow-hidden">
      <AppSidebar />

      <main className="flex-1 flex flex-col overflow-y-auto">
        <header className="h-16 border-b border-zinc-800/80 bg-[#111]/40 backdrop-blur flex items-center justify-between px-8">
          <h2 className="text-sm font-medium text-zinc-400">{t("dashboardTitle")}</h2>
        </header>

        <div className="p-8 flex flex-col gap-8 max-w-5xl w-full mx-auto">
          <div>
            <BreadcrumbNav />
            <h1 className="text-3xl font-bold tracking-tight text-white">{t("welcome")}</h1>
            <p className="text-sm text-zinc-400 mt-1">{t("subtitle")}</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2">
              <span className="text-xs text-zinc-550 uppercase tracking-wider font-semibold">{t("systemStatus")}</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-emerald-400">{tc("online")}</span>
                <Link href="/health-ui" className="text-xs text-emerald-400 hover:underline flex items-center gap-1">
                  {t("details")} &rarr;
                </Link>
              </div>
            </div>

            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2 hover:border-zinc-700 transition-colors">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">{t("productsTotal")}</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-white">{t("active")}</span>
                <Link href="/master-data/products" className="text-xs text-emerald-400 hover:underline flex items-center gap-1">
                  {t("manage")} &rarr;
                </Link>
              </div>
            </div>

            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2 hover:border-zinc-700 transition-colors">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">{t("locations")}</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-white">{t("active")}</span>
                <Link href="/master-data/locations" className="text-xs text-emerald-400 hover:underline flex items-center gap-1">
                  {t("manage")} &rarr;
                </Link>
              </div>
            </div>
          </div>

          <div className="bg-gradient-to-r from-emerald-950/20 to-zinc-900 border border-emerald-900/30 p-8 rounded-2xl flex flex-col gap-4">
            <h3 className="text-lg font-semibold text-white">{t("phaseGuideTitle")}</h3>
            <p className="text-sm text-zinc-300 leading-relaxed max-w-2xl">{t("phaseGuideBody")}</p>
            <div className="flex items-center gap-4 mt-2">
              <Link
                href="/master-data/products"
                className="inline-flex items-center justify-center px-4 py-2 text-sm font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg transition-colors"
              >
                {t("manageCatalog")}
              </Link>
              <Link
                href="/health-ui"
                className="inline-flex items-center justify-center px-4 py-2 text-sm font-medium border border-zinc-800 hover:bg-zinc-800 text-zinc-300 rounded-lg transition-colors"
              >
                {t("systemHealth")}
              </Link>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
