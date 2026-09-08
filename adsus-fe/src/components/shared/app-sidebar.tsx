"use client";

import { LogOut } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import type { CSSProperties } from "react";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/store/auth-store";
import { useUiStore } from "@/store/ui-store";

import { ACCOUNT_ITEM, isNavItemActive, type NavLeaf, visibleGroupsForRole } from "./nav-config";
import { roleAccentVar } from "./role-badge";

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

  if (!user) return null;

  const groups = visibleGroupsForRole(user.role);
  const accentStyle = { "--role-accent": roleAccentVar(user.role) } as CSSProperties;

  return (
    <aside
      style={accentStyle}
      className={cn(
        "sticky top-16 z-30 flex h-[calc(100vh-4rem)] flex-col border-r border-slate-200 bg-slate-50/80 shadow-sm backdrop-blur transition-all duration-300 ease-in-out dark:border-slate-800 dark:bg-slate-900/90 max-md:hidden",
        expanded ? "w-64" : "w-[68px]",
      )}
    >
      <nav
        className={cn(
          "flex-1 overflow-y-auto",
          expanded ? "p-3 space-y-5" : "p-3 space-y-4 flex flex-col items-center",
        )}
      >
        {groups.map((group) => (
          <div key={group.label} className={expanded ? "space-y-1" : "space-y-1 w-full flex flex-col items-center"}>
            {expanded && groups.length > 1 && (
              <p className="px-2.5 pb-1 text-[11px] font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                {group.label}
              </p>
            )}
            {group.items.map((item) => (
              <NavItem key={item.href} item={item} expanded={expanded} active={isNavItemActive(item, pathname)} />
            ))}
          </div>
        ))}
      </nav>

      <div
        style={accentStyle}
        className={cn(
          "border-t border-slate-200 dark:border-slate-800",
          expanded ? "p-3 space-y-1" : "p-3 space-y-2 flex flex-col items-center"
        )}
      >
        <NavItem
          item={ACCOUNT_ITEM}
          expanded={expanded}
          active={pathname.startsWith(ACCOUNT_ITEM.href)}
        />
        <button
          type="button"
          onClick={handleSignOut}
          title="Đăng xuất"
          className={cn(
            "flex items-center rounded-lg font-semibold text-slate-700 transition-colors hover:bg-destructive/10 hover:text-destructive dark:text-slate-300",
            expanded ? "w-full gap-3 px-3 py-2 text-sm" : "justify-center p-2.5",
          )}
        >
          <LogOut className="size-5 shrink-0" />
          {expanded && <span>Đăng xuất</span>}
        </button>
      </div>
    </aside>
  );
}

export function NavItem({
  item,
  active,
  expanded,
}: {
  item: NavLeaf;
  active: boolean;
  expanded: boolean;
}) {
  const Icon = item.icon;
  return (
    <Link
      href={item.href}
      title={!expanded ? item.title : undefined}
      className={cn(
        "relative flex items-center transition-colors",
        expanded ? "w-full gap-3 rounded-lg py-2 pl-3.5 pr-3 text-sm" : "justify-center rounded-lg p-2.5",
        active
          ? "bg-[var(--role-accent)]/15 font-bold text-foreground"
          : "font-medium text-slate-700 hover:bg-slate-100 hover:text-foreground dark:text-slate-300 dark:hover:bg-slate-800",
      )}
    >
      {active && (
        <span
          aria-hidden
          className="absolute inset-y-1.5 left-0 w-1 rounded-r-full bg-[var(--role-accent)]"
        />
      )}
      <Icon className="size-5 shrink-0" />
      {expanded && <span className="whitespace-normal leading-tight">{item.title}</span>}
    </Link>
  );
}
