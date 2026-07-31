"use client";

import { useQuery } from "@tanstack/react-query";

import { getDashboardStatistics } from "../api/dashboard.api";
import type { DashboardQuery } from "../types/dashboard.types";

export function useDashboardStatistics(query: DashboardQuery) {
  return useQuery({
    queryKey: ["dashboard", "statistics", query] as const,
    queryFn: () => getDashboardStatistics(query),
    // Giữ số liệu cũ trong lúc đổi khoảng thời gian, để bảng không nháy trắng.
    placeholderData: (previous) => previous,
  });
}
