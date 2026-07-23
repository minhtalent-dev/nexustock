import AppSidebar from "@/components/app-sidebar";
import BreadcrumbNav from "@/components/breadcrumb-nav";

export default function MasterDataLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-screen bg-background">
      <AppSidebar />
      <main className="flex-1 overflow-auto p-6">
        <BreadcrumbNav />
        {children}
      </main>
    </div>
  );
}
