import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api.types";

import type {
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
} from "../types/auth.types";

export async function login(payload: LoginRequest): Promise<LoginResponse> {
  const { data } = await apiClient.post<ApiResponse<LoginResponse>>(
    "/api/v1/auth/login",
    payload,
  );

  if (!data.data) {
    // A 200 with no data would be a backend bug; treat it as a failure rather than
    // continuing with an empty session.
    throw new Error(data.message || "Đăng nhập thất bại.");
  }

  return data.data;
}

/**
 * UC-03 FT-06 — yêu cầu cấp lại mật khẩu.
 *
 * Backend LUÔN trả 200 kèm đúng một câu, dù thông tin đúng hay sai (AF-01). Vì vậy hàm này
 * trả về void: không có gì để phân biệt, và cũng không được phép có.
 */
export async function forgotPassword(payload: ForgotPasswordRequest): Promise<void> {
  await apiClient.post<ApiResponse<null>>("/api/v1/auth/forgot-password", payload);
}

export async function changePassword(payload: ChangePasswordRequest): Promise<void> {
  // Requires authentication; the token is attached by the apiClient interceptor.
  await apiClient.post<ApiResponse<null>>("/api/v1/auth/change-password", payload);
}
