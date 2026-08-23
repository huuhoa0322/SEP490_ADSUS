"use client";

import { useQuery } from "@tanstack/react-query";

interface MobileReleaseResponse {
  downloadUrl: string | null;
}

// Gọi route nội bộ /api/mobile-release (chạy trên server Next.js), KHÔNG gọi thẳng GitHub API
// từ trình duyệt — token đọc Release nằm ở server, xem route.ts. Vì vậy dùng fetch thường,
// không phải apiClient (apiClient trỏ tới Backend .NET, không phải chính Next.js server này).
export function useLatestAndroidRelease() {
  return useQuery({
    queryKey: ["latest-android-release"],
    queryFn: async () => {
      const response = await fetch("/api/mobile-release");
      const data = (await response.json()) as MobileReleaseResponse;
      return data.downloadUrl;
    },
    staleTime: 60 * 60 * 1000,
    retry: 1,
  });
}
