import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@testing-library/jest-dom/vitest";

import { CancelAppointmentDialog } from "@/features/appointment-scheduling/components/cancel-appointment-dialog";
import type {
  useCancellationStatusToday,
  useCancelMyAppointment,
} from "@/features/appointment-scheduling/hooks/use-appointment-history";

// =============================================================================
// Stable mock functions — hoisted so they are defined before vi.mock's factory.
// vi.mock replaces the module exports with the return value from this factory.
// The factory MUST return the live mock references so the component gets them.
// =============================================================================

const _mockUseCancellationStatusToday = vi.fn();
const _mockUseCancelMyAppointment = vi.fn();

vi.mock(
  "@/features/appointment-scheduling/hooks/use-appointment-history",
  () => ({
    // Return the live mock references so the component receives them, not undefined
    useCancellationStatusToday: (args: Parameters<typeof useCancellationStatusToday>[0]) =>
      _mockUseCancellationStatusToday(args),
    useCancelMyAppointment: () => _mockUseCancelMyAppointment(),
  })
);

// =============================================================================
// Toast mock — react-hot-toast default export { error, success }
// =============================================================================

const toastErrorMock = vi.hoisted(() => vi.fn());
const toastSuccessMock = vi.hoisted(() => vi.fn());

vi.mock("react-hot-toast", () => ({
  default: {
    error: toastErrorMock,
    success: toastSuccessMock,
  },
}));

// =============================================================================
// Helpers
// =============================================================================

function renderDialog(
  appointmentId: string | null,
  onClose = vi.fn()
) {
  return render(
    <CancelAppointmentDialog appointmentId={appointmentId} onClose={onClose} />
  );
}

function setupDefaultHooks() {
  _mockUseCancellationStatusToday.mockReturnValue({
    data: { isNextCancellationFinal: false },
    isLoading: false,
  });
  _mockUseCancelMyAppointment.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
  });
}

// =============================================================================
// Tests
// =============================================================================

