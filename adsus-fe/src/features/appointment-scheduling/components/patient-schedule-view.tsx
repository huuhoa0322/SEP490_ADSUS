"use client";

import { AlertCircle, ChevronLeft, ChevronRight, Loader2 } from "lucide-react";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useDoctorAppointments } from "../hooks/use-doctor-appointments";
import { addDays, groupAppointmentsByWeek, toIsoDate } from "../lib/group-appointments-by-week";

const WEEKDAY_LABELS_VI = ["Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ Nhật"];

/** T2 của tuần chứa `date`. Sao chép từ schedule-slot-management-view.tsx (không import chéo
 * — hai màn hình độc lập hoàn toàn theo quyết định thiết kế 28/08/2026). */
function mondayOfWeek(date: Date): Date {
  const dayOfWeek = date.getDay();
  const offset = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
  return addDays(date, offset);
}

/**
 * SCR mới (28/08/2026) — "Lịch bệnh nhân": Doctor xem nhanh ai đã đặt lịch với mình theo
 * tuần, chỉ đọc, không thao tác gì. Cố ý KHÔNG dùng chung dữ liệu/API với màn Quản lý lịch
 * (schedule-slot-management-view.tsx) — xem design doc 2026-08-28.
 */
export function PatientScheduleView() {
  const [weekAnchor, setWeekAnchor] = useState<Date>(() => mondayOfWeek(new Date()));

  const weekStart = mondayOfWeek(weekAnchor);
  const weekEnd = addDays(weekStart, 6);
  const fromDate = toIsoDate(weekStart);
  const toDate = toIsoDate(weekEnd);

  const { data, isLoading, isPlaceholderData, isError, error } = useDoctorAppointments({ fromDate, toDate });

  const days = groupAppointmentsByWeek(weekStart, data ?? []);

  return (
    <div className="mx-auto w-full max-w-screen-2xl space-y-6 px-6 py-8">
      <header>
        <h1 className="font-heading text-2xl font-semibold text-foreground">Lịch bệnh nhân</h1>
        <p className="text-sm text-muted-foreground">
          Danh sách bệnh nhân đã đặt lịch với bạn, theo tuần. Chỉ xem, không quản lý khung giờ.
        </p>
      </header>

      <div className="flex items-center justify-between rounded-md border border-border bg-background p-3">
        <button
          type="button"
          onClick={() => setWeekAnchor(addDays(weekAnchor, -7))}
          className="rounded-md border border-border p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
          title="Tuần trước"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <h2 className="font-heading text-lg font-semibold text-foreground">
          Tuần {fromDate} → {toDate}
          {isPlaceholderData && (
            // F4 fix: data hiện có là placeholderData của tuần TRƯỚC (placeholderData: (previous)
            // => previous trong useDoctorAppointments) — báo cho người dùng biết đang tải tuần
            // mới, thay vì im lặng hiện dữ liệu/thông báo trống của tuần cũ.
            <span role="status" className="ml-2 inline-flex items-center gap-1 align-middle text-xs font-normal text-muted-foreground">
              <Loader2 className="h-3 w-3 animate-spin" /> Đang cập nhật…
            </span>
          )}
        </h2>
        <button
          type="button"
          onClick={() => setWeekAnchor(addDays(weekAnchor, 7))}
          className="rounded-md border border-border p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
          title="Tuần sau"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>

      {isError && (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-md border border-destructive/25 bg-destructive/5 p-4 text-sm text-destructive"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          {getApiErrorMessage(error, "Không tải được lịch bệnh nhân.")}
        </div>
      )}

      {isLoading && !data ? (
        <div className="flex items-center justify-center gap-2 rounded-md border border-border bg-background p-8 text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Đang tải…
        </div>
      ) : (
        <div className={`grid grid-cols-7 gap-3 ${isPlaceholderData ? "opacity-50" : ""}`}>
          {days.map((day, i) => (
            <div key={day.dateIso} className="flex min-h-[200px] flex-col gap-2 rounded border border-border bg-background p-2">
              <div className="text-center text-sm font-semibold text-muted-foreground">
                {WEEKDAY_LABELS_VI[i]}
                <div className="text-xs font-normal text-muted-foreground/70">{day.dateIso}</div>
              </div>

              {/* F4 fix: khi isPlaceholderData, `data` vẫn còn là dữ liệu tuần CŨ (do
                  placeholderData: (previous) => previous), nên groups rỗng ở đây không có nghĩa
                  tuần mới thật sự không có bệnh nhân — chỉ là chưa tải xong. Không hiện thông báo
                  trống gây hiểu lầm; đã có chỉ báo "Đang cập nhật…" ở header thay thế. */}
              {day.groups.length === 0 && !isPlaceholderData && (
                <p className="mt-2 text-center text-xs text-muted-foreground/70">Không có bệnh nhân</p>
              )}

              {day.groups.map((group) => (
                <div key={group.startTime} className="rounded border border-[var(--chart-3)]/20 bg-[var(--chart-3)]/6 p-2 text-xs">
                  <div className="font-mono font-medium text-[var(--chart-3)]">
                    {group.startTime.slice(0, 5)}–{group.endTime.slice(0, 5)}
                  </div>
                  {group.appointments.map((a) => (
                    <div key={a.appointmentId} className="mt-1 text-foreground">
                      {a.patientFullName}
                      {a.reason && <span className="text-muted-foreground"> · {a.reason}</span>}
                    </div>
                  ))}
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
