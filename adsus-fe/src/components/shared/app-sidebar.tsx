"use client";

import { BrainCircuit, CalendarClock, FileText, KeyRound, LogOut, LayoutDashboard, ClipboardList, Users, Pill, Truck, PackagePlus, Receipt } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";
import { useUiStore } from "@/store/ui-store";

export function AppSidebar() {
  const router = useRouter();
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);
  const signOut = useAuthStore((s) => s.signOut);
  const expanded = useUiStore((s) => s.sidebarExpanded);

  function handleSignOut() {
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    signOut();
    router.replace("/login");
  }

  return (
    <aside 
      className={`sticky top-16 z-30 flex h-[calc(100vh-4rem)] flex-col border-r border-border bg-background/95 backdrop-blur max-md:hidden transition-all duration-300 ease-in-out ${
        expanded ? "w-64" : "w-[68px]"
      }`}
    >
      <div className={`flex-1 overflow-y-auto ${expanded ? "p-4 space-y-1" : "p-3 space-y-2 flex flex-col items-center"}`}>
        {user?.role === "ADMIN" && (
          <>
            <NavItem expanded={expanded} href="/dashboard" icon={<LayoutDashboard className="size-5" />} label="Dashboard" active={pathname.startsWith("/dashboard")} />
            <NavItem expanded={expanded} href="/admin/users" icon={<Users className="size-5" />} label="Tài khoản" active={pathname.startsWith("/admin/users")} />
            <NavItem expanded={expanded} href="/admin/medicines" icon={<Pill className="size-5" />} label="Danh mục thuốc" active={pathname.startsWith("/admin/medicines")} />
            <NavItem expanded={expanded} href="/admin/suppliers" icon={<Truck className="size-5" />} label="Nhà cung cấp" active={pathname.startsWith("/admin/suppliers")} />
            <NavItem expanded={expanded} href="/inventory/import" icon={<PackagePlus className="size-5" />} label="Nhập kho" active={pathname.startsWith("/inventory/import")} />
            <NavItem expanded={expanded} href="/inventory" icon={<ClipboardList className="size-5" />} label="Lịch sử kho" active={pathname === "/inventory"} />
            <NavItem expanded={expanded} href="/admin/ai-models" icon={<BrainCircuit className="size-5" />} label="Mô hình AI" active={pathname.startsWith("/admin/ai-models")} />
            <NavItem expanded={expanded} href="/admin/blog" icon={<FileText className="size-5" />} label="Blog" active={pathname.startsWith("/admin/blog")} />
          </>
        )}

        {(user?.role === "DOCTOR" || user?.role === "NURSE") && (
          <NavItem expanded={expanded} href="/patients" icon={<ClipboardList className="size-5" />} label="Danh sách bệnh nhân" active={pathname.startsWith("/patients")} />
        )}

        {user?.role === "NURSE" && (
          <NavItem expanded={expanded} href="/invoices" icon={<Receipt className="size-5" />} label="Quản lý hóa đơn" active={pathname.startsWith("/invoices")} />
        )}

        {user?.role === "DOCTOR" && (
          <>
            <NavItem expanded={expanded} href="/schedule" icon={<CalendarClock className="size-5" />} label="Quản lý lịch" active={pathname.startsWith("/schedule") && !pathname.startsWith("/schedule/patients")} />
            <NavItem expanded={expanded} href="/schedule/patients" icon={<Users className="size-5" />} label="Lịch bệnh nhân" active={pathname.startsWith("/schedule/patients")} />
          </>
        )}
      </div>

      <div className={`border-t border-border ${expanded ? "p-4 space-y-1" : "p-3 space-y-2 flex flex-col items-center"}`}>
        <NavItem expanded={expanded} href="/change-password" icon={<KeyRound className="size-5" />} label="Đổi mật khẩu" active={pathname.startsWith("/change-password")} />
        <button
          type="button"
          onClick={handleSignOut}
          title="Đăng xuất"
          className={`flex items-center rounded-lg font-medium text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive ${
            expanded ? "w-full gap-3 px-3 py-2 text-sm" : "justify-center p-2.5"
          }`}
        >
          <LogOut className="size-5 shrink-0" />
          {expanded && <span>Đăng xuất</span>}
        </button>
      </div>
    </aside>
  );
}

function NavItem({ href, icon, label, active, expanded }: { href: string; icon: React.ReactNode; label: string; active: boolean; expanded: boolean }) {
  return (
    <Link
      href={href}
      title={!expanded ? label : undefined}
      className={`flex items-center transition-colors ${
        expanded 
          ? "w-full gap-3 rounded-lg px-3 py-2 text-sm font-medium" 
          : "justify-center rounded-lg p-2.5"
      } ${
        active 
          ? "bg-primary text-primary-foreground" 
          : "text-muted-foreground hover:bg-secondary hover:text-foreground"
      }`}
    >
      <div className="shrink-0">{icon}</div>
      {expanded && <span className="truncate">{label}</span>}
    </Link>
  );
}
