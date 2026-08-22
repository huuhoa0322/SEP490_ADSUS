"use client";

import { useQuery } from "@tanstack/react-query";

import { apiClient } from "@/lib/api-client";

/**
 * SCR-01 gọi health-check ngay khi vào trang, không chờ user bấm đăng nhập — mục đích là
 * "đánh thức" Backend Render free-tier trước (nếu đang ngủ), để lúc user gõ xong số điện
 * thoại/mật khẩu thì server thường đã tỉnh, và có tín hiệu để hiển thị loading rõ ràng thay
 * vì để lần đăng nhập đầu tiên tự nhiên chậm bất thường không rõ lý do.
 */
export function useBackendHealth() {
  return useQuery({
    queryKey: ["backend-health"],
    queryFn: async () => {
      const { data } = await apiClient.get("/api/health");
      return data;
    },
    retry: 1,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
}
