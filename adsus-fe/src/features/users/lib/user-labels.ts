import type { Role } from "@/types/api.types";

import type { AccountStatus } from "../types/user.types";

/**
 * Nhãn tiếng Việt cho vai trò và trạng thái.
 *
 * API trả về mã viết hoa (DOCTOR, ACTIVE...) theo quy ước chung với ứng dụng di động.
 * Việc dịch sang tiếng Việt thuộc về phía client, vì người dùng hệ thống này đều là nhân
 * viên phòng khám người Việt.
 */
export const ROLE_LABEL: Record<Role, string> = {
  ADMIN: "Quản trị viên",
  DOCTOR: "Bác sĩ",
  NURSE: "Điều dưỡng",
  PATIENT: "Bệnh nhân",
};

export const STATUS_LABEL: Record<AccountStatus, string> = {
  ACTIVE: "Đang hoạt động",
  LOCKED: "Đã khoá",
  DEACTIVATED: "Đã vô hiệu hoá",
};

/** Màu hiển thị theo trạng thái — dùng token màu của nhóm trong globals.css. */
export const STATUS_CLASS: Record<AccountStatus, string> = {
  ACTIVE: "bg-accent/12 text-accent",
  LOCKED: "bg-amber-500/15 text-amber-700",
  // Vô hiệu hoá là một chiều nên dùng màu cảnh báo mạnh nhất của bộ nhận diện.
  DEACTIVATED: "bg-destructive/12 text-destructive",
};

/**
 * Ngày giờ theo định dạng người Việt đọc quen.
 * Backend trả về ISO UTC; hiển thị theo giờ máy người dùng.
 */
export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
