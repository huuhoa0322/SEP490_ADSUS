import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type { AuditLogEntry } from "../types/audit-log.types";

/**
 * Nhật ký thao tác gần đây. Chỉ Admin gọi được, backend chặn bằng [Authorize(Roles = "ADMIN")].
 *
 * Chỉ có ĐỌC — không có API sửa hay xoá nhật ký, cố ý như vậy.
 */
export async function getRecentAuditLogs(limit = 10): Promise<AuditLogEntry[]> {
  const { data } = await apiClient.get<ApiResponse<AuditLogEntry[]>>(
    "/api/v1/admin/audit-logs",
    { params: { limit } },
  );

  return data.data ?? [];
}
