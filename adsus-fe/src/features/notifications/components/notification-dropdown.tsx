"use client";

import { useState } from "react";
import { Bell, CheckCheck } from "lucide-react";
import { useInfiniteNotifications, useMarkAllAsRead } from "../hooks/use-notifications";
import { NotificationItem } from "./notification-item";
import { NotificationEmpty } from "./notification-empty";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";

export function NotificationDropdown({
  children,
}: {
  children: React.ReactNode;
}) {
  const [isOpen, setIsOpen] = useState(false);
  
  const {
    data,
    isLoading,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage
  } = useInfiniteNotifications(10);
  
  const markAllAsRead = useMarkAllAsRead();

  const allNotifications = data?.pages.flatMap((p) => p.notifications) ?? [];
  const hasUnread = allNotifications.some((n) => !n.isRead);

  const handleMarkAllRead = () => {
    markAllAsRead.mutate();
  };

  const handleScroll = (e: React.UIEvent<HTMLDivElement>) => {
    const { scrollTop, scrollHeight, clientHeight } = e.currentTarget;
    if (scrollHeight - scrollTop <= clientHeight + 50) {
      if (hasNextPage && !isFetchingNextPage) {
        fetchNextPage();
      }
    }
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
        <div className="max-h-[400px] overflow-y-auto overscroll-contain" onScroll={handleScroll}>
          {isLoading ? (
            <div className="flex h-32 items-center justify-center">
              <div className="h-6 w-6 animate-spin rounded-full border-b-2 border-primary" />
            </div>
          ) : allNotifications.length === 0 ? (
            <NotificationEmpty />
          ) : (
            <div className="divide-y divide-border">
              {allNotifications.map((notification) => (
                <NotificationItem
                  key={notification.logId}
                  notification={notification}
                />
              ))}
              {isFetchingNextPage && (
                <div className="flex items-center justify-center py-4">
                  <div className="h-4 w-4 animate-spin rounded-full border-b-2 border-primary" />
                </div>
              )}
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
