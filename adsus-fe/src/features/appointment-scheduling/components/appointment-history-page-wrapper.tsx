"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore, useHasHydrated } from "@/store/auth-store";
import { AppointmentHistoryView } from "./appointment-history-view";

export function AppointmentHistoryPageWrapper() {
  const router = useRouter();
  const hasHydrated = useHasHydrated();
  const accessToken = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);

  useEffect(() => {
    if (!hasHydrated || accessToken) return;
    router.replace("/login?redirect=/lich-hen-cua-toi");
  }, [hasHydrated, accessToken, router]);

  if (!hasHydrated) return null;
  if (!accessToken) return null;
  if (role !== "PATIENT") {
    void router.replace("/");
    return null;
  }

  return <AppointmentHistoryView />;
}
