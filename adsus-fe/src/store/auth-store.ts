import { useSyncExternalStore } from "react";
import { create } from "zustand";
import { persist, createJSONStorage } from "zustand/middleware";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import type { Role } from "@/types/api.types";

export interface AuthUser {
  fullName: string;
  email: string | null;
  role: Role;
  mustChangePassword: boolean;
}

interface AuthState {
  user: AuthUser | null;
  accessToken: string | null;
  signIn: (token: string, user: AuthUser) => void;
  signOut: () => void;
  /** Called after a successful password change — the backend already cleared the flag. */
  clearMustChangePassword: () => void;
}

/**
 * Holds the signed-in session.
 *
 * The token lives in localStorage and travels in the Authorization header, matching mobile
 * (Flutter stores it in flutter_secure_storage and sends the same Bearer header), so the
 * backend only has to support one mechanism.
 *
 * TRADE-OFF WORTH KNOWING: localStorage is readable from JavaScript, so an XSS hole means a
 * stolen token. httpOnly cookies are safer but require backend changes plus CSRF handling.
 * This is a medical system, so the team should revisit this before going to production.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,

      signIn: (token, user) => set({ accessToken: token, user }),

      signOut: () => {
        set({ accessToken: null, user: null });
        if (typeof window !== "undefined") {
          window.localStorage.removeItem(ACCESS_TOKEN_KEY);
        }
      },

      clearMustChangePassword: () =>
        set((state) =>
          state.user ? { user: { ...state.user, mustChangePassword: false } } : state,
        ),
    }),
    {
      name: "adsus.auth",
      storage: createJSONStorage(() => localStorage),
    },
  ),
);

/**
 * Reports whether the store has finished reading localStorage.
 *
 * Needed because the very first render always runs against an empty store — checking auth
 * at that moment would bounce a perfectly valid session back to the sign-in page.
 */
export function useHasHydrated(): boolean {
  return useSyncExternalStore(
    (onStoreChange) => useAuthStore.persist.onFinishHydration(onStoreChange),
    () => useAuthStore.persist.hasHydrated(),
    // There is no localStorage on the server, so treat it as not yet hydrated.
    () => false,
  );
}

/**
 * Home path per role — UC-01 BR-03: there is no role picker, the system sends the user
 * straight to their own area.
 */
export function getHomePathForRole(role: Role): string {
  switch (role) {
    case "ADMIN":
      return "/dashboard";
    case "DOCTOR":
    case "NURSE":
      // Nurse có quyền giống hệt Doctor (UCS), nên cũng vào danh sách bệnh nhân.
      return "/patients";
    case "PATIENT":
      // Bệnh nhân dùng ứng dụng di động, không có giao diện web.
      return "/";
    default:
      return "/";
  }
}
