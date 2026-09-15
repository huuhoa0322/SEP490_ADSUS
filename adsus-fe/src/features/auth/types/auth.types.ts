import type { Role } from "@/types/api.types";

/** UC-01 — sign in with a PHONE NUMBER, not an email or a username. */
export interface LoginRequest {
  phoneNumber: string;
  password: string;
}

/** The "data" payload of a successful sign-in response. */
export interface LoginResponse {
  /** Id tài khoản — GB-04: form tạo ca khám cần nó để điền sẵn ô "Bác sĩ phụ trách". */
  userId: string;
  accessToken: string;
  /** Refresh token for renewing access token without re-login. Used by SignalR. */
  refreshToken: string;
  role: Role;
  fullName: string;
  email: string | null;
  /** When true the user must change their password before reaching any other screen (UC-25). */
  mustChangePassword: boolean;
}

/**
 * UC-03 FT-06 — tự yêu cầu cấp lại mật khẩu.
 * BR-01: phải khớp CẢ số điện thoại LẪN email của một tài khoản đang tồn tại.
 */
export interface ForgotPasswordRequest {
  phoneNumber: string;
  email: string;
}

/**
 * UC-25 — a signed-in user changes their own password.
 *
 * currentPassword bỏ trống được (sửa 06/08/2026) khi tài khoản còn đang dùng mật khẩu tạm
 * (mustChangePassword) — backend tự bỏ qua bước xác thực trong trường hợp đó, dựa trên cờ
 * phía server chứ không phải giá trị client gửi lên.
 */
export interface ChangePasswordRequest {
  currentPassword: string | null;
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

export interface CompleteRegistrationRequest {
  firebaseIdToken: string;
  fullName: string;
  password: string;
  confirmPassword: string;
  email?: string | null;
  dateOfBirth?: string | null;
  gender?: "MALE" | "FEMALE" | "OTHER" | null;
}

export type RegisterStep = "phone" | "otp" | "profile";

export interface CompleteRegistrationResponseData extends LoginResponse {
  user: {
    userId: string;
    fullName: string;
    email: string | null;
    role: Role;
    mustChangePassword: boolean;
  };
}


