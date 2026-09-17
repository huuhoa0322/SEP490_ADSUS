import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@testing-library/jest-dom/vitest";

import { AppointmentHistoryView } from "@/features/appointment-scheduling/components/appointment-history-view";
import type { AppointmentSummaryResponse } from "@/features/appointment-scheduling/types/booking.types";

const { routerPushMock, routerReplaceMock } = vi.hoisted(() => ({
  routerPushMock: vi.fn(),
  routerReplaceMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: routerPushMock,
    replace: routerReplaceMock,
  }),
}));

const _mockUseAppointmentHistory = vi.fn();
const _mockUseCancelMyAppointment = vi.fn();
const _mockUseCancellationStatusToday = vi.fn();

vi.mock(
  "@/features/appointment-scheduling/hooks/use-appointment-history",
  () => ({
    useAppointmentHistory: () => _mockUseAppointmentHistory(),
    useCancelMyAppointment: () => _mockUseCancelMyAppointment(),
    useCancellationStatusToday: (enabled?: boolean) => _mockUseCancellationStatusToday(enabled),
  })
);

function setupDefaultHooks(overrides: {
  data?: AppointmentSummaryResponse[];
  isLoading?: boolean;
  error?: Error | null;
  isPending?: boolean;
} = {}) {
  _mockUseAppointmentHistory.mockReturnValue({
    data: overrides.data ?? [],
    isLoading: overrides.isLoading ?? false,
    error: overrides.error ?? null,
    refetch: vi.fn(),
  });
  _mockUseCancelMyAppointment.mockReturnValue({
    mutate: vi.fn(),
    isPending: overrides.isPending ?? false,
  });
  _mockUseCancellationStatusToday.mockReturnValue({
    data: { isNextCancellationFinal: false },
    isLoading: false,
  });
}

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return Wrapper;
}

function makeAppt(overrides: Partial<AppointmentSummaryResponse> = {}): AppointmentSummaryResponse {
  return {
    appointmentId: "appt-001",
    scheduleSlotId: "slot-001",
    doctorId: "doc-001",
    slotDate: "2036-09-20",
    startTime: "08:00:00",
    endTime: "08:30:00",
    doctorName: "Nguyễn Văn Minh",
    status: "BOOKED",
    createdAt: "2026-09-01T10:00:00Z",
    reason: null,
    cancellationReason: null,
    caseId: null,
    isBookedForOthers: false,
    relationshipLabel: null,
    bookedByUserName: null,
    patientFullName: undefined,
    ...overrides,
  };
}

const SELF_APPT_1 = makeAppt({
  appointmentId: "self-1",
  doctorName: "Trần Thị Lan",
  slotDate: "2036-09-20",
  startTime: "09:00:00",
  endTime: "09:30:00",
  status: "BOOKED",
  isBookedForOthers: false,
});

const SELF_APPT_2 = makeAppt({
  appointmentId: "self-2",
  doctorName: "Lê Hoàng Nam",
  slotDate: "2036-09-21",
  startTime: "10:00:00",
  endTime: "10:30:00",
  status: "CANCELLED",
  cancellationReason: "Bận đột xuất",
  isBookedForOthers: false,
});

const SELF_APPT_3 = makeAppt({
  appointmentId: "self-3",
  doctorName: "Phạm Minh Đức",
  slotDate: "2036-09-22",
  startTime: "14:00:00",
  endTime: "14:30:00",
  status: "COMPLETED",
  isBookedForOthers: false,
});

const RELATIVE_APPT_1 = makeAppt({
  appointmentId: "rel-1",
  doctorName: "BS. Nguyễn Thanh Hà",
  slotDate: "2036-09-20",
  startTime: "15:00:00",
  endTime: "15:30:00",
  status: "APPROVED",
  isBookedForOthers: true,
  relationshipLabel: "Con",
  patientFullName: "Trần Văn Tiến",
});

