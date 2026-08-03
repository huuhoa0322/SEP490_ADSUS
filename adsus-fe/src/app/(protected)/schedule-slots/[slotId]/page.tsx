import type { Metadata } from "next";

import { ScheduleSlotDetail } from "@/features/appointment-scheduling/components/schedule-slot-detail";

export const metadata: Metadata = { title: "Chi tiết khung giờ | ADSUS" };

interface Props {
  params: Promise<{ slotId: string }>;
}

export default async function ScheduleSlotDetailPage({ params }: Props) {
  const { slotId } = await params;
  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <a
        href="/schedule-slots"
        className="mb-4 inline-block text-sm text-[#4488be] hover:underline"
      >
        ← Quay lại
      </a>
      <ScheduleSlotDetail slotId={slotId} />
    </div>
  );
}