import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { listDoctorAppointments } from "@/features/appointment-scheduling/api/doctor-appointment.api";

describe("listDoctorAppointments", () => {
  it("gửi fromDate/toDate qua query param và trả về đúng dữ liệu", async () => {
    let capturedUrl: string | undefined;
    const entry = {
      appointmentId: "appt-1",
      slotDate: "2026-07-10",
      startTime: "08:30:00",
      endTime: "09:00:00",
      patientProfileId: "profile-1",
      patientFullName: "Nguyễn Thị Lan",
      reason: "Khám định kỳ",
    };

    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/doctor`, ({ request }) => {
        capturedUrl = request.url;
        return HttpResponse.json({ code: 200, message: "OK", data: [entry] });
      }),
    );

    const result = await listDoctorAppointments({ fromDate: "2026-07-10", toDate: "2026-07-16" });

    expect(result).toEqual([entry]);
    expect(capturedUrl).toContain("fromDate=2026-07-10");
    expect(capturedUrl).toContain("toDate=2026-07-16");
  });

  it("data null trên response 200 — ném lỗi thay vì coi là hợp lệ", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/doctor`, () =>
        HttpResponse.json({ code: 200, message: "Backend bug.", data: null }),
      ),
    );

    await expect(
      listDoctorAppointments({ fromDate: "2026-07-10", toDate: "2026-07-16" }),
    ).rejects.toThrow("Backend bug.");
  });
});
