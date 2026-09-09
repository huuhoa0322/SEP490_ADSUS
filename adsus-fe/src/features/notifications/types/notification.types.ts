export type NotificationType =
  | "general"
  | "medication_reminder"
  | "appointment_booking"
  | "appointment_reminder"
  | "appointment_confirmed"
  | "appointment_cancelled"
  | "case_update"
  | "prescription_created"
  | "system_alert"
  | "inventory_alert";

export interface NotificationLog {
  logId: string;
  type: NotificationType;
  title: string;
  body: string | null;
  deepLink: string | null;
  metadata: Record<string, unknown> | null;
  sentAt: string;
  readAt: string | null;
  isRead: boolean;
}

export interface NotificationListResponse {
  notifications: NotificationLog[];
  unreadCount: number;
}

export interface UnreadCount {
  count: number;
}
