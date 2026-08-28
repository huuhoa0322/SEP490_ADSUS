"use client";

import { AlertCircle, ChevronLeft, ChevronRight, Loader2 } from "lucide-react";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useDoctorAppointments } from "../hooks/use-doctor-appointments";
import { groupAppointmentsByWeek } from "../lib/group-appointments-by-week";

const WEEKDAY_LABELS_VI = ["Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ Nhật"];

function toIsoDate(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(date.getDate() + days);
  return next;
}

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

  const { data, isLoading, isError, error } = useDoctorAppointments({ fromDate, toDate });

  const days = groupAppointmentsByWeek(weekStart, data ?? []);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="font-heading text-2xl font-semibold">Lịch bệnh nhân</h1>
        <p className="text-sm text-slate-500">
          Danh sách bệnh nhân đã đặt lịch với bạn, theo tuần. Chỉ xem, không quản lý khung giờ.
        </p>
      </header>

      <div className="flex items-center justify-between rounded-md border border-slate-200 bg-white p-3">
        <button
          type="button"
          onClick={() => setWeekAnchor(addDays(weekAnchor, -7))}
          className="rounded-md border border-slate-300 p-1 hover:bg-slate-50"
          title="Tuần trước"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <h2 className="font-heading text-lg font-semibold">
          Tuần {fromDate} → {toDate}
        </h2>
        <button
          type="button"
          onClick={() => setWeekAnchor(addDays(weekAnchor, 7))}
          className="rounded-md border border-slate-300 p-1 hover:bg-slate-50"
          title="Tuần sau"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>

      {isError && (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          {getApiErrorMessage(error, "Không tải được lịch bệnh nhân.")}
        </div>
      )}

      {isLoading && !data ? (
        <div className="flex items-center justify-center gap-2 rounded-md border border-slate-200 bg-white p-8 text-slate-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Đang tải…
        </div>
      ) : (
        <div className="grid grid-cols-7 gap-3">
          {days.map((day, i) => (
            <div key={day.dateIso} className="flex min-h-[200px] flex-col gap-2 rounded border border-slate-200 bg-white p-2">
              <div className="text-center text-sm font-semibold text-slate-500">
                {WEEKDAY_LABELS_VI[i]}
                <div className="text-xs font-normal text-slate-400">{day.dateIso}</div>
              </div>

              {day.groups.length === 0 && (
                <p className="mt-2 text-center text-xs text-slate-400">Không có bệnh nhân</p>
              )}

              {day.groups.map((group) => (
                <div key={group.startTime} className="rounded border border-slate-100 bg-slate-50 p-2 text-xs">
                  <div className="font-mono font-medium text-slate-600">
                    {group.startTime.slice(0, 5)}–{group.endTime.slice(0, 5)}
                  </div>
                  {group.appointments.map((a) => (
                    <div key={a.appointmentId} className="mt-1 text-blue-700">
                      {a.patientFullName}
                      {a.reason && <span className="text-slate-400"> · {a.reason}</span>}
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
