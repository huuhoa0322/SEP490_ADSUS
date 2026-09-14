import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { format } from "date-fns";
import { BookAppointmentModal } from "../components/book-appointment-modal";
import type { DoctorSummary, AvailableSlot } from "../types/checkin.types";
import { useDoctorList, useAvailableSlots } from "../hooks/use-reschedule";
import { useStaffBookAppointment } from "../hooks/use-staff-book-appointment";

vi.mock("../hooks/use-reschedule", () => ({
  useDoctorList: vi.fn(),
  useAvailableSlots: vi.fn(),
}));

vi.mock("../hooks/use-staff-book-appointment", () => ({
  useStaffBookAppointment: vi.fn(),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-relatives", () => ({
  useRelativesForGuardian: vi.fn(() => ({
    data: [],
    isLoading: false,
  })),
}));

// Mock DatePicker to simple input for predictable testing in jsdom
vi.mock("@/components/ui/date-picker", () => ({
  DatePicker: ({
    id,
    value,
    onChange,
    disabled,
  }: {
    id?: string;
    value?: string;
    onChange: (val: string) => void;
    disabled?: boolean;
  }) => (
    <input
      data-testid={id || "date-picker"}
      id={id}
      value={value || ""}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value)}
    />
  ),
}));

describe("BookAppointmentModal", () => {
  const mockOnOpenChange = vi.fn();
  const mockMutateAsync = vi.fn();

  const mockDoctors: DoctorSummary[] = [
    {
      doctorId: "doc-1",
      fullName: "BS. Trần Văn Minh",
      email: "minh@adsus.test",
      specialty: "Nội tổng quát",
      phone: "0901111111",
    },
    {
      doctorId: "doc-2",
      fullName: "BS. Lê Thị Hoa",
      email: "hoa@adsus.test",
      specialty: "Tai Mũi Họng",
      phone: "0902222222",
    },
  ];

  const tomorrowStr = format(new Date(Date.now() + 24 * 60 * 60 * 1000), "yyyy-MM-dd");

  const mockSlots: AvailableSlot[] = [
    {
      slotId: "slot-1",
      doctorId: "doc-1",
      doctorName: "BS. Trần Văn Minh",
      slotDate: tomorrowStr,
      startTime: "09:00:00",
      endTime: "10:00:00",
    },
    {
      slotId: "slot-2",
      doctorId: "doc-1",
      doctorName: "BS. Trần Văn Minh",
      slotDate: tomorrowStr,
      startTime: "10:00:00",
      endTime: "11:00:00",
    },
  ];

  const defaultProps = {
    patientProfileId: "prof-123",
    patientName: "Nguyễn Thị Mai",
    patientPhone: "0912345678",
    open: true,
    onOpenChange: mockOnOpenChange,
  };

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useDoctorList).mockReturnValue({
      data: mockDoctors,
      isLoading: false,
    } as unknown as ReturnType<typeof useDoctorList>);

    vi.mocked(useAvailableSlots).mockReturnValue({
      data: mockSlots,
      isLoading: false,
    } as unknown as ReturnType<typeof useAvailableSlots>);

    vi.mocked(useStaffBookAppointment).mockReturnValue({
      mutateAsync: mockMutateAsync,
      isPending: false,
    } as unknown as ReturnType<typeof useStaffBookAppointment>);
  });

  it("renders patient name and phone number in header when open", () => {
    render(<BookAppointmentModal {...defaultProps} />);

    expect(screen.getByText("Đặt lịch hẹn cho bệnh nhân")).toBeInTheDocument();
    expect(screen.getByText("Nguyễn Thị Mai")).toBeInTheDocument();
    expect(screen.getByText("0912345678")).toBeInTheDocument();
  });

  it("does not render dialog content when open is false", () => {
    render(<BookAppointmentModal {...defaultProps} open={false} />);

    expect(screen.queryByText("Đặt lịch hẹn cho bệnh nhân")).not.toBeInTheDocument();
  });

  it("populates doctor select; date picker and slot select are initially disabled", () => {
    render(<BookAppointmentModal {...defaultProps} />);

    const doctorSelect = document.querySelector("#book-doctor") as HTMLSelectElement;
    expect(doctorSelect).toBeInTheDocument();
    expect(screen.getByRole("option", { name: /BS. Trần Văn Minh/i })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: /BS. Lê Thị Hoa/i })).toBeInTheDocument();

    const datePicker = screen.getByTestId("book-date");
    expect(datePicker).toBeDisabled();

    const slotSelect = document.querySelector("#book-slot") as HTMLSelectElement;
    expect(slotSelect).toBeDisabled();
    expect(screen.getByText("Vui lòng chọn bác sĩ trước")).toBeInTheDocument();
  });

  it("enables date picker when doctor is selected", () => {
    render(<BookAppointmentModal {...defaultProps} />);

    const doctorSelect = document.querySelector("#book-doctor") as HTMLSelectElement;
    fireEvent.change(doctorSelect, { target: { value: "doc-1" } });

    const datePicker = screen.getByTestId("book-date");
    expect(datePicker).toBeEnabled();

    // Slot is still disabled until date is picked
    const slotSelect = document.querySelector("#book-slot") as HTMLSelectElement;
    expect(slotSelect).toBeDisabled();
    expect(screen.getByText("Vui lòng chọn ngày khám")).toBeInTheDocument();
  });

  it("enables slot select and renders open slots when doctor and date are chosen", () => {
    render(<BookAppointmentModal {...defaultProps} />);

    // 1. Select doctor
    const doctorSelect = document.querySelector("#book-doctor") as HTMLSelectElement;
    fireEvent.change(doctorSelect, { target: { value: "doc-1" } });

    // 2. Select date
    const datePicker = screen.getByTestId("book-date");
    fireEvent.change(datePicker, { target: { value: tomorrowStr } });

    // 3. Slot select should be enabled with slot options
    const slotSelect = document.querySelector("#book-slot") as HTMLSelectElement;
    expect(slotSelect).toBeEnabled();
    expect(screen.getByText(/2 slot trống/i)).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "09:00 – 10:00" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "10:00 – 11:00" })).toBeInTheDocument();
  });

  it("shows empty slots message when doctor has no available slots on selected date", () => {
    vi.mocked(useAvailableSlots).mockReturnValue({
      data: [],
      isLoading: false,
    } as unknown as ReturnType<typeof useAvailableSlots>);

    render(<BookAppointmentModal {...defaultProps} />);

    const doctorSelect = document.querySelector("#book-doctor") as HTMLSelectElement;
    fireEvent.change(doctorSelect, { target: { value: "doc-1" } });

    const datePicker = screen.getByTestId("book-date");
    fireEvent.change(datePicker, { target: { value: tomorrowStr } });

    expect(screen.getByText("Không có khung giờ trống trong ngày này")).toBeInTheDocument();
  });

  it("submit button is disabled until doctor, date, and slot are all selected", () => {
    render(<BookAppointmentModal {...defaultProps} />);

    const submitBtn = screen.getByRole("button", { name: /xác nhận đặt lịch/i });
    expect(submitBtn).toBeDisabled();

    // Select doctor only
    const doctorSelect = document.querySelector("#book-doctor") as HTMLSelectElement;
    fireEvent.change(doctorSelect, { target: { value: "doc-1" } });
    expect(submitBtn).toBeDisabled();

    // Select date only
    const datePicker = screen.getByTestId("book-date");
    fireEvent.change(datePicker, { target: { value: tomorrowStr } });
    expect(submitBtn).toBeDisabled();

    // Select slot
    const slotSelect = document.querySelector("#book-slot") as HTMLSelectElement;
    fireEvent.change(slotSelect, { target: { value: "slot-1" } });
    expect(submitBtn).toBeEnabled();
  });

  it("submits booking with patientProfileId, scheduleSlotId, and optional reason, then closes modal", async () => {
    mockMutateAsync.mockResolvedValueOnce({ code: 201, data: {} });

    render(<BookAppointmentModal {...defaultProps} />);

    // Select doctor
    fireEvent.change(document.querySelector("#book-doctor")!, { target: { value: "doc-1" } });
    // Select date
    fireEvent.change(screen.getByTestId("book-date"), { target: { value: tomorrowStr } });
    // Select slot
    fireEvent.change(document.querySelector("#book-slot")!, { target: { value: "slot-1" } });
    // Input reason
    const reasonInput = document.querySelector("#book-reason") as HTMLInputElement;
    fireEvent.change(reasonInput, { target: { value: "Khám định kỳ" } });

    // Click submit
    const submitBtn = screen.getByRole("button", { name: /xác nhận đặt lịch/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        patientProfileId: "prof-123",
        scheduleSlotId: "slot-1",
        reason: "Khám định kỳ",
      });
    });

    expect(mockOnOpenChange).toHaveBeenCalledWith(false);
  });

  it("submits undefined reason if reason input is whitespace or empty", async () => {
    mockMutateAsync.mockResolvedValueOnce({ code: 201, data: {} });

    render(<BookAppointmentModal {...defaultProps} />);

    fireEvent.change(document.querySelector("#book-doctor")!, { target: { value: "doc-1" } });
    fireEvent.change(screen.getByTestId("book-date"), { target: { value: tomorrowStr } });
    fireEvent.change(document.querySelector("#book-slot")!, { target: { value: "slot-2" } });

    const submitBtn = screen.getByRole("button", { name: /xác nhận đặt lịch/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        patientProfileId: "prof-123",
        scheduleSlotId: "slot-2",
        reason: undefined,
      });
    });
  });

  it("disables inputs and shows spinner when booking mutation is pending", () => {
    vi.mocked(useStaffBookAppointment).mockReturnValue({
      mutateAsync: mockMutateAsync,
      isPending: true,
    } as unknown as ReturnType<typeof useStaffBookAppointment>);

    render(<BookAppointmentModal {...defaultProps} />);

    expect(screen.getByText("Đang đặt lịch...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /hủy/i })).toBeDisabled();
    expect(document.querySelector("#book-doctor")).toBeDisabled();
  });

  it("closes modal when Hủy button is clicked", () => {
    render(<BookAppointmentModal {...defaultProps} />);

    const cancelBtn = screen.getByRole("button", { name: /hủy/i });
    fireEvent.click(cancelBtn);

    expect(mockOnOpenChange).toHaveBeenCalledWith(false);
  });

  it("cho phép Staff chọn đặt lịch cho người thân của bệnh nhân", async () => {
    const { useRelativesForGuardian } = await import("@/features/appointment-scheduling/hooks/use-relatives");
    vi.mocked(useRelativesForGuardian).mockReturnValue({
      data: [
        {
          relationshipId: "rel-10",
          patientProfileId: "prof-child",
          patientName: "Bé Con",
          relationshipName: "Con",
          patientPhone: "0912345678",
          dateOfBirth: "2020-01-01",
          gender: "FEMALE",
          isRegisteredAccount: false,
          createdAt: "2026-01-01T00:00:00Z",
        },
      ],
      isLoading: false,
    } as unknown as ReturnType<typeof useRelativesForGuardian>);

    render(<BookAppointmentModal {...defaultProps} patientUserId="user-mother" />);

    expect(screen.getByText(/Người thân \(1\)/i)).toBeInTheDocument();

    fireEvent.click(screen.getByText(/Người thân \(1\)/i));

    const doctorSelect = document.querySelector("#book-doctor") as HTMLSelectElement;
    fireEvent.change(doctorSelect, { target: { value: "doc-1" } });

    const datePicker = screen.getByTestId("book-date");
    fireEvent.change(datePicker, { target: { value: "2026-10-15" } });

    const slotSelect = document.querySelector("#book-slot") as HTMLSelectElement;
    fireEvent.change(slotSelect, { target: { value: "slot-2" } });

    const submitBtn = screen.getByRole("button", { name: "Xác nhận đặt lịch" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        patientProfileId: "prof-child",
        scheduleSlotId: "slot-2",
        reason: undefined,
        relationshipId: "rel-10",
      });
    });
  });
});
