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

/** Ngày ISO yyyy-MM-dd theo lịch ĐỊA PHƯƠNG (không dùng toISOString — quy về UTC sai ngày). */
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

    const byTime = new Map<string, DoctorPatientAppointment[]>();
    for (const appointment of dayAppointments) {
      const key = appointment.startTime;
      const list = byTime.get(key) ?? [];
      list.push(appointment);
      byTime.set(key, list);
    }

    const groups: AppointmentTimeGroup[] = Array.from(byTime.entries())
      .sort(([timeA], [timeB]) => timeA.localeCompare(timeB))
      .map(([startTime, groupAppointments]) => ({
        startTime,
        endTime: groupAppointments[0].endTime,
        appointments: groupAppointments,
      }));

    days.push({ dateIso, groups });
  }

  return days;
}
