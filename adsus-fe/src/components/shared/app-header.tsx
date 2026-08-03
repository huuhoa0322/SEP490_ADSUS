"use client";

import { BrainCircuit, KeyRound, LogOut, ScanLine, Users } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import { getHomePathForRole, useAuthStore } from "@/store/auth-store";
import type { Role } from "@/types/api.types";

const ROLE_LABEL: Record<Role, string> = {
  ADMIN: "Quản trị viên",
  DOCTOR: "Bác sĩ",
  NURSE: "Điều dưỡng",
  PATIENT: "Bệnh nhân",
};

export function AppHeader() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const signOut = useAuthStore((s) => s.signOut);

  function handleSignOut() {
    // UC-01 step 5: end the session and return the user to the sign-in screen.
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    signOut();
    router.replace("/login");
  }

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-background/95 backdrop-blur">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-6">
        {/* Trỏ về khu vực của chính vai trò. Trước đây trỏ "/" mà "/" lại chuyển thẳng sang
            trang đăng nhập — bấm vào logo là như bị đăng xuất. */}
        <Link
          href={user ? getHomePathForRole(user.role) : "/login"}
          className="flex items-center gap-2.5"
        >
          <span className="flex size-9 items-center justify-center rounded-full bg-primary">
            <ScanLine className="size-4.5 text-primary-foreground" />
          </span>
          <span className="font-heading text-lg font-bold tracking-[-0.02em] text-primary">
            ADSUS
          </span>
        </Link>

        <div className="flex items-center gap-2">
          {user && (
            <div className="mr-2 hidden text-right sm:block">
              <p className="font-heading text-sm font-600 leading-tight text-foreground">
                {user.fullName}
              </p>
              <p className="text-xs text-muted-foreground">{ROLE_LABEL[user.role]}</p>
            </div>
          )}

          {/* SCR-06 — chỉ Admin thấy. Không thấy nút không có nghĩa là không vào được:
              chặn thật nằm ở AuthGuard và ở [Authorize(Roles = "ADMIN")] phía backend. */}
          {user?.role === "ADMIN" && (
            <>
              <Link
                href="/admin/users"
                className="flex items-center gap-2 rounded-full px-3.5 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
              >
                <Users className="size-4" />
                <span className="hidden sm:inline">Tài khoản</span>
              </Link>
              <Link
                href="/admin/ai-models"
                className="flex items-center gap-2 rounded-full px-3.5 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
              >
                <BrainCircuit className="size-4" />
                <span className="hidden sm:inline">Mô hình AI</span>
              </Link>
            </>
          )}

          <Link
            href="/change-password"
            className="flex items-center gap-2 rounded-full px-3.5 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-primary"
          >
            <KeyRound className="size-4" />
            <span className="hidden sm:inline">Đổi mật khẩu</span>
          </Link>

          <button
            type="button"
            onClick={handleSignOut}
            className="flex items-center gap-2 rounded-full px-3.5 py-2 text-sm text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
          >
            <LogOut className="size-4" />
            <span className="hidden sm:inline">Đăng xuất</span>
          </button>
        </div>
      </div>
    </header>
  );
}
