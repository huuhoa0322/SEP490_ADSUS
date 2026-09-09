import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { useDoctorAppointments } from "@/features/appointment-scheduling/hooks/use-doctor-appointments";

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useDoctorAppointments", () => {
  it("tải danh sách thành công qua listDoctorAppointments thật", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/doctor`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: [
            {
              appointmentId: "appt-1",
              slotDate: "2026-07-10",
              startTime: "08:30:00",
              endTime: "09:00:00",
              patientProfileId: "profile-1",
              patientFullName: "Nguyễn Thị Lan",
              reason: null,
            },
          ],
        }),
      ),
    );

    const { result } = renderHook(
      () => useDoctorAppointments({ fromDate: "2026-07-10", toDate: "2026-07-16" }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].patientFullName).toBe("Nguyễn Thị Lan");
  });

  it("backend lỗi — isError true", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/doctor`, () =>
        HttpResponse.json(
          { code: 500, message: "An unexpected error occurred. Please try again later.", data: null },
          { status: 500 },
        ),
      ),
    );

    const { result } = renderHook(
      () => useDoctorAppointments({ fromDate: "2026-07-10", toDate: "2026-07-16" }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCreateFollowUpAppointment", () => {
  it("gọi mutation tạo lịch tái khám thành công", async () => {
    let called = false;
    server.use(
      http.post(`${API_BASE_URL}/api/v1/appointments/follow-up`, () => {
        called = true;
        return HttpResponse.json(
          { code: 201, message: "OK", data: { appointmentId: "appt-1" } },
          { status: 201 },
        );
      }),
    );

    const { useCreateFollowUpAppointment } = await import(
      "@/features/appointment-scheduling/hooks/use-doctor-appointments"
    );

    const { result } = renderHook(() => useCreateFollowUpAppointment(), {
      wrapper: createWrapper(),
    });

    await result.current.mutateAsync({
      patientProfileId: "p-1",
      scheduleSlotId: "s-1",
      reason: "Tái khám",
    });

    expect(called).toBe(true);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});
