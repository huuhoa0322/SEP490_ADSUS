"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { login } from "../api/auth.api";
import type { LoginRequest } from "../types/auth.types";

/**
 * Lấy ?redirect=/abc từ URL nếu hợp lệ (relative, không phải absolute URL).
 * - null = không có / không hợp lệ → caller tự quyết định đích.
 */
export function getSafeRedirect(searchParams: URLSearchParams): string | null {
  const value = searchParams.get("redirect");
  if (!value || !value.startsWith("/") || value.startsWith("//")) return null;
  return value;
}

export function useSignIn() {
  const router = useRouter();
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: async (payload: LoginRequest) => {
      return await login(payload);
    },

    onSuccess: (data) => {
      // Store the token first so the axios interceptor can attach it to the next request.
      window.localStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken);

      signIn(data.accessToken, data.refreshToken, {
        userId: data.userId,
        fullName: data.fullName,
        email: data.email,
        role: data.role,
        mustChangePassword: data.mustChangePassword,
      });

      // UC-25: an account holding an admin-issued temporary password must change it now,
      // before reaching any business screen.
      if (data.mustChangePassword) {
        router.replace("/change-password");
        return;
      }

      // UC-01 BR-03: route by role, there is no role picker.
      // Tôn trọng ?redirect=... nếu user đến /login từ trang khác (vd /dat-lich).
      const params =
        typeof window !== "undefined" ? new URLSearchParams(window.location.search) : null;
      const safeRedirect = params ? getSafeRedirect(params) : null;
      router.replace(safeRedirect ?? getHomePathForRole(data.role));
    },
  });
}
