"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { changePassword } from "../api/auth.api";
import type { ChangePasswordRequest } from "../types/auth.types";

export function useChangePassword() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const clearMustChangePassword = useAuthStore((s) => s.clearMustChangePassword);

  // Captured now: once the change succeeds the flag is cleared, and we can no longer tell
  // whether the user was forced here or came on their own.
  const wasForced = user?.mustChangePassword ?? false;

  return useMutation({
    mutationFn: (payload: ChangePasswordRequest) => changePassword(payload),

    onSuccess: () => {
      // The backend already cleared the flag in the database; mirror it so AuthGuard stops
      // holding the user on this page.
      clearMustChangePassword();

      // Forced here -> send them on to their role home.
      // Came voluntarily -> stay put and just show the success message.
      if (wasForced && user) {
        router.replace(getHomePathForRole(user.role));
      }
    },
  });
}
