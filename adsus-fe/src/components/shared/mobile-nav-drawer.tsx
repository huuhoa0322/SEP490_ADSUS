"use client";

import { LogOut, ScanLine } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type CSSProperties } from "react";

import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";
import { useUiStore } from "@/store/ui-store";

import { NavItem } from "./app-sidebar";
import { ACCOUNT_ITEM, isNavItemActive, visibleGroupsForRole } from "./nav-config";
import { RoleBadge, roleAccentVar } from "./role-badge";

/** Below `md`, AppSidebar renders nothing — this is the only nav a phone or small
 *  tablet gets. Same NAV_GROUPS data as the sidebar (see nav-config.ts) so the two
 *  never list different links. */
export function MobileNavDrawer() {
  const router = useRouter();
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);
  const signOut = useAuthStore((s) => s.signOut);
  const isOpen = useUiStore((s) => s.isMobileMenuOpen);
  const setOpen = useUiStore((s) => s.setMobileMenuOpen);

  // Close automatically when the user taps a link and the route actually changes —
  // without this the drawer stays open over the new page.
  useEffect(() => {
    setOpen(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  if (!user) return null;

  function handleSignOut() {
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    signOut();
    setOpen(false);
    router.replace("/login");
  }

  const groups = visibleGroupsForRole(user.role);
  const accentStyle = { "--role-accent": roleAccentVar(user.role) } as CSSProperties;

  return (
    <Dialog open={isOpen} onOpenChange={setOpen}>
      <DialogContent
        style={accentStyle}
        className="fixed inset-y-0 left-0 top-0 h-full w-[280px] max-w-[85vw] translate-x-0 translate-y-0 flex-col gap-0 rounded-none rounded-r-xl border-r border-border bg-background p-0 duration-200 data-closed:slide-out-to-left-full data-open:slide-in-from-left-full"
        showCloseButton
      >
        <DialogTitle className="sr-only">Menu điều hướng</DialogTitle>

        <Link
          href={user ? getHomePathForRole(user.role) : "/login"}
          className="flex h-16 shrink-0 items-center gap-2.5 border-b border-border px-4"
        >
          <span className="flex size-9 items-center justify-center rounded-full bg-primary">
            <ScanLine className="size-4.5 text-primary-foreground" />
          </span>
          <span className="font-heading text-lg font-bold tracking-[-0.02em] text-primary">ADSUS</span>
        </Link>

        <nav className="flex-1 space-y-5 overflow-y-auto p-3">
          {groups.map((group) => (
            <div key={group.label} className="space-y-1">
              {groups.length > 1 && (
                <p className="px-2.5 pb-1 text-[11px] font-700 uppercase tracking-wider text-muted-foreground/70">
                  {group.label}
                </p>
              )}
              {group.items.map((item) => (
                <NavItem key={item.href} item={item} expanded active={isNavItemActive(item, pathname)} />
              ))}
            </div>
          ))}
        </nav>

        <div className="shrink-0 space-y-1 border-t border-border p-3">
          <div className="px-2.5 pb-1">
            <RoleBadge role={user.role} />
          </div>
          <NavItem item={ACCOUNT_ITEM} expanded active={pathname.startsWith(ACCOUNT_ITEM.href)} />
          <button
            type="button"
            onClick={handleSignOut}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
          >
            <LogOut className="size-5 shrink-0" />
            <span>Đăng xuất</span>
          </button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
