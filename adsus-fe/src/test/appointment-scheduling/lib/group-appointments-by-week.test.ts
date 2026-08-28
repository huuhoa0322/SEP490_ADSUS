import { describe, expect, it } from "vitest";

import { groupAppointmentsByWeek } from "@/features/appointment-scheduling/lib/group-appointments-by-week";
import type { DoctorPatientAppointment } from "@/features/appointment-scheduling/types/doctor-appointment.types";

function buildAppointment(overrides: Partial<DoctorPatientAppointment>): DoctorPatientAppointment {
  return {
    appointmentId: "appt-default",
    slotDate: "2026-07-06",
    startTime: "08:00:00",
    endTime: "08:30:00",
    patientProfileId: "profile-default",
    patientFullName: "Bệnh nhân mặc định",
    reason: null,
    ...overrides,
  };
}

describe("groupAppointmentsByWeek", () => {
  const monday = new Date(2026, 6, 6); // Thứ Hai 06/07/2026

  it("trả về đúng 7 ngày, thứ tự T2 -> CN", () => {
    const result = groupAppointmentsByWeek(monday, []);

    expect(result).toHaveLength(7);
    expect(result[0].dateIso).toBe("2026-07-06");
    expect(result[6].dateIso).toBe("2026-07-12");
  });

  it("ngày không có ai đặt lịch -> groups rỗng", () => {
    const result = groupAppointmentsByWeek(monday, []);

    expect(result.every((day) => day.groups.length === 0)).toBe(true);
  });

  it("nhóm đúng theo khung giờ, nhiều bệnh nhân cùng giờ nằm chung 1 group", () => {
    const appointments = [
      buildAppointment({ appointmentId: "a1", slotDate: "2026-07-06", startTime: "08:00:00", endTime: "08:30:00", patientFullName: "Nguyễn Văn A" }),
      buildAppointment({ appointmentId: "a2", slotDate: "2026-07-06", startTime: "08:00:00", endTime: "08:30:00", patientFullName: "Trần Thị B" }),
      buildAppointment({ appointmentId: "a3", slotDate: "2026-07-06", startTime: "09:00:00", endTime: "09:30:00", patientFullName: "Lê Văn C" }),
    ];

    const result = groupAppointmentsByWeek(monday, appointments);
    const monday06 = result.find((d) => d.dateIso === "2026-07-06")!;

    expect(monday06.groups).toHaveLength(2);
    expect(monday06.groups[0].startTime).toBe("08:00:00");
    expect(monday06.groups[0].appointments).toHaveLength(2);
    expect(monday06.groups[1].startTime).toBe("09:00:00");
    expect(monday06.groups[1].appointments).toHaveLength(1);
  });

  it("appointment ngoài tuần (slotDate không khớp 7 ngày) bị bỏ qua", () => {
    const appointments = [buildAppointment({ slotDate: "2026-07-20" })];

    const result = groupAppointmentsByWeek(monday, appointments);

    expect(result.every((day) => day.groups.length === 0)).toBe(true);
  });
});
