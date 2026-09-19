"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";

import { completeRegistration } from "../api/auth.api";
import type { CompleteRegistrationRequest } from "../types/auth.types";


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

      // Theo yÃªu cáº§u: luÃ´n vÃ o Ä‘Ãºng default path (dashboard) mÃ  khÃ´ng theo redirect á»Ÿ url.
      router.replace(getHomePathForRole(user.role));
    },
  });
}