const RELATIVE_APPT_2 = makeAppt({
  appointmentId: "rel-2",
  doctorName: "BS. Hoàng Thị Mai",
  slotDate: "2036-09-21",
  startTime: "08:30:00",
  endTime: "09:00:00",
  status: "BOOKED",
  isBookedForOthers: true,
  relationshipLabel: "Mẹ",
  patientFullName: "Nguyễn Thị Hồng",
});

describe("AppointmentHistoryView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupDefaultHooks({ data: [] });
  });

  // ---------------------------------------------------------------------------
  // 1. Loading skeleton
  // ---------------------------------------------------------------------------
  it("hiển thị skeleton khi đang tải", async () => {
    setupDefaultHooks({ isLoading: true, data: [] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card-skeleton").length).toBeGreaterThan(0);
    });
  });

  // ---------------------------------------------------------------------------
  // 2. Hiển thị đúng số lượng SELF tab
  // ---------------------------------------------------------------------------
  it("tab 'Lịch của tôi' hiển thị đúng số lượng (3)", async () => {
    setupDefaultHooks({
      data: [SELF_APPT_1, SELF_APPT_2, SELF_APPT_3, RELATIVE_APPT_1, RELATIVE_APPT_2],
    });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole("tab", { name: /Lịch của tôi \(3\)/ })).toBeInTheDocument();
    });
    expect(screen.getByRole("tab", { name: /Lịch người thân \(2\)/ })).toBeInTheDocument();

    const cards = screen.getAllByTestId("appointment-history-card");
    expect(cards.length).toBe(3);
  });

  // ---------------------------------------------------------------------------
  // 3. Chuyển tab RELATIVE hiển thị đúng số lượng
  // ---------------------------------------------------------------------------
  it("chuyển sang tab 'Lịch người thân' hiển thị 2 lịch", async () => {
    setupDefaultHooks({
      data: [SELF_APPT_1, SELF_APPT_2, SELF_APPT_3, RELATIVE_APPT_1, RELATIVE_APPT_2],
    });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole("tab", { name: /Lịch của tôi \(3\)/ })).toBeInTheDocument();
    });

    // Initial: SELF tab active, 3 cards rendered (RELATIVE unmounted)
    expect(screen.getAllByTestId("appointment-history-card").length).toBe(3);

    await userEvent.click(screen.getByRole("tab", { name: /Lịch người thân \(2\)/ }));

    // After switch: RELATIVE tab active, 2 cards rendered (SELF unmounted)
    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBe(2);
    });
  });

  // ---------------------------------------------------------------------------
  // 4. Empty state — SELF tab không có dữ liệu
  // ---------------------------------------------------------------------------
  it("hiển thị empty state khi tab SELF không có lịch hẹn", async () => {
    setupDefaultHooks({ data: [RELATIVE_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByTestId("self-empty-state")).toBeInTheDocument();
    });

    const ctaButton = screen.getByRole("button", { name: "Đặt lịch ngay" });
    expect(ctaButton).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 5. Empty state — RELATIVE tab không có dữ liệu
  // ---------------------------------------------------------------------------
  it("hiển thị empty state khi tab RELATIVE không có lịch hẹn", async () => {
    setupDefaultHooks({ data: [SELF_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole("tab", { name: /Lịch của tôi \(1\)/ })).toBeInTheDocument();
    });

    await userEvent.click(screen.getByRole("tab", { name: /Lịch người thân \(0\)/ }));

    // After clicking RELATIVE, empty state appears (SELF unmounted)
    await waitFor(() => {
      expect(screen.getByTestId("relative-empty-state")).toBeInTheDocument();
    });
    // SELF card no longer in DOM
    expect(screen.queryByTestId("appointment-history-card")).not.toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 6. Error state với nút Thử lại
  // ---------------------------------------------------------------------------
  it("hiển thị error state khi API lỗi", async () => {
    const refetchMock = vi.fn();
    _mockUseAppointmentHistory.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error("Lỗi server"),
      refetch: refetchMock,
    });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText(/đã xảy ra lỗi/i)).toBeInTheDocument();
    });

    const retryButton = screen.getByRole("button", { name: /Thử lại/i });
    expect(retryButton).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 7. Click card → mở detail dialog
  // ---------------------------------------------------------------------------
  it("click card → mở detail dialog với đúng appointment", async () => {
    setupDefaultHooks({ data: [SELF_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByTestId("appointment-history-card")[0]);

    await waitFor(() => {
      expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    });
  });

  // ---------------------------------------------------------------------------
  // 8. Close detail dialog → dialog đóng
  // ---------------------------------------------------------------------------
  it("click Đóng trong detail dialog → dialog đóng", async () => {
    setupDefaultHooks({ data: [SELF_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByTestId("appointment-history-card")[0]);

    await waitFor(() => {
      expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Đóng" }));

    await waitFor(() => {
      expect(screen.queryByText("Chi tiết lịch khám")).not.toBeInTheDocument();
    });
  });

  // ---------------------------------------------------------------------------
  // 9. Click Hủy lịch → cancel dialog mở
  // ---------------------------------------------------------------------------
  it("click 'Hủy lịch' trong detail → cancel dialog mở", async () => {
    setupDefaultHooks({ data: [SELF_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByTestId("appointment-history-card")[0]);

    await waitFor(() => {
      expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Hủy lịch" }));

    await waitFor(() => {
      expect(screen.getByText("Hủy lịch khám")).toBeInTheDocument();
    });
  });

  // ---------------------------------------------------------------------------
  // 10. Pagination — hiển thị tối đa 10 cards mỗi trang
  // ---------------------------------------------------------------------------
  it("chỉ hiển thị tối đa 10 card đầu tiên khi SELF list > 10", async () => {
    const manySelf = Array.from({ length: 15 }, (_, i) =>
      makeAppt({ appointmentId: `self-pg-${i}`, slotDate: "2036-09-20" }),
    );
    setupDefaultHooks({ data: manySelf });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBe(10);
    });
  });

  // ---------------------------------------------------------------------------
  // 11. Pagination — hiển thị controls (Trang trước/sau + label)
  // ---------------------------------------------------------------------------
  it("hiển thị controls phân trang khi có > 10 lịch", async () => {
    const manySelf = Array.from({ length: 12 }, (_, i) =>
      makeAppt({ appointmentId: `self-pg-${i}`, slotDate: "2036-09-20" }),
    );
    setupDefaultHooks({ data: manySelf });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /trang sau/i })).toBeInTheDocument();
    });
    expect(screen.getByText(/trang 1/i)).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 12. Pagination — click 'Trang sau' → sang trang 2
  // ---------------------------------------------------------------------------
  it("click 'Trang sau' → hiển thị 2 card còn lại và label 'Trang 2/2'", async () => {
    const manySelf = Array.from({ length: 12 }, (_, i) =>
      makeAppt({ appointmentId: `self-pg-${i}`, slotDate: "2036-09-20" }),
    );
    setupDefaultHooks({ data: manySelf });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /trang sau/i })).toBeInTheDocument();
    });

    await userEvent.click(screen.getByRole("button", { name: /trang sau/i }));

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBe(2);
    });
    await waitFor(() => {
      expect(screen.getByText(/trang 2 \/ 2/i)).toBeInTheDocument();
    });
  });

  // ---------------------------------------------------------------------------
  // 14. Reschedule confirm — click "Đặt lại lịch" → confirm dialog mở, detail dialog giữ nguyên
  // ---------------------------------------------------------------------------
  it("click 'Đặt lại lịch' → confirm dialog mở và detail dialog vẫn hiển thị", async () => {
    setupDefaultHooks({ data: [SELF_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByTestId("appointment-history-card")[0]);

    await waitFor(() => {
      expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Đặt lại lịch" }));

    await waitFor(() => {
      expect(screen.getByText("Đặt lịch mới")).toBeInTheDocument();
      expect(
        screen.getByText(/bạn có muốn đặt lịch mới không\?/i)
      ).toBeInTheDocument();
    });

    // Detail dialog vẫn hiển thị
    expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 15. Reschedule confirm — click "Hủy bỏ" → confirm đóng, detail dialog giữ nguyên, router.push chưa gọi
  // ---------------------------------------------------------------------------
  it("click 'Hủy bỏ' trong confirm → confirm đóng, detail dialog giữ nguyên, router.push chưa gọi", async () => {
    setupDefaultHooks({ data: [SELF_APPT_1] });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByTestId("appointment-history-card")[0]);

    await waitFor(() => {
      expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Đặt lại lịch" }));

    await waitFor(() => {
      expect(screen.getByText("Đặt lịch mới")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Hủy bỏ" }));

    await waitFor(() => {
      expect(screen.queryByText("Đặt lịch mới")).not.toBeInTheDocument();
    });

    // Detail dialog vẫn hiển thị
    expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    // router.push chưa gọi
    expect(routerPushMock).not.toHaveBeenCalled();
  });

  // ---------------------------------------------------------------------------
  // 16. Reschedule confirm — click "Xác nhận" → cancel mutation + router.push
  // ---------------------------------------------------------------------------
  it("click 'Xác nhận' trong confirm → cancel mutation gọi với reason 'Đặt lại lịch' và router.push('/dat-lich')", async () => {
    const cancelMutateMock = vi.fn().mockImplementation(
      (_vars: unknown, opts?: { onSuccess?: () => void }) => {
        opts?.onSuccess?.();
      }
    );
    // setupDefaultHooks must be called FIRST (resets all mocks),
    // then override the specific mock we need.
    setupDefaultHooks({ data: [SELF_APPT_1] });
    _mockUseCancelMyAppointment.mockReturnValue({
      mutate: cancelMutateMock,
      isPending: false,
    });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getAllByTestId("appointment-history-card").length).toBeGreaterThan(0);
    });

    fireEvent.click(screen.getAllByTestId("appointment-history-card")[0]);

    await waitFor(() => {
      expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Đặt lại lịch" }));

    await waitFor(() => {
      expect(screen.getByText("Đặt lịch mới")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Xác nhận" }));

    // Cancel mutation được gọi với reason "Đặt lịch"
    await waitFor(() => {
      expect(cancelMutateMock).toHaveBeenCalledTimes(1);
    });
    const [vars] = cancelMutateMock.mock.calls[0] as [unknown];
    expect(vars).toMatchObject({
      appointmentId: "self-1",
      reason: "Đặt lại lịch",
    });
    // router.push được gọi
    expect(routerPushMock).toHaveBeenCalledWith("/dat-lich");
  });

  // ---------------------------------------------------------------------------
  // 13. Pagination — disabled state đúng (Trang 1 disable nút Trang trước)
  // ---------------------------------------------------------------------------
  it("nút 'Trang trước' disabled ở trang đầu, 'Trang sau' disabled ở trang cuối", async () => {
    const manySelf = Array.from({ length: 12 }, (_, i) =>
      makeAppt({ appointmentId: `self-pg-${i}`, slotDate: "2036-09-20" }),
    );
    setupDefaultHooks({ data: manySelf });

    render(<AppointmentHistoryView />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /trang sau/i })).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: /trang trước/i })).toBeDisabled();

    await userEvent.click(screen.getByRole("button", { name: /trang sau/i }));

    await waitFor(() => {
      expect(screen.getByText(/trang 2 \/ 2/i)).toBeInTheDocument();
    });
    expect(screen.getByRole("button", { name: /trang sau/i })).toBeDisabled();
  });
});
