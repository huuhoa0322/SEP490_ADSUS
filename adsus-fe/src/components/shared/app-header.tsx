"use client";

import { Menu, ScanLine } from "lucide-react";
import Link from "next/link";

import { getHomePathForRole, useAuthStore } from "@/store/auth-store";
import { useUiStore } from "@/store/ui-store";
import { NotificationBell } from "@/features/notifications/components/notification-bell";
import { RoleBadge } from "./role-badge";

export function AppHeader() {
  const user = useAuthStore((s) => s.user);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const toggleMobileMenu = useUiStore((s) => s.toggleMobileMenu);

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-background/95 backdrop-blur">
      <div className="flex h-16 w-full items-center justify-between gap-4 px-4 sm:px-6">
        <div className="flex items-center gap-3 sm:gap-4">
          {/* Below md: opens the slide-in drawer (sidebar isn't rendered there at all).
              md and up: collapses/expands the persistent sidebar instead. */}
          <button
            onClick={toggleMobileMenu}
            aria-label="Mở menu điều hướng"
            className="flex items-center justify-center rounded-lg p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground md:hidden"
          >
            <Menu className="size-5" />
          </button>
          <button
            onClick={toggleSidebar}
            aria-label="Thu gọn / mở rộng menu"
            className="hidden items-center justify-center rounded-lg p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground md:flex"
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
          <div className="flex shrink-0 items-center gap-3">
            <NotificationBell />
            <div className="hidden text-right sm:block">
              <p className="font-heading text-base font-bold leading-tight text-foreground">
                {user.fullName}
              </p>
              <RoleBadge role={user.role} className="justify-end" />
            </div>
          </div>
        )}
      </div>
    </header>
  );
}
