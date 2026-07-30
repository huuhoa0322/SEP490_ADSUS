"use client";

import { Loader2 } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";

import {
  getHomePathForRole,
  isRoleAllowedOnPath,
  useAuthStore,
  useHasHydrated,
} from "@/store/auth-store";

const CHANGE_PASSWORD_PATH = "/change-password";

/**
 * Guards pages that require a signed-in user.
 *
 * The check runs in the browser rather than in Next.js proxy/middleware because the token
 * lives in localStorage, which the server cannot read. If the team ever moves to httpOnly
 * cookies, this check should move to proxy.ts so it can block earlier.
 *
 * This is NOT a data protection layer. A user can edit localStorage and walk right past it.
 * Real protection lives on the backend: every endpoint carries [Authorize] and verifies the
 * token signature itself. The guard exists purely for user experience.
 */
export function AuthGuard({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const hasHydrated = useHasHydrated();

  const accessToken = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const mustChangePassword = useAuthStore((s) => s.user?.mustChangePassword ?? false);

  const isOnChangePassword = pathname === CHANGE_PASSWORD_PATH;
  const needsRedirectToChangePassword = mustChangePassword && !isOnChangePassword;

  // PRD §3.2 Permission Matrix: mỗi khu vực chỉ dành cho một số vai trò. Điều hướng sau
  // đăng nhập (BR-03) không đủ — người dùng vẫn có thể tự gõ đường dẫn lên thanh địa chỉ.
  //
  // CHỐT AN TOÀN: chỉ coi là sai khu vực khi thật sự có chỗ KHÁC để đưa họ tới. Nếu đích
  // đến lại trùng đúng trang đang đứng thì màn hình sẽ kẹt vĩnh viễn ở vòng quay "đang kiểm
  // tra phiên đăng nhập" — người dùng không thấy lỗi, không thấy nội dung, không làm gì được.
  // Thà cho vào và để backend từ chối, còn hơn treo cứng giao diện.
  const homePath = role === undefined ? null : getHomePathForRole(role);

  const isWrongArea =
    !needsRedirectToChangePassword &&
    role !== undefined &&
    !isRoleAllowedOnPath(role, pathname) &&
    homePath !== pathname;

  const isAllowed =
    Boolean(accessToken) && !needsRedirectToChangePassword && !isWrongArea;

  useEffect(() => {
    // Wait for localStorage to be read, otherwise a valid session gets bounced out.
    if (!hasHydrated) return;

    if (!accessToken) {
      router.replace("/login");
      return;
    }

    // UC-25: an account forced to change its password may only reach the change-password
    // screen; everything else stays blocked until that is done.
    if (needsRedirectToChangePassword) {
      router.replace(CHANGE_PASSWORD_PATH);
      return;
    }

    // Sai khu vực thì đưa về đúng khu vực của vai trò, không hiện trang "cấm truy cập" —
    // người dùng hợp lệ gõ nhầm đường dẫn không cần bị doạ.
    if (isWrongArea && homePath !== null) {
      router.replace(homePath);
    }
  }, [
    hasHydrated,
    accessToken,
    needsRedirectToChangePassword,
    isWrongArea,
    homePath,
    router,
  ]);

  if (!hasHydrated || !isAllowed) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
        <span className="sr-only">Đang kiểm tra phiên đăng nhập</span>
      </div>
    );
  }

  return <>{children}</>;
}
