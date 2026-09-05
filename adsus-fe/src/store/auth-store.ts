import { useSyncExternalStore } from "react";
import { create } from "zustand";
import { persist, createJSONStorage, type StateStorage } from "zustand/middleware";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import type { Role } from "@/types/api.types";

/**
 * window.localStorage luôn tồn tại trên trình duyệt thật. Bọc lại chỉ để không crash trong
 * môi trường test (jsdom/Node) khi localStorage chưa sẵn sàng — cùng lớp vấn đề đã gặp ở
 * api-client.ts. Không bọc thì persist middleware của zustand gọi thẳng storage.setItem trên
 * giá trị undefined và ném lỗi ngay khi signIn() được gọi.
 */
const safeStorage: StateStorage = {
  getItem: (name) => (typeof window !== "undefined" && window.localStorage ? window.localStorage.getItem(name) : null),
  setItem: (name, value) => {
    if (typeof window !== "undefined" && window.localStorage) window.localStorage.setItem(name, value);
  },
  removeItem: (name) => {
    if (typeof window !== "undefined" && window.localStorage) window.localStorage.removeItem(name);
  },
};

export interface AuthUser {
  /**
   * Id tài khoản đang đăng nhập.
   *
   * Có mặt từ 04/08/2026 (backend thêm vào LoginResponse). Phiên persist trước mốc đó KHÔNG
   * có trường này — AuthGuard coi đó là phiên hỏng và bắt đăng nhập lại, xem lý do ở đó.
   */
  userId: string;
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
        if (typeof window !== "undefined" && window.localStorage) {
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
      storage: createJSONStorage(() => safeStorage),
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
      // Bệnh nhân dùng ứng dụng di động, không có giao diện web. Trường hợp này đã bị chặn
      // ngay từ lúc đăng nhập, nên thực tế không đi tới đây.
      return "/login";
    case "PHARMACIST":
      return "/medicines";
    default:
      return "/login";
  }
}

/**
 * Vai trò nào được vào khu vực nào — chép thẳng từ PRD §3.2 Permission Matrix.
 *
 * Điều hướng theo vai trò (BR-03) mới chỉ quyết định người dùng ĐƯỢC ĐƯA tới đâu sau khi
 * đăng nhập. Nó không ngăn được ai đó tự gõ đường dẫn khác lên thanh địa chỉ — nên cần
 * bảng này.
 */
const ROUTE_ROLES: ReadonlyArray<{ prefix: string; roles: readonly Role[] }> = [
  // "Statistics dashboard | View": Admin = Full, Doctor/Nurse = No, Patient = No.
  { prefix: "/dashboard", roles: ["ADMIN"] },
  // UC-09: Admin KHÔNG vào màn lâm sàng này — Admin quản lý tài khoản ở SCR-06.
  { prefix: "/patients", roles: ["DOCTOR", "NURSE"] },
  // SCR-30 — chi tiết ca khám. Cùng vai trò với /patients: UC-08 cho cả Bác sĩ lẫn Điều
  // dưỡng xem bản đầy đủ trên Web. Bệnh nhân xem bản rút gọn trên di động, không qua đây.
  { prefix: "/cases", roles: ["DOCTOR", "NURSE"] },
  // UC-04 (SCR-06, SCR-07): "Create", "Lock / Deactivate" và "Assign role" đều là No cho
  // Doctor/Nurse/Patient. Đây là chỗ đầu tiên NURSE khác DOCTOR.
  { prefix: "/admin/users", roles: ["ADMIN"] },
  { prefix: "/admin/blog", roles: ["ADMIN"] },
  { prefix: "/admin/ai-models", roles: ["ADMIN"] },
  { prefix: "/admin/shift-requests", roles: ["ADMIN"] },
  // Quản lý thuốc — Admin + Dược sĩ (URL mới, không còn /admin prefix)
  { prefix: "/medicines", roles: ["ADMIN", "PHARMACIST"] },
  { prefix: "/suppliers", roles: ["ADMIN", "PHARMACIST"] },
  { prefix: "/inventory", roles: ["ADMIN", "PHARMACIST"] },
  // UC-18: Doctor kê đơn thuốc (Module 7 Task 8 / SCR-17). Nurse có thể xem danh sách
  // tuân thủ nhưng không được kê đơn — kê đơn là hành vi y khoa chỉ Doctor được phép.
  { prefix: "/prescriptions", roles: ["DOCTOR"] },
  // SCR mới (28/08/2026) — "Lịch bệnh nhân": Doctor xem lịch bệnh nhân đã đặt, chỉ đọc. Phải
  // đứng TRƯỚC "/schedule" bên dưới vì isRoleAllowedOnPath dùng .find() (khớp luật đầu tiên) —
  // nếu để sau, cả hai luật đều cho DOCTOR nên không lộ bug, nhưng thứ tự đúng ngăn một luật
  // "/schedule" mới hơn (nếu sau này đổi role) vô tình khớp nhầm trước.
  { prefix: "/schedule/patients", roles: ["DOCTOR"] },
  // UC-15: Chỉ Bác sĩ mới được quyền quản lý lịch khám của mình, Admin và Nurse không được vào.
  { prefix: "/schedule", roles: ["DOCTOR"] },
];

/**
 * Vai trò này có được mở đường dẫn này không.
 *
 * Đường dẫn không có luật riêng thì ai đã đăng nhập cũng vào được — ví dụ /change-password,
 * vì ma trận quyền ghi "Change own password" là Full cho mọi vai trò.
 *
 * Lưu ý: đây vẫn chỉ là lớp trải nghiệm, giống [AuthGuard]. Chặn thật nằm ở backend.
 */
export function isRoleAllowedOnPath(role: Role, pathname: string): boolean {
  const rule = ROUTE_ROLES.find(
    (r) => pathname === r.prefix || pathname.startsWith(`${r.prefix}/`),
  );

  return rule ? rule.roles.includes(role) : true;
}
