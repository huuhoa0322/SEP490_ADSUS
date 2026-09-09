import type { NotificationType, NotificationLog } from "../types/notification.types";

export const NOTIFICATION_ICONS: Record<NotificationType, string> = {
  general: "📢",
  medication_reminder: "💊",
  appointment_booking: "📅",
  appointment_reminder: "⏰",
  appointment_confirmed: "✅",
  appointment_cancelled: "❌",
  case_update: "📋",
  prescription_created: "💉",
  system_alert: "⚠️",
  inventory_alert: "📦",
};

export const NOTIFICATION_COLORS: Record<
  NotificationType,
  { bg: string; text: string }
> = {
  general: { bg: "bg-blue-100", text: "text-blue-600" },
  medication_reminder: { bg: "bg-purple-100", text: "text-purple-600" },
  appointment_booking: { bg: "bg-green-100", text: "text-green-600" },
  appointment_reminder: { bg: "bg-orange-100", text: "text-orange-600" },
  appointment_confirmed: { bg: "bg-emerald-100", text: "text-emerald-600" },
  appointment_cancelled: { bg: "bg-red-100", text: "text-red-600" },
  case_update: { bg: "bg-cyan-100", text: "text-cyan-600" },
  prescription_created: { bg: "bg-indigo-100", text: "text-indigo-600" },
  system_alert: { bg: "bg-yellow-100", text: "text-yellow-600" },
  inventory_alert: { bg: "bg-amber-100", text: "text-amber-600" },
};

export function formatRelativeTime(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return "Vừa xong";
  if (diffMins < 60) return `${diffMins} phút trước`;
  if (diffHours < 24) return `${diffHours} giờ trước`;
  if (diffDays < 7) return `${diffDays} ngày trước`;

  return date.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "2-digit",
  });
}

export function isRecentNotification(notification: NotificationLog): boolean {
  const date = new Date(notification.sentAt);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  return diffMs < 86400000;
}
