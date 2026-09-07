"use client";

import { Bell } from "lucide-react";
import { useUnreadCount } from "../hooks/use-notifications";
import { NotificationDropdown } from "./notification-dropdown";

export function NotificationBell() {
  const { data: unreadCount = 0 } = useUnreadCount();

  return (
    <NotificationDropdown>
      <button className="relative flex items-center justify-center rounded-lg p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground">
        <Bell className="size-5" />
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 flex size-5 min-w-5 items-center justify-center rounded-full bg-red-500 text-[10px] font-medium leading-none text-white">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>
    </NotificationDropdown>
  );
}
