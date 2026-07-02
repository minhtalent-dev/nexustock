import Link from "next/link";
import { LayoutDashboard, Activity, Package, MapPin, Shield } from "lucide-react";

export default function Home() {
  return (
    <div className="flex h-screen w-screen bg-[#0a0a0a] text-zinc-100 font-sans overflow-hidden">
      {/* Sidebar */}
      <aside className="w-64 border-r border-zinc-800/80 bg-[#111] flex flex-col justify-between p-4 shrink-0">
        <div className="flex flex-col gap-6">
          {/* Logo */}
          <div className="flex items-center gap-3 px-2 py-1.5 border-b border-zinc-850 pb-4">
            <div className="h-9 w-9 rounded-lg bg-emerald-500/10 border border-emerald-500/25 flex items-center justify-center">
              <Shield className="h-5 w-5 text-emerald-500" />
            </div>
            <span className="font-bold text-lg text-white tracking-tight">Nexustock WMS</span>
          </div>

          {/* Navigation Links */}
          <nav className="flex flex-col gap-1.5">
            <Link
              href="/"
              className="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg bg-zinc-800/60 text-white border border-zinc-750"
            >
              <LayoutDashboard className="h-4 w-4 text-emerald-400" />
              Trang chủ
            </Link>

            <Link
              href="/health-ui"
              className="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-900 transition-colors"
            >
              <Activity className="h-4 w-4" />
              Giám sát hệ thống
            </Link>

            <Link
              href="/products"
              className="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-900 transition-colors pointer-events-none opacity-50"
            >
              <Package className="h-4 w-4" />
              Sản phẩm (Phase 02)
            </Link>

            <Link
              href="/locations"
              className="flex items-center gap-3 px-3 py-2 text-sm font-medium rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-900 transition-colors pointer-events-none opacity-50"
            >
              <MapPin className="h-4 w-4" />
              Vị trí kho (Phase 02)
            </Link>
          </nav>
        </div>

        {/* Footer info */}
        <div className="px-2 py-3 border-t border-zinc-800/40 text-xs text-zinc-500">
          <span>Phiên bản v0.1.0 (Phase 01)</span>
        </div>
      </aside>

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
            <h1 className="text-3xl font-bold tracking-tight text-white">Chào mừng tới Nexustock</h1>
            <p className="text-sm text-zinc-400 mt-1">Hệ thống quản lý kho thông minh (WMS) thiết kế Modular Monolith.</p>
          </div>

          {/* Quick Stats Placeholder */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">Trạng thái hệ thống</span>
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

            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2 opacity-50">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">Tổng sản phẩm</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-white">—</span>
                <span className="text-xs text-zinc-500">Chưa bắt đầu</span>
              </div>
            </div>

            <div className="bg-[#111] border border-zinc-800/60 p-6 rounded-xl flex flex-col gap-2 opacity-50">
              <span className="text-xs text-zinc-500 uppercase tracking-wider font-semibold">Vị trí lưu trữ</span>
              <div className="flex items-center justify-between mt-1">
                <span className="text-2xl font-bold text-white">—</span>
                <span className="text-xs text-zinc-500">Chưa bắt đầu</span>
              </div>
            </div>
          </div>

          {/* Guide Card */}
          <div className="bg-gradient-to-r from-emerald-950/20 to-zinc-900 border border-emerald-900/30 p-8 rounded-2xl flex flex-col gap-4">
            <h3 className="text-lg font-semibold text-white">Bạn đang ở Phase 01: Project foundation</h3>
            <p className="text-sm text-zinc-300 leading-relaxed max-w-2xl">
              Hệ thống đã thiết lập xong cấu trúc thư mục Monorepo, nạp biến môi trường tự động từ file .env cục bộ, 
              và triển khai thành công hệ thống giám sát sức khỏe kết nối thời gian thực đến cơ sở dữ liệu PostgreSQL 
              và bộ nhớ đệm Redis trên Portainer.
            </p>
            <div className="flex items-center gap-4 mt-2">
              <Link
                href="/health-ui"
                className="inline-flex items-center justify-center px-4 py-2 text-sm font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg transition-colors"
              >
                Mở Health Dashboard
              </Link>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
