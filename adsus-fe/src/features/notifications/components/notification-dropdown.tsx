"use client";

import { useState } from "react";
import { Bell, CheckCheck } from "lucide-react";
import { useNotifications, useMarkAllAsRead } from "../hooks/use-notifications";
import { NotificationItem } from "./notification-item";
import { NotificationEmpty } from "./notification-empty";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { ScrollArea } from "@/components/ui/scroll-area";

export function NotificationDropdown({
  children,
}: {
  children: React.ReactNode;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const { data: notifications, isLoading } = useNotifications(1, 20);
  const markAllAsRead = useMarkAllAsRead();

  const hasUnread = notifications?.notifications.some((n) => !n.isRead) ?? false;

  const handleMarkAllRead = () => {
    markAllAsRead.mutate();
  };

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>
      <PopoverContent className="w-[400px] p-0" align="end" sideOffset={8}>
        {/* Header */}
        <div className="flex items-center justify-between border-b px-4 py-3">
          <div className="flex items-center gap-2">
            <Bell className="size-5 text-foreground" />
            <h3 className="font-semibold text-foreground">Thông báo</h3>
          </div>
          {hasUnread && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleMarkAllRead}
              disabled={markAllAsRead.isPending}
              className="h-auto p-1 text-xs text-muted-foreground hover:text-foreground"
            >
              <CheckCheck className="mr-1 size-4" />
              Đánh dấu đã đọc
            </Button>
          )}
        </div>

        {/* Notification List */}
        <ScrollArea className="max-h-[400px]">
          {isLoading ? (
            <div className="flex h-32 items-center justify-center">
              <div className="h-6 w-6 animate-spin rounded-full border-b-2 border-primary" />
            </div>
          ) : notifications?.notifications.length === 0 ? (
            <NotificationEmpty />
          ) : (
            <div className="divide-y divide-border">
              {notifications?.notifications.map((notification) => (
                <NotificationItem
                  key={notification.logId}
                  notification={notification}
                />
              ))}
            </div>
          )}
        </ScrollArea>

        {/* Footer */}
        {notifications &&
          notifications.notifications.length >= 20 && (
            <div className="border-t p-2 text-center">
              <Button variant="ghost" size="sm" className="w-full text-xs">
                Xem tất cả thông báo
              </Button>
            </div>
          )}
      </PopoverContent>
    </Popover>
  );
}
