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

describe("createFollowUpAppointment", () => {
  it("gửi payload chính xác đến POST /api/v1/appointments/follow-up", async () => {
    let capturedBody: unknown;

    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments/follow-up`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          {
            code: 201,
            message: "Tạo lịch hẹn tái khám thành công.",
            data: { appointmentId: "appt-123" },
          },
          { status: 201 },
        );
      }),
    );

    const { createFollowUpAppointment } = await import(
      "@/features/appointment-scheduling/api/doctor-appointment.api"
    );

    await expect(
      createFollowUpAppointment({
        patientProfileId: "profile-1",
        scheduleSlotId: "slot-1",
        reason: "Tái khám sau 2 tuần",
      }),
    ).resolves.toBeUndefined();

    expect(capturedBody).toEqual({
      patientProfileId: "profile-1",
      scheduleSlotId: "slot-1",
      reason: "Tái khám sau 2 tuần",
    });
  });

  it("ném lỗi khi Backend trả HTTP 400 Bad Request", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments/follow-up`, () => {
        return HttpResponse.json(
          {
            code: 400,
            message: "Khung giờ này đã được đặt hoặc đã đóng.",
            data: null,
          },
          { status: 400 },
        );
      }),
    );

    const { createFollowUpAppointment } = await import(
      "@/features/appointment-scheduling/api/doctor-appointment.api"
    );

    await expect(
      createFollowUpAppointment({
        patientProfileId: "profile-1",
        scheduleSlotId: "slot-1",
        reason: "Tái khám",
      }),
    ).rejects.toThrow();
  });

  it("ném lỗi khi Backend trả HTTP 404 Not Found", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments/follow-up`, () => {
        return HttpResponse.json(
          {
            code: 404,
            message: "Không tìm thấy khung giờ này.",
            data: null,
          },
          { status: 404 },
        );
      }),
    );

    const { createFollowUpAppointment } = await import(
      "@/features/appointment-scheduling/api/doctor-appointment.api"
    );

    await expect(
      createFollowUpAppointment({
        patientProfileId: "profile-1",
        scheduleSlotId: "slot-not-found",
        reason: "Tái khám",
      }),
    ).rejects.toThrow();
  });
});
