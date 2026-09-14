import { render, screen, fireEvent } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@testing-library/jest-dom/vitest";

import { AppointmentHistoryDetailDialog } from "@/features/appointment-scheduling/components/appointment-history-detail-dialog";
import type { AppointmentSummaryResponse } from "@/features/appointment-scheduling/types/booking.types";

function makeAppointment(
  overrides: Partial<AppointmentSummaryResponse> = {}
): AppointmentSummaryResponse {
  return {
    appointmentId: "appt-001",
    scheduleSlotId: "slot-001",
    doctorId: "doc-001",
    slotDate: "2036-09-20",
    startTime: "08:00:00",
    endTime: "08:30:00",
    doctorName: "BS. Nguyễn Văn Minh",
    status: "BOOKED",
    createdAt: "2026-09-01T10:00:00Z",
    reason: null,
    cancellationReason: null,
    caseId: null,
    isBookedForOthers: false,
    relationshipLabel: null,
    bookedByUserName: null,
    ...overrides,
  };
}

describe("AppointmentHistoryDetailDialog", () => {
  const mockOnClose = vi.fn();
  const mockOnCancel = vi.fn();
  const mockOnReschedule = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // 1. Render self appointment (không isBookedForOthers)
  // =========================================================================
  it("hiển thị đúng thông tin cuộc hẹn của bản thân (không isBookedForOthers)", () => {
    const appt = makeAppointment({ isBookedForOthers: false });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.getByText("Chi tiết lịch khám")).toBeInTheDocument();
    expect(screen.getByText("BS. Nguyễn Văn Minh")).toBeInTheDocument();
    expect(screen.getByText("20/09/2036")).toBeInTheDocument();
    expect(screen.getByText("08:00 – 08:30")).toBeInTheDocument();
    // No "Người khám" or "Quan hệ" rows
    expect(screen.queryByText("Người khám")).not.toBeInTheDocument();
  });

  // =========================================================================
  // 2. Render relative appointment (có isBookedForOthers)
  // =========================================================================
  it("hiển thị 'Người khám' và 'Quan hệ' khi isBookedForOthers", () => {
    const appt = makeAppointment({
      isBookedForOthers: true,
      relationshipLabel: "Con",
      patientFullName: "Trần Thị Lan",
    });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.getByText("Người khám")).toBeInTheDocument();
    expect(screen.getByText("Trần Thị Lan")).toBeInTheDocument();
    expect(screen.getByText("Quan hệ")).toBeInTheDocument();
    expect(screen.getByText("Con")).toBeInTheDocument();
  });

  // =========================================================================
  // 3. Hiện "Lý do khám" khi reason có giá trị
  // =========================================================================
  it("hiển thị 'Lý do khám' khi reason không rỗng", () => {
    const appt = makeAppointment({ reason: "Đau đầu, chóng mặt" });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.getByText("Lý do khám")).toBeInTheDocument();
    expect(screen.getByText("Đau đầu, chóng mặt")).toBeInTheDocument();
  });

  it("KHÔNG hiển thị 'Lý do khám' khi reason là null", () => {
    const appt = makeAppointment({ reason: null });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.queryByText("Lý do khám")).not.toBeInTheDocument();
  });

  // =========================================================================
  // 4. Hiện "Lý do hủy" khi cancelled + cancellationReason
  // =========================================================================
  it("hiển thị 'Lý do hủy' (màu danger) khi CANCELLED và có cancellationReason", () => {
    const appt = makeAppointment({
      status: "CANCELLED",
      cancellationReason: "Bệnh nhân bận việc đột xuất",
    });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.getByText("Lý do hủy")).toBeInTheDocument();
    const reasonEl = screen.getByText("Bệnh nhân bận việc đột xuất");
    expect(reasonEl).toBeInTheDocument();
    expect(reasonEl).toHaveClass("text-destructive");
  });

  it("KHÔNG hiển thị 'Lý do hủy' khi CANCELLED nhưng cancellationReason là null", () => {
    const appt = makeAppointment({
      status: "CANCELLED",
      cancellationReason: null,
    });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.queryByText("Lý do hủy")).not.toBeInTheDocument();
  });

  // =========================================================================
  // 5. Buttons visible khi BOOKED + chưa expired
  // =========================================================================
  it("hiển thị 'Hủy lịch' và 'Đặt lại lịch' khi BOOKED và chưa expired", () => {
    const appt = makeAppointment({ status: "BOOKED", slotDate: "2036-09-20", endTime: "23:59:59" });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.getByRole("button", { name: "Hủy lịch" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Đặt lại lịch" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Đóng" })).toBeInTheDocument();
  });

  it("hiển thị buttons khi APPROVED và chưa expired", () => {
    const appt = makeAppointment({ status: "APPROVED", slotDate: "2036-09-20", endTime: "23:59:59" });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.getByRole("button", { name: "Hủy lịch" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Đặt lại lịch" })).toBeInTheDocument();
  });

  // =========================================================================
  // 6. Buttons ẩn khi CANCELLED
  // =========================================================================
  it("KHÔNG hiển thị 'Hủy lịch' và 'Đặt lại lịch' khi CANCELLED", () => {
    const appt = makeAppointment({
      status: "CANCELLED",
      cancellationReason: "Lý do hủy ở đây",
    });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.queryByRole("button", { name: "Hủy lịch" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Đặt lại lịch" })).not.toBeInTheDocument();
    // Đóng button vẫn phải có
    expect(screen.getByRole("button", { name: "Đóng" })).toBeInTheDocument();
  });

  it("KHÔNG hiển thị buttons khi slot đã expired (BOOKED + past)", () => {
    const appt = makeAppointment({ status: "BOOKED", slotDate: "2020-01-01", endTime: "00:00:00" });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(screen.queryByRole("button", { name: "Hủy lịch" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Đặt lại lịch" })).not.toBeInTheDocument();
  });

  // =========================================================================
  // 7. onCancel được gọi khi click "Hủy lịch"
  // =========================================================================
  it("gọi onCancel với đúng appointmentId khi click 'Hủy lịch'", () => {
    const appt = makeAppointment({
      appointmentId: "appt-cancel-123",
      status: "BOOKED",
      slotDate: "2036-09-20",
      endTime: "23:59:59",
    });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Hủy lịch" }));
    expect(mockOnCancel).toHaveBeenCalledTimes(1);
    expect(mockOnCancel).toHaveBeenCalledWith("appt-cancel-123");
    expect(mockOnClose).not.toHaveBeenCalled(); // parent handles closing
  });

  // =========================================================================
  // 8. onReschedule được gọi khi click "Đặt lại lịch"
  // =========================================================================
  it("gọi onReschedule với đúng appointmentId khi click 'Đặt lại lịch'", () => {
    const appt = makeAppointment({
      appointmentId: "appt-reschedule-456",
      status: "APPROVED",
      slotDate: "2036-09-20",
      endTime: "23:59:59",
    });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Đặt lại lịch" }));
    expect(mockOnReschedule).toHaveBeenCalledTimes(1);
    expect(mockOnReschedule).toHaveBeenCalledWith("appt-reschedule-456");
    expect(mockOnClose).not.toHaveBeenCalled(); // parent handles closing
  });

  // =========================================================================
  // 9. onClose được gọi khi click "Đóng"
  // =========================================================================
  it("gọi onClose khi click 'Đóng'", () => {
    const appt = makeAppointment({ status: "CANCELLED" });
    render(
      <AppointmentHistoryDetailDialog
        appointment={appt}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Đóng" }));
    expect(mockOnClose).toHaveBeenCalledTimes(1);
  });

  // =========================================================================
  // Additional: null appointment → renders nothing
  // =========================================================================
  it("không render gì khi appointment là null", () => {
    const { container } = render(
      <AppointmentHistoryDetailDialog
        appointment={null}
        onClose={mockOnClose}
        onCancel={mockOnCancel}
        onReschedule={mockOnReschedule}
      />
    );

    expect(container).toBeEmptyDOMElement();
  });

  // =========================================================================
  // Additional: status badge labels
  // =========================================================================
  it.each([
    { status: "BOOKED", expected: "Đã đặt" },
    { status: "APPROVED", expected: "Đã duyệt" },
    { status: "CANCELLED", expected: "Đã huỷ" },
    { status: "COMPLETED", expected: "Hoàn thành" },
    { status: "NO_SHOW", expected: "Vắng mặt" },
    { status: "UNKNOWN", expected: "UNKNOWN" },
  ] as const)(
    "hiển thị badge đúng nhãn cho status '$status'",
    ({ status, expected }) => {
      const appt = makeAppointment({ status });
      render(
        <AppointmentHistoryDetailDialog
          appointment={appt}
          onClose={mockOnClose}
          onCancel={mockOnCancel}
          onReschedule={mockOnReschedule}
        />
      );

      expect(screen.getByText(expected)).toBeInTheDocument();
    }
  );
});
