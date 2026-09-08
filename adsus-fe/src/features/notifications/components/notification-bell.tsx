"use client";

import { Bell } from "lucide-react";
import { useUnreadCount } from "../hooks/use-notifications";
import { NotificationDropdown } from "./notification-dropdown";

export function NotificationBell() {
  const { data: unreadCount = 0 } = useUnreadCount();

  const hasUnread = unreadCount > 0;

  return (
    <NotificationDropdown>
      <button
        aria-label={hasUnread ? `Thông báo, ${unreadCount} chưa đọc` : "Thông báo"}
        className="relative flex items-center justify-center rounded-lg p-2 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
      >
        <Bell className="size-5" />
        {hasUnread && (
          <span className="absolute -top-0.5 -right-0.5 flex size-5 min-w-5 items-center justify-center">
            {/* Ping only while there's something unread — motion here signals real
                state, not ambient decoration, and stops the moment it's read. */}
            <span className="absolute inline-flex size-full animate-ping rounded-full bg-destructive/60 motion-reduce:hidden" />
            <span className="relative flex size-5 min-w-5 items-center justify-center rounded-full bg-destructive text-[10px] font-medium leading-none text-white">
              {unreadCount > 99 ? "99+" : unreadCount}
            </span>
          </span>
        )}
      </button>
    </NotificationDropdown>
  );
}
