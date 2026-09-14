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
});
