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
