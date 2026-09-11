import { apiClient } from "@/lib/api-client";
import type { ApiResponse, PagedResult } from "@/types/api.types";
import type { AuditLogEntry, AuditLogsQueryParams } from "../types/audit-logs.types";

/**
 * Lấy danh sách nhật ký thao tác có phân trang, tìm kiếm realtime, lọc theo hành động/danh mục
 * và khoảng thời gian.
 *
 * Endpoint: GET /api/v1/admin/audit-logs
 * Quyền: ADMIN
 */
export async function getAuditLogs(
  params: AuditLogsQueryParams = {}
): Promise<PagedResult<AuditLogEntry>> {
  const { page = 1, pageSize = 15, search, action, fromDate, toDate } = params;

  const trimmedSearch = search?.trim() || undefined;
  const effectiveAction =
    action && action !== "ALL" && action.trim().length > 0 ? action.trim() : undefined;

  const { data } = await apiClient.get<ApiResponse<PagedResult<AuditLogEntry>>>(
    "/api/v1/admin/audit-logs",
    {
      params: {
        page,
        pageSize,
        search: trimmedSearch,
        action: effectiveAction,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
      },
    }
  );

  return (
    data.data ?? {
      items: [],
      page,
      pageSize,
      totalItems: 0,
      totalPages: 0,
    }
  );
}
