import { apiClient } from "@/lib/api-client";
import type { NotificationListResponse, UnreadCount } from "../types/notification.types";
import type { ApiResponse } from "@/types/api.types";

const BASE_URL = "/api/v1/notifications";

export const notificationsApi = {
  getNotifications: async (page = 1, pageSize = 20): Promise<NotificationListResponse> => {
    const { data } = await apiClient.get<ApiResponse<NotificationListResponse>>(
      BASE_URL,
      {
        params: { page, pageSize },
      }
    );
    if (!data.data) throw new Error(data.message || "Failed to fetch notifications");
    return data.data;
  },

  getUnreadCount: async (): Promise<number> => {
    try {
      const { data } = await apiClient.get<ApiResponse<UnreadCount>>(
        `${BASE_URL}/unread-count`
      );
      return data.data?.count ?? 0;
    } catch {
      return 0;
    }
  },

  markAsRead: async (logId: string): Promise<void> => {
    await apiClient.put(`${BASE_URL}/${logId}/read`);
  },

  markAllAsRead: async (): Promise<void> => {
    await apiClient.put(`${BASE_URL}/read-all`);
  },

  deleteNotification: async (logId: string): Promise<void> => {
    await apiClient.delete(`${BASE_URL}/${logId}`);
  },
};
