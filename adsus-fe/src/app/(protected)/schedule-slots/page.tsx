import type { Metadata } from "next";

import { ScheduleSlotListView } from "@/features/appointment-scheduling/components/schedule-slot-list-view";

export const metadata: Metadata = { title: "Quản lý khung giờ | ADSUS" };

export default function ScheduleSlotsPage() {
  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-[#223a66]">
        Quản lý khung giờ khám
      </h1>
      <p className="mt-2 text-muted-foreground">
        Đăng ký và quản lý các khung giờ khám bệnh.
      </p>
      <div className="mt-8">
        <ScheduleSlotListView />
      </div>
    </div>
  );
}