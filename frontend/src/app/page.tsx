import Link from "next/link";
import { LayoutDashboard, Activity, Package, MapPin, Shield } from "lucide-react";
import AppSidebar from "@/components/app-sidebar";
import BreadcrumbNav from "@/components/breadcrumb-nav";

export default function Home() {
  return (
    <div className="flex h-screen w-screen bg-[#0a0a0a] text-zinc-100 font-sans overflow-hidden">
      {/* Sidebar */}
      <AppSidebar />

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col overflow-y-auto">
        {/* Topbar */}
        <header className="h-16 border-b border-zinc-800/80 bg-[#111]/40 backdrop-blur flex items-center justify-between px-8">
          <h2 className="text-sm font-medium text-zinc-400">Bảng điều khiển quản trị</h2>
          <div className="flex items-center gap-4">
            <span className="text-xs text-zinc-500 font-mono">ASPNETCORE_URLS: http://localhost:5024</span>
          </div>
        </header>

        {/* Content Body */}
        <div className="p-8 flex flex-col gap-8 max-w-5xl w-full mx-auto">
          <div>
            <BreadcrumbNav />
            <h1 className="text-3xl font-bold tracking-tight text-white">Chào mừng tới Nexustock</h1>
            <p className="text-sm text-zinc-400 mt-1">Hệ thống quản lý kho thông minh (WMS) thiết kế Modular Monolith.</p>
          </div>

          {/* Quick Stats Placeholder */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2">
              <span className="text-xs text-zinc-550 uppercase tracking-wider font-semibold">Trạng thái hệ thống</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-emerald-400">ONLINE</span>
                <Link
                  href="/health-ui"
                  className="text-xs text-emerald-400 hover:underline flex items-center gap-1"
                >
                  Chi tiết &rarr;
                </Link>
              </div>
            </div>

            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2 hover:border-zinc-700 transition-colors">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">Tổng sản phẩm</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-white">Kích hoạt</span>
                <Link
                  href="/master-data/products"
                  className="text-xs text-emerald-400 hover:underline flex items-center gap-1"
                >
                  Quản lý &rarr;
                </Link>
              </div>
            </div>

            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2 hover:border-zinc-700 transition-colors">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">Vị trí lưu trữ</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-white">Kích hoạt</span>
                <Link
                  href="/master-data/locations"
                  className="text-xs text-emerald-400 hover:underline flex items-center gap-1"
                >
                  Quản lý &rarr;
                </Link>
              </div>
            </div>
          </div>

          {/* Guide Card */}
          <div className="bg-gradient-to-r from-emerald-950/20 to-zinc-900 border border-emerald-900/30 p-8 rounded-2xl flex flex-col gap-4">
            <h3 className="text-lg font-semibold text-white">Bạn đang ở Phase 02: Master data foundation</h3>
            <p className="text-sm text-zinc-300 leading-relaxed max-w-2xl">
              Hệ thống đã hoàn thành thiết lập cơ sở dữ liệu Master Data bao gồm Vật tư, Đơn vị tính, Kho bãi, Vùng kho, 
              Vị trí kệ, Đối tác và Mã lý do. Quy trình nhập dữ liệu 2 bước (Preview & Commit nguyên khối) từ tệp Excel/CSV 
              đã hoạt động ổn định và tin cậy.
            </p>
            <div className="flex items-center gap-4 mt-2">
              <Link
                href="/master-data/products"
                className="inline-flex items-center justify-center px-4 py-2 text-sm font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg transition-colors"
              >
                Quản lý danh mục
              </Link>
              <Link
                href="/health-ui"
                className="inline-flex items-center justify-center px-4 py-2 text-sm font-medium border border-zinc-800 hover:bg-zinc-800 text-zinc-300 rounded-lg transition-colors"
              >
                Sức khỏe hệ thống
              </Link>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
