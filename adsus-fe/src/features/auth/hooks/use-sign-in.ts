"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { login } from "../api/auth.api";
import {
  WebNotAvailableForRoleError,
  type LoginRequest,
} from "../types/auth.types";

export function useSignIn() {
  const router = useRouter();
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: async (payload: LoginRequest) => {
      const data = await login(payload);

      // UC-01: SCR-01 là màn đăng nhập Web của Admin/Doctor/Nurse. Bệnh nhân đăng nhập
      // trên ứng dụng di động. Chặn ở đây, TRƯỚC khi lưu token — nếu để lọt thì bệnh nhân
      // vừa đăng nhập xong lại bị đá về đúng màn đăng nhập mà không hiểu vì sao.
      if (data.role === "PATIENT") {
        throw new WebNotAvailableForRoleError();
      }

      return data;
    },

    onSuccess: (data) => {
      // Store the token first so the axios interceptor can attach it to the next request.
      window.localStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken);

      signIn(data.accessToken, {
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
      router.replace(getHomePathForRole(data.role));
    },
  });
}
