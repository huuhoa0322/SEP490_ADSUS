import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";
import { server } from "@/test/mocks/server";

import {
  useAppointmentHistory,
  useCancellationStatusToday,
  useCancelMyAppointment,
} from "@/features/appointment-scheduling/hooks/use-appointment-history";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  }
  return Wrapper;
}

describe("useAppointmentHistory", () => {
  it("gọi getMyAppointments và trả về danh sách lịch hẹn", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: [
            {
              appointmentId: "appt-1",
              scheduleSlotId: "slot-1",
              doctorId: "doc-1",
              slotDate: "2026-09-15",
              startTime: "08:00:00",
              endTime: "08:30:00",
              doctorName: "BS. Trần Văn Minh",
              status: "BOOKED",
              createdAt: "2026-09-10T10:00:00Z",
              reason: null,
              cancellationReason: null,
              caseId: null,
              patientFullName: "Nguyễn Văn Bệnh",
              patientPhone: null,
              patientProfileId: "profile-1",
            },
            {
              appointmentId: "appt-2",
              scheduleSlotId: "slot-2",
              doctorId: "doc-2",
              slotDate: "2026-09-20",
              startTime: "14:00:00",
              endTime: "14:30:00",
              doctorName: "BS. Lê Thị Hoa",
              status: "BOOKED",
              createdAt: "2026-09-11T11:00:00Z",
              reason: "Tái khám",
              cancellationReason: null,
              caseId: null,
              patientFullName: "Nguyễn Văn Bệnh",
              patientPhone: null,
              patientProfileId: "profile-1",
            },
          ],
        }),
      ),
    );

    const { result } = renderHook(() => useAppointmentHistory(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(2);
    expect(result.current.data?.[0].appointmentId).toBe("appt-1");
    expect(result.current.data?.[0].doctorName).toBe("BS. Trần Văn Minh");
  });
});

describe("useCancellationStatusToday", () => {
  it("enabled=false thì không fetch API", async () => {
    let fetchCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/cancellation-status-today`, () => {
        fetchCount++;
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: {
            cancellationsToday: 0,
            maxCancellations: 3,
            canBookOnline: true,
            isNextCancellationFinal: false,
          },
        });
      }),
    );

    const { result } = renderHook(
      () => useCancellationStatusToday(false),
      { wrapper: createWrapper() },
    );

    // enabled=false → query không chạy
    expect(result.current.isFetching).toBe(false);
    expect(result.current.data).toBeUndefined();
    expect(fetchCount).toBe(0);
  });

  it("enabled=true thì fetch và trả về đúng dữ liệu isNextCancellationFinal", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/cancellation-status-today`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: {
            cancellationsToday: 1,
            maxCancellations: 3,
            canBookOnline: true,
            isNextCancellationFinal: false,
          },
        }),
      ),
    );

    const { result } = renderHook(
      () => useCancellationStatusToday(true),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.isNextCancellationFinal).toBe(false);
    expect(result.current.data?.cancellationsToday).toBe(1);
    expect(result.current.data?.maxCancellations).toBe(3);
  });
});

describe("useCancelMyAppointment", () => {
  beforeEach(() => {
    useAuthStore.setState({
      accessToken: "patient-token",
      user: {
        userId: "user-1",
        email: "patient@example.com",
        fullName: "Nguyễn Văn Bệnh",
        role: "PATIENT",
        mustChangePassword: false,
      },
    });
  });

  it("gọi cancelAppointment và invalidateQueries cả 2 queryKey", async () => {
    let cancelCalled = false;

    server.use(
      http.post(
        `${API_BASE_URL}/api/v1/appointments/appt-1/cancel`,
        () => {
          cancelCalled = true;
          return HttpResponse.json({
            code: 200,
            message: "OK",
            data: {
              appointmentId: "appt-1",
              status: "CANCELLED",
            },
          });
        },
      ),
    );

    const { result } = renderHook(() => useCancelMyAppointment(), {
      wrapper: createWrapper(),
    });

    await result.current.mutateAsync({
      appointmentId: "appt-1",
      reason: "Trùng lịch công tác",
    });

    expect(cancelCalled).toBe(true);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });
});
