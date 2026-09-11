import type { Metadata } from "next";

import { AuditLogListView } from "@/features/audit-logs/components/audit-log-list-view";

export const metadata: Metadata = {
  title: "Nhật ký hệ thống | ADSUS",
  description: "Theo dõi toàn bộ lịch sử thao tác, phân quyền và hoạt động trong hệ thống ADSUS.",
};

/**
 * SCR: Admin Audit Log Management Page (R2, Milestone 3).
 * Được bảo vệ bởi ProtectedLayout và AuthGuard (yêu cầu vai trò ADMIN theo ROUTE_ROLES).
 */
export default function AdminAuditLogsPage() {
  return <AuditLogListView />;
}
