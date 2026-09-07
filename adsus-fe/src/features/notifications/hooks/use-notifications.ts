import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { notificationsApi } from "../api/notifications.api";

export const NOTIFICATION_KEYS = {
  all: ["notifications"] as const,
  unreadCount: () => [...NOTIFICATION_KEYS.all, "unread-count"] as const,
  list: (page: number, pageSize: number) =>
    [...NOTIFICATION_KEYS.all, "list", { page, pageSize }] as const,
};

export function useNotifications(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: NOTIFICATION_KEYS.list(page, pageSize),
    queryFn: () => notificationsApi.getNotifications(page, pageSize),
    staleTime: 30_000,
  });
}

export function useUnreadCount() {
  return useQuery({
    queryKey: NOTIFICATION_KEYS.unreadCount(),
    queryFn: () => notificationsApi.getUnreadCount(),
    refetchInterval: 30_000,
    refetchIntervalInBackground: false,
  });
}

export function useMarkAsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (logId: string) => notificationsApi.markAsRead(logId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
    },
  });
}

export function useMarkAllAsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => notificationsApi.markAllAsRead(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
    },
  });
}

export function useDeleteNotification() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (logId: string) => notificationsApi.deleteNotification(logId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
    },
  });
}