describe("CancelAppointmentDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupDefaultHooks();
  });

  // ---------------------------------------------------------------------------
  // 1. Dialog không render khi appointmentId === null
  // ---------------------------------------------------------------------------
  it("không render gì khi appointmentId là null", () => {
    renderDialog(null);
    // Dialog title and confirm button must not appear
    expect(screen.queryByText("Hủy lịch khám")).not.toBeInTheDocument();
    expect(screen.queryByTestId("confirm-cancel-button")).not.toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 2. Dialog render khi appointmentId !== null
  // ---------------------------------------------------------------------------
  it("render dialog khi appointmentId có giá trị", () => {
    renderDialog("appt-123");
    expect(screen.getByText("Hủy lịch khám")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Đóng" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Xác nhận hủy/i })
    ).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 3. Nút "Xác nhận hủy" disabled khi reason rỗng
  // ---------------------------------------------------------------------------
  it("nút 'Xác nhận hủy' disabled khi chưa chọn preset và textarea trống", () => {
    renderDialog("appt-123");
    expect(screen.getByTestId("confirm-cancel-button")).toBeDisabled();
  });

  // ---------------------------------------------------------------------------
  // 4. Nút "Xác nhận hủy" enabled khi đã chọn preset
  // ---------------------------------------------------------------------------
  it("nút 'Xác nhận hủy' enabled sau khi chọn preset 'Bận đột xuất'", () => {
    renderDialog("appt-123");
    fireEvent.click(screen.getByText("Bận đột xuất"));
    expect(screen.getByTestId("confirm-cancel-button")).not.toBeDisabled();
  });

  // ---------------------------------------------------------------------------
  // 5. Chọn preset 'Khác (ghi rõ)' → hiện textarea
  // ---------------------------------------------------------------------------
  it("chọn preset 'Khác (ghi rõ)' → hiện textarea và bật disabled khi trống", () => {
    renderDialog("appt-123");
    fireEvent.click(screen.getByText("Khác (ghi rõ)"));

    const textarea = screen.getByTestId("custom-reason-textarea");
    expect(textarea).toBeInTheDocument();
    expect(screen.getByTestId("confirm-cancel-button")).toBeDisabled();

    fireEvent.change(textarea, { target: { value: "Tôi bận đi công tác" } });
    expect(screen.getByTestId("confirm-cancel-button")).not.toBeDisabled();
  });

  // ---------------------------------------------------------------------------
  // 6. isNextCancellationFinal === true → warning dialog hiện ngay
  // ---------------------------------------------------------------------------
  it("hiện warning dialog khi isNextCancellationFinal là true", async () => {
    _mockUseCancellationStatusToday.mockReturnValue({
      data: { isNextCancellationFinal: true },
      isLoading: false,
    });

    renderDialog("appt-123");

    await waitFor(() => {
      expect(screen.getByText("Cảnh báo")).toBeInTheDocument();
    });
    expect(
      screen.getByText(/Bạn đã hủy 2 lần trong ngày/i)
    ).toBeInTheDocument();
    expect(screen.getByTestId("back-from-warning-button")).toBeInTheDocument();
    expect(screen.getByTestId("final-confirm-button")).toBeInTheDocument();
  });

  it("quay lại từ warning dialog → hiện lại form reason", async () => {
    _mockUseCancellationStatusToday.mockReturnValue({
      data: { isNextCancellationFinal: true },
      isLoading: false,
    });

    renderDialog("appt-123");

    await waitFor(() => {
      expect(screen.getByText("Cảnh báo")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("back-from-warning-button"));

    expect(screen.getByText("Hủy lịch khám")).toBeInTheDocument();
    expect(screen.getByText("Bận đột xuất")).toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // 7. Mutation được gọi đúng khi submit
  // ---------------------------------------------------------------------------
  it("gọi mutation với đúng appointmentId và reason khi submit preset 'Đổi lịch trình'", () => {
    const mutateMock = vi.fn();
    _mockUseCancelMyAppointment.mockReturnValue({
      mutate: mutateMock,
      isPending: false,
    });

    renderDialog("appt-abc");

    fireEvent.click(screen.getByText("Đổi lịch trình"));
    fireEvent.click(screen.getByTestId("confirm-cancel-button"));

    expect(mutateMock).toHaveBeenCalledTimes(1);
    // TanStack Query passes callbacks as 2nd arg — assert on 1st arg
    const [vars] = mutateMock.mock.calls[0] as [unknown];
    expect(vars).toEqual({
      appointmentId: "appt-abc",
      reason: "Đổi lịch trình",
    });
  });

  it("gọi mutation với textarea content khi chọn 'Khác'", () => {
    const mutateMock = vi.fn();
    _mockUseCancelMyAppointment.mockReturnValue({
      mutate: mutateMock,
      isPending: false,
    });

    renderDialog("appt-xyz");

    fireEvent.click(screen.getByText("Khác (ghi rõ)"));
    fireEvent.change(screen.getByTestId("custom-reason-textarea"), {
      target: { value: "Có việc gia đình đột xuất" },
    });
    fireEvent.click(screen.getByTestId("confirm-cancel-button"));

    const [vars] = mutateMock.mock.calls[0] as [unknown];
    expect(vars).toEqual({
      appointmentId: "appt-xyz",
      reason: "Có việc gia đình đột xuất",
    });
  });

  // ---------------------------------------------------------------------------
  // 8. Success → toast.success + onClose(); Error → toast.error + dialog kept open
  // ---------------------------------------------------------------------------
  it("gọi toast.success và onClose khi cancel thành công", async () => {
    const onCloseMock = vi.fn();
    const mutateMock = vi.fn().mockImplementation((_vars: unknown, opts?: { onSuccess?: () => void }) => {
      opts?.onSuccess?.();
    });
    _mockUseCancelMyAppointment.mockReturnValue({
      mutate: mutateMock,
      isPending: false,
    });

    renderDialog("appt-success", onCloseMock);

    fireEvent.click(screen.getByText("Bận đột xuất"));
    fireEvent.click(screen.getByTestId("confirm-cancel-button"));

    await waitFor(() => {
      expect(toastSuccessMock).toHaveBeenCalledWith("Đã hủy lịch khám thành công");
    });
    expect(onCloseMock).toHaveBeenCalledTimes(1);
  });

  it("gọi toast.error khi cancel thất bại và dialog giữ mở", async () => {
    const mutateMock = vi.fn().mockImplementation(
      (_vars: unknown, opts?: { onError?: (err: Error) => void }) => {
        opts?.onError?.(new Error("Không thể hủy lịch"));
      }
    );
    _mockUseCancelMyAppointment.mockReturnValue({
      mutate: mutateMock,
      isPending: false,
    });

    renderDialog("appt-fail");

    fireEvent.click(screen.getByText("Bận đột xuất"));
    fireEvent.click(screen.getByTestId("confirm-cancel-button"));

    await waitFor(() => {
      expect(toastErrorMock).toHaveBeenCalledWith("Không thể hủy lịch");
    });

    // Dialog vẫn mở
    expect(screen.getByText("Hủy lịch khám")).toBeInTheDocument();
  });
});
