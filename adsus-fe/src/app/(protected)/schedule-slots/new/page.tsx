import type { Metadata } from "next";

import { ScheduleSlotCreateForm } from "@/features/appointment-scheduling/components/schedule-slot-create-form";

export const metadata: Metadata = { title: "Tạo khung giờ | ADSUS" };

interface PageProps {
  searchParams: Promise<{ doctorId?: string }>;
}

/**
 * Module 8 UC-15 — tạo slot mới.
 *
 * Lấy doctorId từ query string (Doctor tự truyền của mình,
 * Nurse truyền của bác sĩ mà mình đăng ký lịch hộ).
 */
export default async function NewScheduleSlotPage({ searchParams }: PageProps) {
  const { doctorId } = await searchParams;

  if (!doctorId) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-10">
        <h1 className="font-heading text-[32px] font-bold text-[#223a66]">Tạo khung giờ</h1>
        <p className="mt-4 text-destructive">
          Thiếu doctorId trên URL. Vui lòng thêm <code>?doctorId=&lt;uuid&gt;</code>.
        </p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl px-6 py-10">
      <h1 className="font-heading text-[32px] font-bold text-[#223a66]">Tạo khung giờ</h1>
      <p className="mt-2 text-muted-foreground">
        Tạo khung giờ khám mới theo ngày và khung giờ trong ngày.
      </p>
      <div className="mt-8">
        <ScheduleSlotCreateForm doctorId={doctorId} doctorName={doctorId} />
      </div>
    </div>
  );
}