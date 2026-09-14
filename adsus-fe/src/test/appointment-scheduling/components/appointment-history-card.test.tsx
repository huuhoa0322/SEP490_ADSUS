import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import "@testing-library/jest-dom/vitest";

import {
  AppointmentHistoryCard,
  formatSlotDate,
  isExpired,
  getAccentColor,
} from "@/features/appointment-scheduling/components/appointment-history-card";
import type { AppointmentSummaryResponse } from "@/features/appointment-scheduling/types/booking.types";

// Test helper factories
function makeAppointment(overrides: Partial<AppointmentSummaryResponse> = {}): AppointmentSummaryResponse {
  return {
    appointmentId: "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    scheduleSlotId: "slot-001",
    doctorId: "doc-001",
    slotDate: "2026-09-20",
    startTime: "08:00:00",
    endTime: "08:30:00",
    doctorName: "Nguyễn Văn A",
    status: "BOOKED",
    createdAt: "2026-09-01T10:00:00Z",
    reason: "Khám định kỳ",
    cancellationReason: null,
    caseId: null,
    isBookedForOthers: false,
    relationshipLabel: null,
    bookedByUserName: null,
    ...overrides,
  };
}

// ============================================================
// Test: formatSlotDate
// ============================================================
describe("formatSlotDate", () => {
  it("chuyển YYYY-MM-DD thành dd/MM/yyyy", () => {
    expect(formatSlotDate("2026-09-20")).toBe("20/09/2026");
  });

  it("trả về nguyên input nếu parse thất bại", () => {
    expect(formatSlotDate("invalid")).toBe("invalid");
  });
});

// ============================================================
// Test: isExpired
// ============================================================
describe("isExpired", () => {
  it("trả về true khi slot đã qua", () => {
    // Slot ngày hôm qua, giờ đã qua
    expect(isExpired("2020-01-01", "00:00:00")).toBe(true);
  });

  it("trả về false khi slot trong tương lai", () => {
    // Slot 10 năm sau
    expect(isExpired("2036-01-01", "23:59:59")).toBe(false);
  });
});

// ============================================================
// Test: getAccentColor
// ============================================================
describe("getAccentColor", () => {
  it("trả về teal cho BOOKED chưa expired", () => {
    const appt = makeAppointment({ status: "BOOKED", slotDate: "2036-01-01", endTime: "23:59:59" });
    expect(getAccentColor(appt)).toBe("bg-teal-600");
  });

  it("trả về orange cho BOOKED đã expired", () => {
    const appt = makeAppointment({ status: "BOOKED", slotDate: "2020-01-01", endTime: "00:00:00" });
    expect(getAccentColor(appt)).toBe("bg-orange-400");
  });

  it("trả về gray cho CANCELLED", () => {
    const appt = makeAppointment({ status: "CANCELLED" });
    expect(getAccentColor(appt)).toBe("bg-gray-400");
  });

  it("trả về gray cho COMPLETED", () => {
    const appt = makeAppointment({ status: "COMPLETED" });
    expect(getAccentColor(appt)).toBe("bg-gray-400");
  });

  it("trả về gray cho NO_SHOW", () => {
    const appt = makeAppointment({ status: "NO_SHOW" });
    expect(getAccentColor(appt)).toBe("bg-gray-400");
  });

  it("trả về orange cho APPROVED đã expired", () => {
    const appt = makeAppointment({ status: "APPROVED", slotDate: "2020-01-01", endTime: "00:00:00" });
    expect(getAccentColor(appt)).toBe("bg-orange-400");
  });
});

// ============================================================
// Test: AppointmentHistoryCard rendering
// ============================================================
describe("AppointmentHistoryCard", () => {
  // Case 1: Self appointment
  it("hiển thị thông tin cuộc hẹn bình thường (không isBookedForOthers)", () => {
    const onClick = vi.fn();
    const appt = makeAppointment({ isBookedForOthers: false });
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);

    expect(screen.getByTestId("appointment-history-card")).toBeInTheDocument();
    expect(screen.getByTestId("date-label")).toHaveTextContent("Lịch khám: 20/09/2026");
    expect(screen.getByTestId("time-range")).toHaveTextContent("08:00 – 08:30");
    expect(screen.getByTestId("doctor-name")).toHaveTextContent("BS. Nguyễn Văn A");
    expect(screen.getByTestId("status-badge")).toHaveTextContent("Đã đặt");
    expect(screen.getByTestId("accent-bar")).toHaveClass("bg-teal-600");
  });

  // Case 2: Relative appointment
  it("hiển thị badge đặt hộ + tên người khám khi isBookedForOthers", () => {
    const onClick = vi.fn();
    const appt = makeAppointment({
      isBookedForOthers: true,
      relationshipLabel: "Con",
      patientFullName: "Trần Thị B",
    });
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);

    expect(screen.getByText("Đặt hộ: Con")).toBeInTheDocument();
    expect(screen.getByText("Trần Thị B")).toBeInTheDocument();
  });

  // Case 3: Accent bar teal cho BOOKED + chưa expired
  it("accent bar màu teal khi BOOKED và chưa hết hạn", () => {
    const onClick = vi.fn();
    const appt = makeAppointment({ status: "BOOKED", slotDate: "2036-01-01", endTime: "23:59:59" });
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);
    expect(screen.getByTestId("accent-bar")).toHaveClass("bg-teal-600");
  });

  // Case 4: Accent bar orange cho BOOKED + đã expired
  it("accent bar màu orange khi BOOKED và đã hết hạn", () => {
    const onClick = vi.fn();
    const appt = makeAppointment({ status: "BOOKED", slotDate: "2020-01-01", endTime: "00:00:00" });
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);
    expect(screen.getByTestId("accent-bar")).toHaveClass("bg-orange-400");
  });

  // Case 5: Accent bar gray cho CANCELLED
  it("accent bar màu gray khi CANCELLED", () => {
    const onClick = vi.fn();
    const appt = makeAppointment({ status: "CANCELLED" });
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);
    expect(screen.getByTestId("accent-bar")).toHaveClass("bg-gray-400");
  });

  // Case 6: Status badge đúng màu
  it("status badge có màu đúng theo trạng thái", () => {
    const onClick = vi.fn();
    // CANCELLED → red
    const appt = makeAppointment({ status: "CANCELLED" });
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);
    expect(screen.getByTestId("status-badge")).toHaveClass("bg-red-100", "text-red-800");
  });

  // Case 7: onClick được gọi khi click card
  it("gọi onClick với đúng appointment khi click card", async () => {
    const onClick = vi.fn();
    const appt = makeAppointment();
    render(<AppointmentHistoryCard appointment={appt} onClick={onClick} />);

    const card = screen.getByTestId("appointment-history-card");
    card.click();

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(onClick).toHaveBeenCalledWith(appt);
  });
});
