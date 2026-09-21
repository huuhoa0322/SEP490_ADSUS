"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { changePassword } from "../api/auth.api";
import type { ChangePasswordRequest } from "../types/auth.types";

export function useChangePassword() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const signIn = useAuthStore((s) => s.signIn);

  // Captured now: once the change succeeds the flag is cleared, and we can no longer tell
  // whether the user was forced here or came on their own.
  const wasForced = user?.mustChangePassword ?? false;

  return useMutation({
    mutationFn: (payload: ChangePasswordRequest) => changePassword(payload),

    onSuccess: (tokens) => {
      // The old access token may still carry mustChangePassword=true — every request made
      // with it would keep getting rejected by the backend until the caller signs in again.
      // Replace it with the fresh pair the backend just issued instead of only clearing the
      // local flag (bug found via System Test, fixed 21/09/2026).
      signIn(tokens.accessToken, tokens.refreshToken, {
        userId: tokens.userId,
        fullName: tokens.fullName,
        email: tokens.email,
        role: tokens.role,
        mustChangePassword: tokens.mustChangePassword,
      });

      // Forced here -> send them on to their role home.
      // Came voluntarily -> stay put and just show the success message.
      if (wasForced && user) {
        router.replace(getHomePathForRole(user.role));
      }
    },
  });
}
