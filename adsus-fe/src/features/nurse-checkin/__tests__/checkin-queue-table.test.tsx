import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CheckinQueueTable } from "../components/checkin-queue-table";
import type { CheckinQueueItem } from "../types/checkin.types";

describe("CheckinQueueTable", () => {
  const mockAppointments: CheckinQueueItem[] = [
    {
      appointmentId: "app-1",
      slotTime: "2026-09-10T08:30:00Z",
      patientFullName: "Nguyễn Văn An",
      patientPhone: "0901234567",
      patientProfileId: "prof-1",
      caseId: "case-1",
      reason: "Đau đầu",
      doctorName: "BS. Trần Văn Minh",
      status: "Booked",
    },
    {
      appointmentId: "app-2",
      slotTime: "2026-09-10T09:00:00Z",
      patientFullName: "Lê Thị Bích",
      patientPhone: "0909876543",
      patientProfileId: "prof-2",
      caseId: "case-2",
      reason: "Khám định kỳ",
      doctorName: "BS. Nguyễn Văn Nam",
      status: "Approved",
    },
    {
      appointmentId: "app-3",
      slotTime: "2026-09-10T09:30:00Z",
      patientFullName: "Phạm Quốc Dũng",
      patientPhone: null,
      patientProfileId: "prof-3",
      caseId: "case-3",
      reason: null,
      doctorName: "BS. Lê Hoa",
      status: "Completed",
    },
    {
      appointmentId: "app-4",
      slotTime: "2026-09-10T10:00:00Z",
      patientFullName: "Võ Thị Mai",
      patientPhone: "0911223344",
      patientProfileId: "prof-4",
      caseId: "case-4",
      reason: "Siêu âm thai",
      doctorName: "BS. Lê Hoa",
      status: "Cancelled",
    },
    {
      appointmentId: "app-5",
      slotTime: "2026-09-10T10:30:00Z",
      patientFullName: "Hoàng Văn Tuấn",
      patientPhone: "0988776655",
      patientProfileId: "prof-5",
      caseId: "case-5",
      reason: "Tái khám huyết áp",
      doctorName: "BS. Hoàng Long",
      status: "NoShow",
    },
  ];

  it("should render loading spinner when isLoading is true", () => {
    render(
      <CheckinQueueTable
        queue={[]}
        isLoading={true}
        onCheckin={vi.fn()}
        checkingInId={null}
      />
    );

    expect(screen.queryByText("STT")).not.toBeInTheDocument();
  });

  it("should render empty state message when queue has 0 items", () => {
    render(
      <CheckinQueueTable
        queue={[]}
        isLoading={false}
        onCheckin={vi.fn()}
        checkingInId={null}
      />
    );

    expect(screen.getByText("Không tìm thấy lịch hẹn phù hợp.")).toBeInTheDocument();
  });

  it("should render table headers and patient details properly", () => {
    render(
      <CheckinQueueTable
        queue={mockAppointments}
        isLoading={false}
        onCheckin={vi.fn()}
        checkingInId={null}
        page={1}
        pageSize={15}
      />
    );

    expect(screen.getByText("Nguyễn Văn An")).toBeInTheDocument();
    expect(screen.getByText("0901234567")).toBeInTheDocument();
    expect(screen.getByText("BS. Trần Văn Minh")).toBeInTheDocument();
    expect(screen.getByText("Đau đầu")).toBeInTheDocument();
  });

  it("should calculate continuous STT based on page number and pageSize", () => {
    render(
      <CheckinQueueTable
        queue={mockAppointments.slice(0, 2)}
        isLoading={false}
        onCheckin={vi.fn()}
        checkingInId={null}
        page={2}
        pageSize={15}
      />
    );

    // Page 2: items should start from 16 and 17
    expect(screen.getByText("16")).toBeInTheDocument();
    expect(screen.getByText("17")).toBeInTheDocument();
  });

  it("should display appropriate status badges and buttons", () => {
    render(
      <CheckinQueueTable
        queue={mockAppointments}
        isLoading={false}
        onCheckin={vi.fn()}
        checkingInId={null}
      />
    );

    // Booked -> Check-in button
    expect(screen.getByRole("button", { name: /Check-in/i })).toBeInTheDocument();

    // Approved & Completed -> Đã check-in badge
    expect(screen.getAllByText("Đã check-in").length).toBe(2);

    // Cancelled -> Đã huỷ badge
    expect(screen.getByText("Đã huỷ")).toBeInTheDocument();

    // NoShow -> Vắng mặt badge
    expect(screen.getByText("Vắng mặt")).toBeInTheDocument();
  });

  it("should display NoShow (amber) and Cancelled (rose) badges independently with correct styling", () => {
    render(
      <CheckinQueueTable
        queue={[
          {
            appointmentId: "app-cancel",
            slotTime: "2026-09-10T10:00:00Z",
            patientFullName: "Võ Thị Mai",
            patientPhone: "0911223344",
            patientProfileId: "prof-4",
            caseId: "case-4",
            reason: null,
            doctorName: "BS. Lê Hoa",
            status: "Cancelled",
          },
          {
            appointmentId: "app-noshow",
            slotTime: "2026-09-10T10:30:00Z",
            patientFullName: "Hoàng Văn Tuấn",
            patientPhone: "0988776655",
            patientProfileId: "prof-5",
            caseId: "case-5",
            reason: null,
            doctorName: "BS. Trần Văn Minh",
            status: "NoShow",
          },
        ]}
        isLoading={false}
        onCheckin={vi.fn()}
        checkingInId={null}
      />
    );

    const cancelledBadge = screen.getByText("Đã huỷ");
    expect(cancelledBadge).toBeInTheDocument();
    expect(cancelledBadge.className).toContain("text-rose-700");
    expect(cancelledBadge.className).toContain("border-rose-600/30");

    const noShowBadge = screen.getByText("Vắng mặt");
    expect(noShowBadge).toBeInTheDocument();
    expect(noShowBadge.className).toContain("text-amber-700");
    expect(noShowBadge.className).toContain("border-amber-600/30");
  });

  it("should trigger onCheckin callback when Check-in button is clicked", () => {
    const handleCheckin = vi.fn();
    render(
      <CheckinQueueTable
        queue={[mockAppointments[0]]}
        isLoading={false}
        onCheckin={handleCheckin}
        checkingInId={null}
      />
    );

    const button = screen.getByRole("button", { name: /Check-in/i });
    fireEvent.click(button);

    expect(handleCheckin).toHaveBeenCalledWith(mockAppointments[0]);
  });

  it("should disable Check-in button when checkingInId is present", () => {
    render(
      <CheckinQueueTable
        queue={[mockAppointments[0]]}
        isLoading={false}
        onCheckin={vi.fn()}
        checkingInId="app-1"
      />
    );

    const button = screen.getByRole("button", { name: /Check-in/i });
    expect(button).toBeDisabled();
  });
});
