import type { Role } from "@/types/api.types";

/** UC-01 — sign in with a PHONE NUMBER, not an email or a username. */
export interface LoginRequest {
  phoneNumber: string;
  password: string;
}

/** The "data" payload of a successful sign-in response. */
export interface LoginResponse {
  accessToken: string;
  role: Role;
  fullName: string;
  email: string | null;
  /** When true the user must change their password before reaching any other screen (UC-25). */
  mustChangePassword: boolean;
}

/**
 * Đăng nhập đúng mật khẩu nhưng vai trò này không có giao diện Web.
 *
 * UC-01: SCR-01 (Web) dành cho Admin, Doctor, Nurse. Bệnh nhân dùng SCR-02 trên ứng dụng
 * di động. Không phải lỗi xác thực nên KHÔNG gộp vào câu chung của GB-06 — nói mơ hồ ở đây
 * chỉ khiến bệnh nhân tưởng mình nhập sai mật khẩu và thử đi thử lại.
 */
export class WebNotAvailableForRoleError extends Error {
  constructor() {
    super("WEB_NOT_AVAILABLE_FOR_ROLE");
    this.name = "WebNotAvailableForRoleError";
  }
}

/** UC-25 — a signed-in user changes their own password. */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

/**
 * Password policy from TDS §4.3 — must stay in sync with the backend validator.
 * Kept here so the form can show the requirements while the user types, instead of
 * rejecting the submission afterwards.
 */
export const PASSWORD_POLICY = {
  minLength: 8,
  maxLength: 72,
  rules: [
    { label: "Từ 8 đến 72 ký tự", test: (v: string) => v.length >= 8 && v.length <= 72 },
    { label: "Có ít nhất 1 chữ hoa", test: (v: string) => /[A-Z]/.test(v) },
    { label: "Có ít nhất 1 chữ số", test: (v: string) => /[0-9]/.test(v) },
  ],
} as const;
