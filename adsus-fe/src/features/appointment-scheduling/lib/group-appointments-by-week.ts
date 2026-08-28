import type { DoctorPatientAppointment } from "../types/doctor-appointment.types";

export interface AppointmentTimeGroup {
  startTime: string;
  endTime: string;
  appointments: DoctorPatientAppointment[];
}

export interface DayAppointments {
  dateIso: string;
  groups: AppointmentTimeGroup[];
}

/**
 * Ngày ISO yyyy-MM-dd theo lịch ĐỊA PHƯƠNG (không dùng toISOString — quy về UTC sai ngày).
 * Export để `patient-schedule-view.tsx` dùng chung, tránh viết trùng lần thứ 2 (F3, 28/08/2026).
 */
export function toIsoDate(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

export function addDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(date.getDate() + days);
  return next;
}

/**
 * Nhóm appointment (đã lọc BOOKED/APPROVED sẵn ở Backend) theo 7 ngày trong tuần (T2 -> CN, tính
 * từ weekStart), rồi theo khung giờ trong mỗi ngày. Ngày/khung giờ không có ai thì mảng rỗng —
 * component tự quyết định hiện "Không có bệnh nhân" khi groups rỗng.
 */
export function groupAppointmentsByWeek(
  weekStart: Date,
  appointments: DoctorPatientAppointment[],
): DayAppointments[] {
  const days: DayAppointments[] = [];

  for (let i = 0; i < 7; i++) {
    const dateIso = toIsoDate(addDays(weekStart, i));
    const dayAppointments = appointments.filter((a) => a.slotDate === dateIso);

    // Key gồm cả startTime+endTime (không chỉ startTime) — lưới slot 30 phút hiện tại luôn cho
    // cùng startTime thì cùng endTime, nhưng nếu sau này có slot khác kiểu (vd đăng ký ngoài giờ)
    // trùng startTime nhưng khác endTime, gộp theo startTime sẽ hiện sai endTime (F7, 28/08/2026).
    const byTime = new Map<string, DoctorPatientAppointment[]>();
    for (const appointment of dayAppointments) {
      const key = `${appointment.startTime}|${appointment.endTime}`;
      const list = byTime.get(key) ?? [];
      list.push(appointment);
      byTime.set(key, list);
    }

    const groups: AppointmentTimeGroup[] = Array.from(byTime.values())
      .sort((a, b) => a[0].startTime.localeCompare(b[0].startTime))
      .map((groupAppointments) => ({
        startTime: groupAppointments[0].startTime,
        endTime: groupAppointments[0].endTime,
        appointments: groupAppointments,
      }));

    days.push({ dateIso, groups });
  }

  return days;
}
