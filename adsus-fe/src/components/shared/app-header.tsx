"use client";

import { Menu, ScanLine } from "lucide-react";
import Link from "next/link";

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

        {user && (
          <div className="flex items-center text-right">
            <div>
              <p className="font-heading text-sm font-bold leading-tight text-foreground">
                {user.fullName}
              </p>
              <p className="text-xs text-muted-foreground">{ROLE_LABEL[user.role]}</p>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}
