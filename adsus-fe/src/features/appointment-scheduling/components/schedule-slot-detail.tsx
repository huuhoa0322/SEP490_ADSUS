"use client";

import {
  useCloseScheduleSlot,
  useScheduleSlotAppointments,
  useScheduleSlotDetail,
} from "../hooks/use-schedule-slots";
import { AppointmentsTable } from "./appointments-table";

interface Props {
  slotId: string;
}

export function ScheduleSlotDetail({ slotId }: Props) {
  const { data, isLoading, isError } = useScheduleSlotDetail(slotId);
  const appts = useScheduleSlotAppointments(slotId);
  const close = useCloseScheduleSlot();

  if (isLoading) return <p className="text-muted-foreground">Đang tải...</p>;
  if (isError || !data)
    return <p className="text-destructive">Không tải được khung giờ.</p>;

  return (
    <article className="space-y-6">
      <header>
        <p className="text-sm text-muted-foreground">Bác sĩ {data.doctorName}</p>
        <h2 className="font-heading text-2xl font-bold text-[#223a66]">
          {data.slotDate} · {data.startTime.slice(0, 5)}-{data.endTime.slice(0, 5)}
        </h2>
        <p className="mt-1 text-sm">
          Trạng thái:{" "}
          <span
            className={
              data.status === "OPEN" ? "text-[#1cba9f]" : "text-muted-foreground"
            }
          >
            {data.status === "OPEN" ? "Đang mở" : "Đã đóng"}
          </span>
        </p>
      </header>

      {data.status === "OPEN" && (
        <button
          type="button"
          onClick={() => close.mutate(slotId)}
          disabled={close.isPending}
          className="rounded-md border px-4 py-2 text-sm text-red-600 transition hover:bg-red-50 disabled:opacity-50"
        >
          {close.isPending ? "Đang đóng..." : "Đóng khung giờ này"}
        </button>
      )}

      <section>
        <h3 className="mb-3 font-heading text-lg font-semibold text-[#223a66]">
          Bệnh nhân đặt lịch ({appts.data?.totalItems ?? 0})
        </h3>
        <AppointmentsTable
          data={appts.data?.items ?? []}
          loading={appts.isLoading}
        />
      </section>
    </article>
  );
}