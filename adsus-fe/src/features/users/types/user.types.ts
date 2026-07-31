import type { Role } from "@/types/api.types";

/** Trạng thái tài khoản — khớp enum user_status trong database. */
export type AccountStatus = "ACTIVE" | "LOCKED" | "DEACTIVATED";

/**
 * Vai trò Admin gán được ở SCR-07.
 *
 * Cố ý KHÔNG có ADMIN: theo UC-04, tài khoản quản trị được cấp lúc dựng hệ thống chứ không
 * tạo qua màn này. Backend cũng từ chối, đây chỉ là lớp chặn thứ hai cho gọn.
 */
export type AssignableRole = Extract<Role, "DOCTOR" | "NURSE" | "PATIENT">;

export const ASSIGNABLE_ROLES: readonly AssignableRole[] = [
  "DOCTOR",
  "NURSE",
  "PATIENT",
] as const;

/** Một dòng trong danh sách tài khoản (SCR-06). */
export interface UserAccount {
  userId: string;
  phoneNumber: string;
  fullName: string;
  email: string | null;
  role: Role;
  status: AccountStatus;
  /**
   * LUÔN null khi vai trò là PATIENT — backend lọc bỏ (UC-04 BR-01).
   * Ngày sinh của bệnh nhân là dữ liệu y tế, Admin không được xem.
   */
  dateOfBirth: string | null;
  mustChangePassword: boolean;
  createdAt: string;
  /**
   * Dòng này là tài khoản của chính Admin đang xem.
   * Backend đã chặn tự khoá chính mình (UC-04 AF-04); cờ này để giao diện đừng bày ra nút
   * chắc chắn báo lỗi.
   */
  isCurrentUser: boolean;
}

/** UC-04 FT-07. Không có trường mật khẩu: hệ thống tự sinh rồi gửi email. */
export interface CreateUserAccountRequest {
  phoneNumber: string;
  fullName: string;
  role: AssignableRole;
  email?: string | null;
  dateOfBirth?: string | null;
}

/** UC-04 FT-09. Không có số điện thoại (BR-02) và không có trạng thái (endpoint riêng). */
export interface UpdateUserAccountRequest {
  fullName: string;
  role: AssignableRole;
  email?: string | null;
  dateOfBirth?: string | null;
}

export interface UserListQuery {
  keyword?: string;
  role?: Role | "";
  status?: AccountStatus | "";
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
