"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Calendar, UserPlus } from "lucide-react";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { useAuthStore, useHasHydrated } from "@/store/auth-store";
import { BookingView } from "./booking-view";
import { AddRelativeForm } from "./add-relative-form";

/**
 * Wraps BookingView — chỉ Patient đã đăng nhập mới vào được trang đặt lịch.
 * Quản lý 2 Tab: Đặt lịch khám & Thêm người thân.
 * Dùng forceMount trên cả 2 TabsContent để giữ nguyên toàn bộ form state khi chuyển Tab.
 */
export function BookingPageWrapper() {
  const router = useRouter();
  const hasHydrated = useHasHydrated();
  const accessToken = useAuthStore((s) => s.accessToken);
  const role = useAuthStore((s) => s.user?.role);
  const [activeTab, setActiveTab] = useState<string>("booking");

  useEffect(() => {
    // Chỉ chạy sau khi hydrate và khi chưa có token
    if (!hasHydrated || accessToken) return;
    router.replace("/login?redirect=/dat-lich");
  }, [hasHydrated, accessToken, router]);

  // Chưa hydrate xong → chờ
  if (!hasHydrated) return null;

  // Guest (chưa đăng nhập) → useEffect đã redirect rồi, return null tránh flash
  if (!accessToken) return null;

  // Không phải Patient → về landing
  if (role !== "PATIENT") {
    router.replace("/");
    return null;
  }

  return (
    <div className="mx-auto w-full max-w-screen-md px-4 py-8">
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="grid w-full grid-cols-2 mb-8 h-12 p-1 bg-muted/60 rounded-xl">
          <TabsTrigger
            value="booking"
            className="flex items-center justify-center gap-2 rounded-lg font-medium transition-all"
          >
            <Calendar className="h-4 w-4" />
            <span>Đặt lịch khám</span>
          </TabsTrigger>
          <TabsTrigger
            value="add-relative"
            className="flex items-center justify-center gap-2 rounded-lg font-medium transition-all"
          >
            <UserPlus className="h-4 w-4" />
            <span>Thêm người thân</span>
          </TabsTrigger>
        </TabsList>

        {/* Tab 1: Luôn forceMount để giữ nguyên toàn bộ form state */}
        <TabsContent
          value="booking"
          forceMount
          className="data-[state=inactive]:hidden outline-none"
        >
          <BookingView onSwitchToAddRelative={() => setActiveTab("add-relative")} />
        </TabsContent>

        {/* Tab 2: Thêm người thân, khi xong tự chuyển về Tab 1 */}
        <TabsContent
          value="add-relative"
          forceMount
          className="data-[state=inactive]:hidden outline-none"
        >
          <AddRelativeForm onSuccess={() => setActiveTab("booking")} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
