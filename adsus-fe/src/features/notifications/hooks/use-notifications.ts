import { useRef } from "react";
import { useQuery, useMutation, useQueryClient, useInfiniteQuery } from "@tanstack/react-query";
import { notificationsApi } from "../api/notifications.api";

export const NOTIFICATION_KEYS = {
  all: ["notifications"] as const,
  unreadCount: () => [...NOTIFICATION_KEYS.all, "unread-count"] as const,
  list: (page: number, pageSize: number) =>
    [...NOTIFICATION_KEYS.all, "list", { page, pageSize }] as const,
  infiniteList: () => [...NOTIFICATION_KEYS.all, "infinite-list"] as const,
};

export function useNotifications(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: NOTIFICATION_KEYS.list(page, pageSize),
    queryFn: () => notificationsApi.getNotifications(page, pageSize),
    staleTime: 30_000,
  });
}

export function useInfiniteNotifications(
  pageSize = 10,
  options?: { refetchInterval?: number | false }
) {
  return useInfiniteQuery({
    queryKey: NOTIFICATION_KEYS.infiniteList(),
    queryFn: ({ pageParam = 1 }) => notificationsApi.getNotifications(pageParam, pageSize),
    initialPageParam: 1,
    getNextPageParam: (lastPage, allPages) => {
      // Giả sử API trả về mảng notifications. Nếu mảng rỗng hoặc ít hơn pageSize, hết dữ liệu.
      if (lastPage.notifications.length < pageSize) return undefined;
      return allPages.length + 1;
    },
    staleTime: 0,
    refetchInterval: options?.refetchInterval,
  });
}

export function useUnreadCount() {
  const queryClient = useQueryClient();
  const prevCountRef = useRef<number | null>(null);

  return useQuery({
    queryKey: NOTIFICATION_KEYS.unreadCount(),
    queryFn: async () => {
      const count = await notificationsApi.getUnreadCount();
      if (prevCountRef.current !== null && count > prevCountRef.current) {
        // Có thông báo mới tới -> tự động invalidate danh sách để lần mở chuông kế tiếp có data mới ngay
        queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.infiniteList() });
      }
      prevCountRef.current = count;
      return count;
    },
    refetchInterval: 15_000,
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
