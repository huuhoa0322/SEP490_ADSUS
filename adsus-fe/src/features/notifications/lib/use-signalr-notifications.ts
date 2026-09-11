"use client";

import { useEffect, useRef, useCallback, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/store/auth-store";
import { NOTIFICATION_KEYS } from "../hooks/use-notifications";
import { API_BASE_URL } from "@/lib/api-client";

const HUB_URL = `${API_BASE_URL}/hubs/notifications`;

// Refresh token every 10 minutes (before 15-minute access token expiry)
const TOKEN_REFRESH_INTERVAL = 10 * 60 * 1000;

interface NotificationMessage {
  logId: string;
  type: string;
  title: string;
  body: string | null;
  deepLink: string | null;
  sentAt: string;
}

export function useSignalRNotifications() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const queryClient = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const accessToken = useAuthStore((s) => s.accessToken);
  const refreshAccessToken = useAuthStore((s) => s.refreshAccessToken);
  const [isConnected, setIsConnected] = useState(false);

  const startConnection = useCallback(async () => {
    if (!user || !accessToken) return;

    // Nếu đã có connection đang kết nối, không tạo lại
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      // Stop existing connection if any
      if (connectionRef.current) {
        try {
          await connectionRef.current.stop();
        } catch {
          // Ignore stop errors
        }
      }

      // SignalR sẽ tự thêm access_token vào query string khi negotiate
      // accessTokenFactory được dùng cho tất cả requests (negotiate + hub)
      // Đọc trực tiếp từ localStorage để đảm bảo luôn có token mới nhất
      const getAccessToken = () => {
        if (typeof window !== "undefined" && window.localStorage) {
          return window.localStorage.getItem("adsus.accessToken") || "";
        }
        return "";
      };

      const connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL, {
          accessTokenFactory: getAccessToken,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Retry: 0s → 2s → 5s → 10s → 30s
            if (retryContext.previousRetryCount < 4) {
              return [0, 2000, 5000, 10000, 30000][retryContext.previousRetryCount];
            }
            return 30000;
          },
        })
        // Tắt logging để tránh spam console
        .configureLogging(signalR.LogLevel.None)
        .build();

      // Xử lý khi nhận notification
      connection.on("ReceiveNotification", (notification: NotificationMessage) => {
        console.log("📬 SignalR: Received notification:", notification);
        // Invalidate và ép refetch ngay lập tức cache
        queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
        queryClient.refetchQueries({ queryKey: NOTIFICATION_KEYS.infiniteList() });
        queryClient.refetchQueries({ queryKey: NOTIFICATION_KEYS.unreadCount() });
      });

      // Connection events
      connection.on("Connected", (connectionId: string) => {
        console.log("✅ SignalR: Connected successfully with ID:", connectionId);
        setIsConnected(true);
      });

      connection.onreconnecting((error) => {
        console.warn("⚠️ SignalR: Reconnecting...", error?.message);
        setIsConnected(false);
      });

      connection.onreconnected((connectionId) => {
        console.log("✅ SignalR: Reconnected with ID:", connectionId);
        setIsConnected(true);
        // Refetch notifications when reconnected
        queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
        queryClient.refetchQueries({ queryKey: NOTIFICATION_KEYS.infiniteList() });
        queryClient.refetchQueries({ queryKey: NOTIFICATION_KEYS.unreadCount() });
      });

      connection.onclose((error) => {
        console.warn("⚠️ SignalR: Connection closed", error?.message);
        setIsConnected(false);
      });

      // Start connection
      await connection.start();
      connectionRef.current = connection;
      setIsConnected(true);
      console.log("✅ SignalR: Ready and connected");
    } catch (error: unknown) {
      const err = error as Error;
      // Không log error nếu là transport errors - đây là bình thường
      if (err?.message && !err.message.includes("WebSocket") && !err.message.includes("ServerSentEvents")) {
        console.error("SignalR: Failed to initialize:", err.message);
        // Nếu lỗi 401, thử refresh token ngay lập tức
        if (err.message.includes("401")) {
          refreshAccessToken().then((success) => {
            if (success && connectionRef.current) {
               connectionRef.current.start().catch(e => console.error("SignalR: Retry failed", e));
            }
          });
        }
      }
    }
  }, [user, accessToken, queryClient, refreshAccessToken]);

  const stopConnection = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.stop();
        setIsConnected(false);
      } catch {
        // Ignore stop errors
      }
      connectionRef.current = null;
    }
  }, []);

  // Auto-refresh token every 10 minutes
  useEffect(() => {
    if (!user) return;

    const refreshToken = async () => {
      console.log("SignalR: Refreshing token...");
      const success = await refreshAccessToken();
      if (success) {
        console.log("SignalR: Token refreshed, connection will auto-reconnect");
      } else {
        console.warn("SignalR: Token refresh failed, will retry on next interval");
      }
    };

    // Initial refresh after 10 minutes
    const timer = setInterval(refreshToken, TOKEN_REFRESH_INTERVAL);

    return () => clearInterval(timer);
  }, [user, refreshAccessToken]);

  // Start/stop connection based on auth state
  useEffect(() => {
    let mounted = true;

    const manageConnection = async () => {
      if (!mounted) return;

      if (user && accessToken) {
        await startConnection();
      } else {
        await stopConnection();
      }
    };

    manageConnection();

    return () => {
      mounted = false;
      stopConnection();
    };
  }, [user, accessToken, startConnection, stopConnection]);

  // Listen for token refresh via localStorage change
  useEffect(() => {
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === "adsus.accessToken" && connectionRef.current) {
        console.log("SignalR: Token changed in storage, reconnecting...");
        connectionRef.current.stop().then(() => {
          startConnection();
        });
      }
    };

    window.addEventListener("storage", handleStorageChange);
    return () => window.removeEventListener("storage", handleStorageChange);
  }, [startConnection]);

  return { startConnection, stopConnection, isConnected };
}
