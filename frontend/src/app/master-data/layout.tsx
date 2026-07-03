import AppSidebar from "@/components/app-sidebar";
import BreadcrumbNav from "@/components/breadcrumb-nav";

export default function MasterDataLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-screen bg-[#0a0a0a]">
      <AppSidebar />
      <main className="flex-1 p-6 overflow-auto">
        <BreadcrumbNav />
        {children}
      </main>
    </div>
  );
}
