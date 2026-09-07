"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Check, Trash2 } from "lucide-react";
import type { NotificationLog } from "../types/notification.types";
import { useMarkAsRead, useDeleteNotification } from "../hooks/use-notifications";
import {
  NOTIFICATION_ICONS,
  NOTIFICATION_COLORS,
  formatRelativeTime,
} from "../lib/notification-utils";

interface NotificationItemProps {
  notification: NotificationLog;
}

export function NotificationItem({ notification }: NotificationItemProps) {
  const router = useRouter();
  const [isHovered, setIsHovered] = useState(false);
  const markAsRead = useMarkAsRead();
  const deleteNotification = useDeleteNotification();

  const isUnread = !notification.isRead;
  const icon = NOTIFICATION_ICONS[notification.type] || "📢";
  const colors = NOTIFICATION_COLORS[notification.type] || {
    bg: "bg-gray-100",
    text: "text-gray-600",
  };

  const handleClick = () => {
    if (isUnread) {
      markAsRead.mutate(notification.logId);
    }
    if (notification.deepLink) {
      router.push(notification.deepLink);
    }
  };

  const handleDelete = (e: React.MouseEvent) => {
    e.stopPropagation();
    deleteNotification.mutate(notification.logId);
  };

  const handleMarkRead = (e: React.MouseEvent) => {
    e.stopPropagation();
    markAsRead.mutate(notification.logId);
  };

  return (
    <div
      role="button"
      tabIndex={0}
      className={`relative cursor-pointer px-4 py-3 transition-colors hover:bg-muted/50 ${
        isUnread ? "bg-blue-50/50" : ""
      }`}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onClick={handleClick}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          handleClick();
        }
      }}
    >
      {/* Unread indicator */}
      {isUnread && (
        <div className="absolute left-1.5 top-1/2 h-2 w-2 -translate-y-1/2 rounded-full bg-blue-500" />
      )}

      <div className="flex gap-3">
        {/* Icon */}
        <div
          className={`size-10 flex shrink-0 items-center justify-center rounded-full text-lg ${colors.bg} ${colors.text}`}
        >
          {icon}
        </div>

        {/* Content */}
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-2">
            <h4
              className={`text-sm font-medium ${
                isUnread ? "text-foreground" : "text-muted-foreground"
              }`}
            >
              {notification.title}
            </h4>
            <span className="whitespace-nowrap text-xs text-muted-foreground">
              {formatRelativeTime(notification.sentAt)}
            </span>
          </div>
          <p className="mt-0.5 line-clamp-2 text-sm text-muted-foreground">
            {notification.body}
          </p>
        </div>
      </div>

      {/* Action buttons (show on hover) */}
      {isHovered && (
        <div className="absolute right-2 top-1/2 flex -translate-y-1/2 gap-1">
          {isUnread && (
            <button
              onClick={handleMarkRead}
              className="rounded p-1.5 text-muted-foreground hover:bg-secondary hover:text-foreground"
              title="Đánh dấu đã đọc"
            >
              <Check className="size-4" />
            </button>
          )}
          <button
            onClick={handleDelete}
            className="rounded p-1.5 text-muted-foreground hover:bg-red-100 hover:text-red-500"
            title="Xóa thông báo"
          >
            <Trash2 className="size-4" />
          </button>
        </div>
      )}
    </div>
  );
}
