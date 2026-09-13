"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore, useHasHydrated } from "@/store/auth-store";
import { BookingView } from "./booking-view";

/**
 * Wraps BookingView — chỉ Patient đã đăng nhập mới vào được trang đặt lịch.
 * Guest (chưa login) → redirect /login ngay.
 */
export function BookingPageWrapper() {
  const router = useRouter();
  const hasHydrated = useHasHydrated();
  const accessToken = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);

  useEffect(() => {
    // Chỉ chạy sau khi hydrate và khi chưa có token
    if (!hasHydrated || accessToken) return;
    router.replace("/login?redirect=/dat-lich");
  }, [hasHydrated, accessToken, router]);

  // Chưa hydrate xong → chờ
  if (!hasHydrated) return null;

  // Guest (chưa đăng nhập) → useEffect đã redirect rồi, return null tránh flash
  if (!accessToken) return null;

  // Không phải Patient → đâu đó sai trong luồng, về landing
  if (role !== "PATIENT") {
    router.replace("/");
    return null;
  }

  return (
    <BookingView
      requireAuth={false}
    />
  );
}
