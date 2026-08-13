"use client";

import { BrainCircuit, CalendarClock, FileText, LayoutDashboard, Menu, ScanLine, Users, ClipboardList } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { getHomePathForRole, useAuthStore } from "@/store/auth-store";
import { useUiStore } from "@/store/ui-store";
import type { Role } from "@/types/api.types";

const ROLE_LABEL: Record<Role, string> = {
  ADMIN: "Quản trị viên",
  DOCTOR: "Bác sĩ",
  NURSE: "Điều dưỡng",
  PATIENT: "Bệnh nhân",
};

export function AppHeader() {
  const user = useAuthStore((s) => s.user);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-background/95 backdrop-blur">
      <div className="flex h-16 w-full items-center justify-between gap-4 px-6">
        <div className="flex items-center gap-4">
          <button 
            onClick={toggleSidebar} 
            className="flex items-center justify-center rounded-lg p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
          >
            <Menu className="size-5" />
          </button>
          
          <Link
            href={user ? getHomePathForRole(user.role) : "/login"}
            className="flex items-center gap-2.5"
          >
            <span className="flex size-9 items-center justify-center rounded-full bg-primary">
              <ScanLine className="size-4.5 text-primary-foreground" />
            </span>
            <span className="font-heading text-lg font-bold tracking-[-0.02em] text-primary">
              ADSUS
            </span>
          </Link>
        </div>

        {/* Navigation Items in Header */}
        <div className="hidden flex-1 items-center justify-end gap-1 mr-6 xl:flex border-r border-border pr-6">
          {user?.role === "ADMIN" && (
            <>
              <HeaderNav href="/dashboard" icon={<LayoutDashboard className="size-4" />} label="Dashboard" active={pathname.startsWith("/dashboard")} />
              <HeaderNav href="/admin/users" icon={<Users className="size-4" />} label="Tài khoản" active={pathname.startsWith("/admin/users")} />
              <HeaderNav href="/admin/ai-models" icon={<BrainCircuit className="size-4" />} label="Mô hình AI" active={pathname.startsWith("/admin/ai-models")} />
              <HeaderNav href="/admin/blog" icon={<FileText className="size-4" />} label="Blog" active={pathname.startsWith("/admin/blog")} />
            </>
          )}

          {(user?.role === "DOCTOR" || user?.role === "NURSE") && (
            <HeaderNav href="/patients" icon={<ClipboardList className="size-4" />} label="Danh sách bệnh nhân" active={pathname.startsWith("/patients")} />
          )}

          {user?.role === "DOCTOR" && (
            <HeaderNav href="/schedule" icon={<CalendarClock className="size-4" />} label="Quản lý lịch" active={pathname.startsWith("/schedule")} />
          )}
        </div>

        {user && (
          <div className="flex shrink-0 items-center text-right">
            <div>
              <p className="font-heading text-base font-bold leading-tight text-foreground">
                {user.fullName}
              </p>
              <p className="text-sm font-medium text-muted-foreground">{ROLE_LABEL[user.role]}</p>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}

function HeaderNav({ href, icon, label, active }: { href: string; icon: React.ReactNode; label: string; active: boolean }) {
  return (
    <Link
      href={href}
      className={`flex items-center gap-2 rounded-full px-3.5 py-2 text-sm font-medium transition-colors ${
        active 
          ? "bg-primary/10 text-primary" 
          : "text-muted-foreground hover:bg-secondary hover:text-foreground"
      }`}
    >
      {icon}
      <span>{label}</span>
    </Link>
  );
}
