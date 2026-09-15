"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { completeRegistration } from "../api/auth.api";
import type { CompleteRegistrationRequest } from "../types/auth.types";
import { getSafeRedirect } from "./use-sign-in";

export function useCompleteRegistration() {
  const router = useRouter();

  return useMutation({
    mutationFn: async (payload: CompleteRegistrationRequest) => {
      return await completeRegistration(payload);
    },

    onSuccess: (data) => {
      if (!data.data) {
        return;
      }

      const { accessToken, refreshToken, user } = data.data;

      if (typeof window !== "undefined" && accessToken) {
        window.localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      }

      useAuthStore.getState().signIn(accessToken, refreshToken, user);

      const params =
        typeof window !== "undefined"
          ? new URLSearchParams(window.location.search)
          : null;
      const safeRedirect = params ? getSafeRedirect(params) : null;
      router.replace(safeRedirect ?? getHomePathForRole(user.role));
    },
  });
}
