import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type { DashboardQuery, DashboardStatistics } from "../types/dashboard.types";

/** UC-05 — chỉ Admin gọi được, backend chặn bằng [Authorize(Roles = "ADMIN")]. */
export async function getDashboardStatistics(
  query: DashboardQuery,
): Promise<DashboardStatistics> {
  const { data } = await apiClient.get<ApiResponse<DashboardStatistics>>(
    "/api/v1/dashboard/statistics",
    {
      params: {
        fromDate: query.fromDate || undefined,
        toDate: query.toDate || undefined,
      },
    },
  );

  if (!data.data) throw new Error(data.message || "Không tải được số liệu thống kê.");

  return data.data;
}
