import { AxiosError } from "axios";

import { getApiErrorMessage } from "@/lib/api-client";

/**
 * Vietnamese wording for authentication errors.
 *
 * The API answers in English (per api_design_rules) and is shared with the mobile app, so
 * translation belongs on the client. Every user of this system is a Vietnamese hospital
 * worker; showing them a raw English sentence would be a defect.
 */

/**
 * The ONE sentence shown for every failed sign-in.
 *
 * UCS GB-06 requires wrong phone number, wrong password, locked account and deactivated
 * account to be indistinguishable. Mapping on the HTTP status rather than on the backend
 * text keeps that guarantee: there is a single branch, so there is nothing to leak.
 */
const SIGN_IN_FAILED = "Số điện thoại hoặc mật khẩu không đúng.";

const CHANGE_PASSWORD_ERRORS: Record<string, string> = {
  "Current password is incorrect.": "Mật khẩu hiện tại không đúng.",
  "This account is no longer active.": "Tài khoản này đã ngừng hoạt động.",
  "Invalid access token.": "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
  "Current password is required.": "Vui lòng nhập mật khẩu hiện tại.",
  "New password is required.": "Vui lòng nhập mật khẩu mới.",
  "New password must be at least 8 characters.":
    "Mật khẩu mới phải có ít nhất 8 ký tự.",
  "New password must not exceed 72 characters.":
    "Mật khẩu mới không được vượt quá 72 ký tự.",
  "New password must contain at least one uppercase letter.":
    "Mật khẩu mới phải có ít nhất 1 chữ hoa.",
  "New password must contain at least one digit.":
    "Mật khẩu mới phải có ít nhất 1 chữ số.",
  "Password confirmation is required.": "Vui lòng nhập lại mật khẩu mới.",
  "Password confirmation does not match the new password.":
    "Xác nhận mật khẩu không khớp với mật khẩu mới.",
};

export function getSignInErrorMessage(error: unknown): string {
  // 401 always means "credentials rejected" — never say which part was wrong.
  if (error instanceof AxiosError && error.response?.status === 401) {
    return SIGN_IN_FAILED;
  }

  // 400 only ever comes from client-side shape validation (empty fields), which the form
  // already guards, so this is a safety net rather than a normal path.
  if (error instanceof AxiosError && error.response?.status === 400) {
    return "Vui lòng nhập đầy đủ số điện thoại và mật khẩu.";
  }

  // Anything else is a transport problem; getApiErrorMessage already words that in Vietnamese.
  return getApiErrorMessage(error, SIGN_IN_FAILED);
}

export function getChangePasswordErrorMessage(error: unknown): string {
  const raw = getApiErrorMessage(error, "");

  // The backend joins multiple validation failures with a space, so match the first known
  // sentence rather than requiring an exact whole-string match.
  for (const [english, vietnamese] of Object.entries(CHANGE_PASSWORD_ERRORS)) {
    if (raw.includes(english)) return vietnamese;
  }

  return raw || "Đổi mật khẩu thất bại. Vui lòng thử lại.";
}
