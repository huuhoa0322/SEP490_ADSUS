"use client";

import { useEffect } from "react";
import { useSignalRNotifications } from "@/features/notifications/lib/use-signalr-notifications";
import { useAuthStore } from "@/store/auth-store";

export function SignalRProvider({ children }: { children: React.ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const accessToken = useAuthStore((s) => s.accessToken);
  const { isConnected } = useSignalRNotifications();

  useEffect(() => {
    if (user) {
      console.log(
        `[SignalRProvider] ${isConnected ? "✅ Connected" : "⏳ Connecting..."}`
      );
    }
  }, [user, isConnected]);

  return <>{children}</>;
}
