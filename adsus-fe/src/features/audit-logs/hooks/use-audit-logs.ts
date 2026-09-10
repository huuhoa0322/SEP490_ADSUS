"use client";

import { useQuery } from "@tanstack/react-query";
import { useDebounce } from "use-debounce";

import { getAuditLogs } from "../api/audit-logs.api";
import type { AuditLogsQueryParams } from "../types/audit-logs.types";

/**
 * Query keys for Audit Log queries.
 */
export const auditLogsQueryKeys = {
  all: ["admin", "audit-logs"] as const,
  list: (params: AuditLogsQueryParams) => [...auditLogsQueryKeys.all, "list", params] as const,
};

/**
 * Hook TanStack Query tải danh sách nhật ký thao tác với debounce 300ms cho từ khóa tìm kiếm.
 */
export function useAuditLogs(params: AuditLogsQueryParams = {}, debounceMs = 300) {
  const { page = 1, pageSize = 15, search = "", action, fromDate, toDate } = params;

  // 300ms debounce cho ô tìm kiếm realtime để tránh spam backend khi người dùng đang gõ
  const [debouncedSearch] = useDebounce(search.trim(), debounceMs);

  const effectiveAction = action && action !== "ALL" ? action : undefined;

  const effectiveParams: AuditLogsQueryParams = {
    page,
    pageSize,
    search: debouncedSearch || undefined,
    action: effectiveAction,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  };

  return useQuery({
    queryKey: auditLogsQueryKeys.list(effectiveParams),
    queryFn: () => getAuditLogs(effectiveParams),
    // Giữ kết quả trang cũ trong lúc nạp trang mới để tránh giật giao diện
    placeholderData: (previous) => previous,
  });
}
