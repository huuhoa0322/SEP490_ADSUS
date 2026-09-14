import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import { format, addDays, startOfDay } from "date-fns";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";
import { useBookingForm } from "@/features/appointment-scheduling/hooks/use-booking-form";

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

describe("useBookingForm", () => {
  const today = startOfDay(new Date());
  const tomorrow = addDays(today, 1);
  const tomorrowStr = format(tomorrow, "yyyy-MM-dd");

  const mockSlots = [
    {
      slotId: "slot-male-1",
      doctorId: "doc-1",
      doctorName: "Nguyễn Văn Nam",
      doctorStatus: "ACTIVE",
      doctorGender: "MALE",
      slotDate: tomorrowStr,
      startTime: "09:00:00",
      endTime: "09:30:00",
      createdAt: "2026-09-01T00:00:00Z",
    },
    {
      slotId: "slot-female-1",
      doctorId: "doc-2",
      doctorName: "Trần Thị Nữ",
      doctorStatus: "ACTIVE",
      doctorGender: "FEMALE",
      slotDate: tomorrowStr,
      startTime: "10:00:00",
      endTime: "10:30:00",
      createdAt: "2026-09-01T00:00:00Z",
    },
  ];

  it("chuỗi reset chuẩn Mobile: selectDoctorGender reset doctor, date, slot", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.allSlots).toHaveLength(2));

    // Chọn bác sĩ, ngày, slot
    act(() => {
      result.current.selectDoctor("doc-1");
      result.current.selectDate(tomorrowStr);
      result.current.selectSlot("slot-male-1");
    });

    expect(result.current.selectedDoctorId).toBe("doc-1");
    expect(result.current.selectedDate).toBe(tomorrowStr);
    expect(result.current.selectedSlotId).toBe("slot-male-1");

    // Đổi giới tính bác sĩ -> Phải reset CẢ 3: doctor, date, slot
    act(() => {
      result.current.selectDoctorGender("FEMALE");
    });

    expect(result.current.selectedDoctorGender).toBe("FEMALE");
    expect(result.current.selectedDoctorId).toBeNull();
    expect(result.current.selectedDate).toBeNull();
    expect(result.current.selectedSlotId).toBeNull();
  });

  it("chuỗi reset: selectDoctor reset slot nhưng GIỮ selectedDate", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.allSlots).toHaveLength(2));

    act(() => {
      result.current.selectDate(tomorrowStr);
      result.current.selectSlot("slot-male-1");
    });

    expect(result.current.selectedDate).toBe(tomorrowStr);
    expect(result.current.selectedSlotId).toBe("slot-male-1");

    // Chọn bác sĩ khác
    act(() => {
      result.current.selectDoctor("doc-2");
    });

    expect(result.current.selectedDoctorId).toBe("doc-2");
    expect(result.current.selectedDate).toBe(tomorrowStr); // Giữ nguyên ngày!
    expect(result.current.selectedSlotId).toBeNull(); // Đã reset slot!
  });

  it("chuỗi reset: selectWeek reset date và slot", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.selectDate(tomorrowStr);
      result.current.selectSlot("slot-male-1");
    });

    // Chọn tuần khác
    act(() => {
      result.current.selectWeek(1);
    });

    expect(result.current.selectedWeekIndex).toBe(1);
    expect(result.current.selectedDate).toBeNull();
    expect(result.current.selectedSlotId).toBeNull();
  });

  it("chuỗi reset: selectDate reset slot", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.selectSlot("slot-male-1");
    });

    expect(result.current.selectedSlotId).toBe("slot-male-1");

    act(() => {
      result.current.selectDate(tomorrowStr);
    });

    expect(result.current.selectedDate).toBe(tomorrowStr);
    expect(result.current.selectedSlotId).toBeNull();
  });

  it("lọc filteredDoctorOptions chính xác theo giới tính", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.doctorOptions).toHaveLength(2));

    // Không chọn giới tính -> cả 2
    expect(result.current.filteredDoctorOptions).toHaveLength(2);

    // Chọn FEMALE -> chỉ 1
    act(() => {
      result.current.selectDoctorGender("FEMALE");
    });
    expect(result.current.filteredDoctorOptions).toHaveLength(1);
    expect(result.current.filteredDoctorOptions[0].id).toBe("doc-2");
  });

  it("visibleSlots lọc đúng theo bác sĩ và ngày đã chọn", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: mockSlots,
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.allSlots).toHaveLength(2));

    // Chưa chọn bác sĩ -> visibleSlots rỗng
    expect(result.current.visibleSlots).toEqual([]);

    // Chọn bác sĩ doc-1 nhưng chưa chọn ngày -> rỗng
    act(() => {
      result.current.selectDoctor("doc-1");
    });
    expect(result.current.visibleSlots).toEqual([]);

    // Chọn ngày tomorrowStr -> có 1 slot của doc-1
    act(() => {
      result.current.selectDate(tomorrowStr);
    });
    expect(result.current.visibleSlots).toHaveLength(1);
    expect(result.current.visibleSlots[0].slotId).toBe("slot-male-1");
  });

  it("đổi isBookingForSelf thành true sẽ reset selectedRelativeId", () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: [],
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.setIsBookingForSelf(false);
      result.current.setSelectedRelativeId("rel-123");
    });

    expect(result.current.isBookingForSelf).toBe(false);
    expect(result.current.selectedRelativeId).toBe("rel-123");

    act(() => {
      result.current.setIsBookingForSelf(true);
    });

    expect(result.current.isBookingForSelf).toBe(true);
    expect(result.current.selectedRelativeId).toBeNull();
  });

  it("giới hạn 30 ngày: query slots với fromDate là hôm nay và toDate là hôm nay + 30 ngày", async () => {
    let requestedFromDate: string | null = null;
    let requestedToDate: string | null = null;

    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, ({ request }) => {
        const url = new URL(request.url);
        requestedFromDate = url.searchParams.get("fromDate");
        requestedToDate = url.searchParams.get("toDate");
        return HttpResponse.json({
          code: 200,
          message: "OK",
          data: [],
        });
      }),
    );

    renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    const expectedFrom = format(today, "yyyy-MM-dd");
    const expectedTo = format(addDays(today, 30), "yyyy-MM-dd");

    await waitFor(() => {
      expect(requestedFromDate).toBe(expectedFrom);
      expect(requestedToDate).toBe(expectedTo);
    });
  });

  it("giới hạn 30 ngày: availableDates loại bỏ các ngày quá khứ hoặc vượt quá 30 ngày", async () => {
    const pastDateStr = format(addDays(today, -2), "yyyy-MM-dd");
    const validDateStr = format(addDays(today, 15), "yyyy-MM-dd");
    const futureBeyond30Str = format(addDays(today, 35), "yyyy-MM-dd");

    server.use(
      http.get(`${API_BASE_URL}/api/v1/appointments/slots`, () =>
        HttpResponse.json({
          code: 200,
          message: "OK",
          data: [
            {
              slotId: "slot-past",
              doctorId: "doc-1",
              doctorName: "Bác sĩ",
              slotDate: pastDateStr,
              startTime: "09:00:00",
              endTime: "09:30:00",
            },
            {
              slotId: "slot-valid",
              doctorId: "doc-1",
              doctorName: "Bác sĩ",
              slotDate: validDateStr,
              startTime: "10:00:00",
              endTime: "10:30:00",
            },
            {
              slotId: "slot-too-far",
              doctorId: "doc-1",
              doctorName: "Bác sĩ",
              slotDate: futureBeyond30Str,
              startTime: "11:00:00",
              endTime: "11:30:00",
            },
          ],
        }),
      ),
    );

    const { result } = renderHook(() => useBookingForm(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.allSlots).toHaveLength(3));

    // availableDates chỉ chứa validDate, không chứa pastDate hay futureBeyond30
    const availableDatesFormatted = result.current.availableDates.map((d) =>
      format(d, "yyyy-MM-dd"),
    );
    expect(availableDatesFormatted).toContain(validDateStr);
    expect(availableDatesFormatted).not.toContain(pastDateStr);
    expect(availableDatesFormatted).not.toContain(futureBeyond30Str);
  });
});

