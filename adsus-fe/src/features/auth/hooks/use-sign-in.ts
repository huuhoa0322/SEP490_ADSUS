"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { login } from "../api/auth.api";
import type { LoginRequest } from "../types/auth.types";

export function useSignIn() {
  const router = useRouter();
  const signIn = useAuthStore((state) => state.signIn);

  return useMutation({
    mutationFn: (payload: LoginRequest) => login(payload),

    onSuccess: (data) => {
      // Store the token first so the axios interceptor can attach it to the next request.
      window.localStorage.setItem(ACCESS_TOKEN_KEY, data.accessToken);

      signIn(data.accessToken, {
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
