"use client";

import { useMutation } from "@tanstack/react-query";

import { forgotPassword } from "../api/auth.api";
import type { ForgotPasswordRequest } from "../types/auth.types";

/** UC-03 FT-06 — tự yêu cầu cấp lại mật khẩu. Tách ra đây (12/08/2026, P_FE7) cho khớp
 * pattern P_FE5 — useSignIn/useChangePassword cũng đều nằm ở hooks/, không tự dựng
 * useMutation ngay trong component. */
export function useForgotPassword() {
  return useMutation({
    mutationFn: (payload: ForgotPasswordRequest) => forgotPassword(payload),
  });
}
