import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { format } from "date-fns";
import { AppointmentDetailModal } from "../components/appointment-detail-modal";
import type {
  CheckinQueueItem,
  DoctorSummary,
  AvailableSlot,
} from "../types/checkin.types";
import {
  useDoctorList,
  useAvailableSlots,
  useRescheduleAppointment,
} from "../hooks/use-reschedule";

vi.mock("../hooks/use-reschedule", () => ({
  useDoctorList: vi.fn(),
  useAvailableSlots: vi.fn(),
  useRescheduleAppointment: vi.fn(),
}));

describe("AppointmentDetailModal", () => {
  const mockClose = vi.fn();
  const mockCheckin = vi.fn();
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

  // Luôn tính là "ngày mai" tại thời điểm chạy test — component ẩn slot đã qua
  // giờ trong NGÀY HÔM NAY (so với đồng hồ thật của máy chạy test, xem
  // filteredAvailableSlots trong appointment-detail-modal.tsx). Hard-code một
  // ngày cụ thể từng khiến các test này chỉ pass trước 09:00 giờ máy chạy test.
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

  const baseItem: CheckinQueueItem = {
    appointmentId: "app-100",
    slotTime: "2099-09-12T08:30:00Z",
    patientFullName: "Nguyễn Văn An",
    patientPhone: "0901234567",
    patientProfileId: "prof-100",
    caseId: "case-100",
    reason: "Đau đầu, chóng mặt",
    doctorName: "BS. Trần Văn Minh",
    doctorId: "doc-1",
    status: "Booked",
    caseStatus: "Booked",
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

    vi.mocked(useRescheduleAppointment).mockReturnValue({
      mutateAsync: mockMutateAsync,
      isPending: false,
    } as unknown as ReturnType<typeof useRescheduleAppointment>);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // 1. Rendering / Modal Visibility
  // =========================================================================
  describe("Modal Visibility Guard", () => {
    it("should render nothing when isOpen is false", () => {
      const { container } = render(
        <AppointmentDetailModal
          isOpen={false}
          item={baseItem}
          onClose={mockClose}
        />
      );
      expect(container).toBeEmptyDOMElement();
      expect(screen.queryByText("Chi tiết lịch hẹn")).not.toBeInTheDocument();
    });

    it("should render nothing when item is null even if isOpen is true", () => {
      const { container } = render(
        <AppointmentDetailModal
          isOpen={true}
          item={null}
          onClose={mockClose}
        />
      );
      expect(container).toBeEmptyDOMElement();
      expect(screen.queryByText("Chi tiết lịch hẹn")).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // 2. View Mode Display
  // =========================================================================
  describe("View Mode Display", () => {
    it("should render patient name, doctor, phone, reason, and Booked status badge", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      expect(screen.getByText("Chi tiết lịch hẹn")).toBeInTheDocument();
      expect(screen.getByText("Nguyễn Văn An")).toBeInTheDocument();
      expect(screen.getByText("BS. Trần Văn Minh")).toBeInTheDocument();
      expect(screen.getByText("0901234567")).toBeInTheDocument();
      expect(screen.getByText("Đau đầu, chóng mặt")).toBeInTheDocument();
      expect(screen.getByText("Đang chờ check-in")).toBeInTheDocument();
    });

    it("should display placeholder '—' when phone and reason are missing or null", () => {
      const itemWithoutDetails: CheckinQueueItem = {
        ...baseItem,
        patientPhone: null,
        reason: null,
      };

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={itemWithoutDetails}
          onClose={mockClose}
        />
      );

      const dashes = screen.getAllByText("—");
      expect(dashes.length).toBeGreaterThanOrEqual(2);
    });

    it("should display 'Đã check-in' badge for APPROVED appointment status", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Approved" }}
          onClose={mockClose}
        />
      );
      expect(screen.getByText("Đã check-in")).toBeInTheDocument();
    });

    it("should display 'Đã hoàn thành' badge for COMPLETED appointment status", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Completed" }}
          onClose={mockClose}
        />
      );
      expect(screen.getByText("Đã check-in")).toBeInTheDocument();
    });

    it("should display 'Đã huỷ / Vắng mặt' badge for CANCELLED and NO_SHOW statuses", () => {
      const { rerender } = render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Cancelled" }}
          onClose={mockClose}
        />
      );
      expect(screen.getByText("Đã huỷ / Vắng mặt")).toBeInTheDocument();

      rerender(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "NoShow" }}
          onClose={mockClose}
        />
      );
      expect(screen.getByText("Đã huỷ / Vắng mặt")).toBeInTheDocument();

      rerender(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "NO_SHOW" }}
          onClose={mockClose}
        />
      );
      expect(screen.getByText("Đã huỷ / Vắng mặt")).toBeInTheDocument();
    });

    it("should display fallback outline badge when status is unfamiliar", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "UNKNOWN_STATUS" }}
          onClose={mockClose}
        />
      );
      expect(screen.getByText("UNKNOWN_STATUS")).toBeInTheDocument();
    });
  });

  // =========================================================================
  // 3. Action Buttons & Scenario Permissions in View Mode
  // =========================================================================
  describe("Action Buttons & Permissions in View Mode", () => {
    it("should render 'Check-in' button when status is BOOKED and trigger onCheckin and onClose when clicked", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
          onCheckin={mockCheckin}
        />
      );

      const checkinBtn = screen.getByRole("button", { name: "Check-in" });
      expect(checkinBtn).toBeInTheDocument();

      fireEvent.click(checkinBtn);
      expect(mockCheckin).toHaveBeenCalledWith(baseItem);
      expect(mockClose).toHaveBeenCalled();
    });

    it("should NOT render 'Check-in' button when status is COMPLETED, APPROVED, CANCELLED, or NO_SHOW", () => {
      const statuses = ["Completed", "Approved", "Cancelled", "NoShow"];

      for (const status of statuses) {
        const { unmount } = render(
          <AppointmentDetailModal
            isOpen={true}
            item={{ ...baseItem, status }}
            onClose={mockClose}
            onCheckin={mockCheckin}
          />
        );
        expect(screen.queryByRole("button", { name: "Check-in" })).not.toBeInTheDocument();
        unmount();
      }
    });

    it("should NOT render 'Check-in' button if onCheckin prop is not provided", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );
      expect(screen.queryByRole("button", { name: "Check-in" })).not.toBeInTheDocument();
    });

    it("should trigger onClose when 'Đóng' button is clicked", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      const closeBtn = screen.getByRole("button", { name: "Đóng" });
      fireEvent.click(closeBtn);
      expect(mockClose).toHaveBeenCalled();
    });

    it("Scenario 1 (BOOKED): 'Đổi lịch' button is present and enabled", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Booked", caseStatus: "Booked" }}
          onClose={mockClose}
        />
      );

      const rescheduleBtn = screen.getByRole("button", { name: /Đổi lịch/i });
      expect(rescheduleBtn).toBeInTheDocument();
      expect(rescheduleBtn).toBeEnabled();
    });

    it("Scenario 2 (COMPLETED / APPROVED with InProgress case): 'Đổi lịch' button is present and enabled", () => {
      const { rerender } = render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Completed", caseStatus: "InProgress" }}
          onClose={mockClose}
        />
      );

      expect(screen.getByRole("button", { name: /Đổi lịch/i })).toBeEnabled();

      rerender(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Approved", caseStatus: "InProgress" }}
          onClose={mockClose}
        />
      );

      expect(screen.getByRole("button", { name: /Đổi lịch/i })).toBeEnabled();
    });

    it("Scenario 2 (COMPLETED with CONFIRMED case): 'Đổi lịch' button is hidden and warning banner is displayed", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Completed", caseStatus: "Confirmed" }}
          onClose={mockClose}
        />
      );

      expect(screen.queryByRole("button", { name: /Đổi lịch/i })).not.toBeInTheDocument();
      expect(screen.getByText("Ca khám đã kết thúc, không thể đổi lịch.")).toBeInTheDocument();
    });

    it("Scenario 2 (COMPLETED with END case): 'Đổi lịch' button is hidden and warning banner is displayed", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Completed", caseStatus: "End" }}
          onClose={mockClose}
        />
      );

      expect(screen.queryByRole("button", { name: /Đổi lịch/i })).not.toBeInTheDocument();
      expect(screen.getByText("Ca khám đã kết thúc, không thể đổi lịch.")).toBeInTheDocument();
    });

    it("Scenario 3 (CANCELLED / NO_SHOW): 'Đổi lịch' button is present and enabled", () => {
      const { rerender } = render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Cancelled", caseStatus: "Cancelled" }}
          onClose={mockClose}
        />
      );

      expect(screen.getByRole("button", { name: /Đổi lịch/i })).toBeEnabled();

      rerender(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "NoShow", caseStatus: "Cancelled" }}
          onClose={mockClose}
        />
      );

      expect(screen.getByRole("button", { name: /Đổi lịch/i })).toBeEnabled();
    });
  });

  // =========================================================================
  // 4. Reschedule Form Mode Navigation & Inputs
  // =========================================================================
  describe("Reschedule Form Mode Navigation & Inputs", () => {
    it("should switch to Reschedule Form mode on 'Đổi lịch' and back to View mode on 'Quay lại'", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      // Initially in View mode
      expect(screen.getByText("Chi tiết lịch hẹn")).toBeInTheDocument();

      // Click "Đổi lịch"
      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      // Now in Reschedule Form mode
      expect(screen.getByText("Đổi lịch hẹn")).toBeInTheDocument();
      expect(screen.getByText("Bệnh nhân:")).toBeInTheDocument();

      // Click "Quay lại"
      fireEvent.click(screen.getByRole("button", { name: /Quay lại/i }));

      // Returns to View mode
      expect(screen.getByText("Chi tiết lịch hẹn")).toBeInTheDocument();
    });

    it("should populate doctor select and preselect current doctor", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const doctorSelect = document.querySelector("#reschedule-doctor") as HTMLSelectElement;
      expect(doctorSelect).toBeInTheDocument();
      expect(doctorSelect.value).toBe("doc-1");

      // Verify options are present
      expect(screen.getByRole("option", { name: "BS. Trần Văn Minh" })).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "BS. Lê Thị Hoa" })).toBeInTheDocument();
    });

    it("should display loading indicator when doctors are loading", () => {
      vi.mocked(useDoctorList).mockReturnValue({
        data: [],
        isLoading: true,
      } as unknown as ReturnType<typeof useDoctorList>);

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      expect(screen.getByText("Đang tải danh sách bác sĩ...")).toBeInTheDocument();
    });

    it("should populate available slots dropdown formatted as 'HH:mm – HH:mm'", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      expect(screen.getByText(/-- Chọn khung giờ \(2 slot trống\) --/i)).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "09:00 – 10:00" })).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "10:00 – 11:00" })).toBeInTheDocument();
    });

    it("should show loading state for slots when isLoadingSlots is true", () => {
      vi.mocked(useAvailableSlots).mockReturnValue({
        data: [],
        isLoading: true,
      } as unknown as ReturnType<typeof useAvailableSlots>);

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      expect(screen.getByText("Đang tải khung giờ...")).toBeInTheDocument();
    });

    it("should show 'Không có khung giờ trống' when availableSlots list is empty", () => {
      vi.mocked(useAvailableSlots).mockReturnValue({
        data: [],
        isLoading: false,
      } as unknown as ReturnType<typeof useAvailableSlots>);

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      expect(screen.getByText("Không có khung giờ trống")).toBeInTheDocument();
    });

    it("should reset slot selection when doctor is changed", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });
      expect(slotSelect.value).toBe("slot-1");

      const doctorSelect = document.querySelector("#reschedule-doctor") as HTMLSelectElement;
      fireEvent.change(doctorSelect, { target: { value: "doc-2" } });

      expect(slotSelect.value).toBe("");
    });

    it("should reset slot selection when date is changed", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });
      expect(slotSelect.value).toBe("slot-1");

      const dateInput = document.querySelector("#reschedule-date") as HTMLInputElement;
      fireEvent.change(dateInput, { target: { value: "20/09/2026" } });

      expect(slotSelect.value).toBe("");
    });

    it("should pre-fill 'Lý do khám' with existing appointment reason and allow editing", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const newReasonInput = document.querySelector("#reschedule-new-reason") as HTMLTextAreaElement;
      expect(newReasonInput.value).toBe("Đau đầu, chóng mặt");

      fireEvent.change(newReasonInput, { target: { value: "Khám thêm tai mũi họng" } });
      expect(newReasonInput.value).toBe("Khám thêm tai mũi họng");
    });
  });

  // =========================================================================
  // 5. Validation Handling
  // =========================================================================
  describe("Form Validation Handling", () => {
    it("should require slot selection and reschedule reason", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const submitBtn = screen.getByRole("button", { name: /Xác nhận đổi lịch/i });
      // Button is disabled when slot or reason is empty
      expect(submitBtn).toBeDisabled();

      // Enter reschedule reason
      const reasonInput = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      fireEvent.change(reasonInput, { target: { value: "Bác sĩ bận việc gấp" } });
      expect(submitBtn).toBeDisabled();

      // Select slot
      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });
      expect(submitBtn).toBeEnabled();
    });

    it("should clear validation errors when 'Quay lại' is clicked", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      // Back to view mode
      fireEvent.click(screen.getByRole("button", { name: /Quay lại/i }));
      expect(screen.queryByText(/Vui lòng/i)).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // 6. Smart AutoCheckin Checkbox Defaults
  // =========================================================================
  describe("Smart AutoCheckin Checkbox Defaults across Scenarios", () => {
    it("Scenario 1 (BOOKED): AutoCheckin defaults to false (unchecked)", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Booked" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      expect(checkbox).toHaveAttribute("data-state", "unchecked");
    });

    it("Scenario 2 (COMPLETED + InProgress): AutoCheckin defaults to true (checked)", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Completed", caseStatus: "InProgress" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      expect(checkbox).toHaveAttribute("data-state", "checked");
    });

    it("Scenario 2 (APPROVED + InProgress): AutoCheckin defaults to true (checked)", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Approved", caseStatus: "InProgress" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      expect(checkbox).toHaveAttribute("data-state", "checked");
    });

    it("Scenario 3 (CANCELLED): AutoCheckin defaults to false (unchecked)", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Cancelled", caseStatus: "Cancelled" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      expect(checkbox).toHaveAttribute("data-state", "unchecked");
    });

    it("Scenario 3 (NO_SHOW): AutoCheckin defaults to false (unchecked)", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "NoShow", caseStatus: "Cancelled" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      expect(checkbox).toHaveAttribute("data-state", "unchecked");
    });

    it("should allow nurse to toggle AutoCheckin checkbox manually", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "Booked" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      expect(checkbox).toHaveAttribute("data-state", "unchecked");

      // Click to toggle on
      fireEvent.click(checkbox);
      expect(checkbox).toHaveAttribute("data-state", "checked");

      // Click to toggle off
      fireEvent.click(checkbox);
      expect(checkbox).toHaveAttribute("data-state", "unchecked");
    });
  });

  // =========================================================================
  // 7. Form Submission & API Mutation
  // =========================================================================
  describe("Form Submission & API Mutation", () => {
    it("should invoke reschedule mutation with correct payload and close modal on success", async () => {
      mockMutateAsync.mockResolvedValueOnce({ ok: true });

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      // Select slot
      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-2" } });

      // Change examination reason
      const newReasonInput = document.querySelector("#reschedule-new-reason") as HTMLTextAreaElement;
      fireEvent.change(newReasonInput, { target: { value: "Tái khám theo lịch hẹn mới" } });

      // Enter reschedule reason
      const rescheduleReasonInput = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      fireEvent.change(rescheduleReasonInput, { target: { value: "Bệnh nhân yêu cầu dời giờ" } });

      // Click submit
      const submitBtn = screen.getByRole("button", { name: /Xác nhận đổi lịch/i });
      fireEvent.click(submitBtn);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith({
          appointmentId: "app-100",
          request: {
            newScheduleSlotId: "slot-2",
            rescheduleReason: "Bệnh nhân yêu cầu dời giờ",
            newReason: "Tái khám theo lịch hẹn mới",
            autoCheckin: false,
          },
        });
        expect(mockClose).toHaveBeenCalled();
      });
    });

    it("should send autoCheckin=true when checkbox is toggled on during submission", async () => {
      mockMutateAsync.mockResolvedValueOnce({ ok: true });

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      // Select slot
      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });

      // Enter reschedule reason
      const rescheduleReasonInput = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      fireEvent.change(rescheduleReasonInput, { target: { value: "Checkin ngay cho bệnh nhân có mặt" } });

      // Toggle autoCheckin checkbox on
      const checkbox = document.querySelector("#auto-checkin") as HTMLElement;
      fireEvent.click(checkbox);

      // Submit
      const submitBtn = screen.getByRole("button", { name: /Xác nhận đổi lịch/i });
      fireEvent.click(submitBtn);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith({
          appointmentId: "app-100",
          request: {
            newScheduleSlotId: "slot-1",
            rescheduleReason: "Checkin ngay cho bệnh nhân có mặt",
            newReason: "Đau đầu, chóng mặt",
            autoCheckin: true,
          },
        });
      });
    });

    it("should display loading state while mutation is pending", () => {
      vi.mocked(useRescheduleAppointment).mockReturnValue({
        mutateAsync: mockMutateAsync,
        isPending: true,
      } as unknown as ReturnType<typeof useRescheduleAppointment>);

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      expect(screen.getByText("Đang xử lý...")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Đang xử lý.../i })).toBeDisabled();
      expect(screen.getByRole("button", { name: /Quay lại/i })).toBeDisabled();
    });

    it("should not close modal when mutation rejects/fails", async () => {
      mockMutateAsync.mockRejectedValueOnce(new Error("Network Error"));

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });

      const rescheduleReasonInput = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      fireEvent.change(rescheduleReasonInput, { target: { value: "Lý do hợp lệ" } });

      const submitBtn = screen.getByRole("button", { name: /Xác nhận đổi lịch/i });
      fireEvent.click(submitBtn);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalled();
      });

      // onClose must NOT be called on failure
      expect(mockClose).not.toHaveBeenCalled();
      // Still in reschedule mode
      expect(screen.getByText("Đổi lịch hẹn")).toBeInTheDocument();
    });
  });

  // =========================================================================
  // 8. Adversarial Stress & Edge Cases
  // =========================================================================
  describe("Adversarial Stress & Edge Cases", () => {
    it("CHALLENGE 1: guarantees zero state leakage when switching active appointments", () => {
      const item2: CheckinQueueItem = {
        ...baseItem,
        appointmentId: "app-200",
        patientFullName: "Bệnh nhân Thứ Hai",
        doctorName: "BS. Lê Thị Hoa",
        doctorId: "doc-2",
        status: "Cancelled",
        caseStatus: "Cancelled",
        reason: "Lý do khám của BN 2",
      };

      const { rerender } = render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
          onCheckin={mockCheckin}
        />
      );

      // Enter reschedule mode for baseItem
      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      expect(screen.getByText("Đổi lịch hẹn")).toBeInTheDocument();

      // Dirty the state: enter reschedule reason and select slot
      const reasonInput = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      fireEvent.change(reasonInput, { target: { value: "Lý do tạm của BN 1" } });
      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });

      // Switch to item2
      rerender(
        <AppointmentDetailModal
          isOpen={true}
          item={item2}
          onClose={mockClose}
          onCheckin={mockCheckin}
        />
      );

      // Modal must reset to View Mode for item2
      expect(screen.getByText("Chi tiết lịch hẹn")).toBeInTheDocument();
      expect(screen.getByText("Bệnh nhân Thứ Hai")).toBeInTheDocument();
      expect(screen.getByText("Lý do khám của BN 2")).toBeInTheDocument();

      // Open reschedule mode for item2
      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      // State must be completely clean without leaking baseItem dirty fields
      const reasonInput2 = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      expect(reasonInput2.value).toBe("");

      const doctorSelect2 = document.querySelector("#reschedule-doctor") as HTMLSelectElement;
      expect(doctorSelect2.value).toBe("doc-2");

      const slotSelect2 = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      expect(slotSelect2.value).toBe("");

      const autoCheckin2 = document.querySelector("#auto-checkin") as HTMLElement;
      expect(autoCheckin2).toHaveAttribute("data-state", "unchecked");
    });

    it("CHALLENGE 2: rejects whitespace-only reschedule reason inputs", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={baseItem}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      fireEvent.change(slotSelect, { target: { value: "slot-1" } });

      const reasonInput = document.querySelector("#reschedule-reason") as HTMLTextAreaElement;
      fireEvent.change(reasonInput, { target: { value: "   \t  \n   " } });

      const submitBtn = screen.getByRole("button", { name: /Xác nhận đổi lịch/i });
      expect(submitBtn).toBeDisabled();
      expect(mockMutateAsync).not.toHaveBeenCalled();
    });

    it("CHALLENGE 3: normalizes lowercase and mixed-case statuses accurately", () => {
      const { rerender } = render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, status: "booked" }}
          onClose={mockClose}
        />
      );
      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      expect(document.querySelector("#auto-checkin")).toHaveAttribute("data-state", "unchecked");

      rerender(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, appointmentId: "app-case-ins", status: "completed", caseStatus: "inprogress" }}
          onClose={mockClose}
        />
      );
      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      expect(document.querySelector("#auto-checkin")).toHaveAttribute("data-state", "checked");
    });

    it("CHALLENGE 4: falls back to doctor name lookup when doctorId is missing", () => {
      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, doctorId: undefined, doctorName: "BS. Lê Thị Hoa" }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const doctorSelect = document.querySelector("#reschedule-doctor") as HTMLSelectElement;
      expect(doctorSelect.value).toBe("doc-2");
    });

    it("CHALLENGE 5: safely handles empty doctors list without crashing", () => {
      vi.mocked(useDoctorList).mockReturnValue({
        data: [],
        isLoading: false,
      } as unknown as ReturnType<typeof useDoctorList>);

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, doctorId: undefined, doctorName: "" }}
          onClose={mockClose}
        />
      );

      expect(() => {
        fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));
      }).not.toThrow();

      const doctorSelect = document.querySelector("#reschedule-doctor") as HTMLSelectElement;
      expect(doctorSelect.value).toBe("");
    });

    it("filters out past slots when appointment date is today", () => {
      const todayStr = format(new Date(), "yyyy-MM-dd");
      const slotsToday: AvailableSlot[] = [
        {
          slotId: "slot-past",
          doctorId: "doc-1",
          doctorName: "BS. Trần Văn Minh",
          slotDate: todayStr,
          startTime: "00:01:00",
          endTime: "00:30:00",
        },
        {
          slotId: "slot-future",
          doctorId: "doc-1",
          doctorName: "BS. Trần Văn Minh",
          slotDate: todayStr,
          startTime: "23:30:00",
          endTime: "23:59:00",
        },
      ];

      vi.mocked(useAvailableSlots).mockReturnValue({
        data: slotsToday,
        isLoading: false,
      } as unknown as ReturnType<typeof useAvailableSlots>);

      render(
        <AppointmentDetailModal
          isOpen={true}
          item={{ ...baseItem, slotTime: `${todayStr}T10:00:00Z` }}
          onClose={mockClose}
        />
      );

      fireEvent.click(screen.getByRole("button", { name: /Đổi lịch/i }));

      const slotSelect = document.querySelector("#reschedule-slot") as HTMLSelectElement;
      const options = Array.from(slotSelect.querySelectorAll("option"));

      expect(options.some((o) => o.value === "slot-past")).toBe(false);
      expect(options.some((o) => o.value === "slot-future")).toBe(true);
    });
  });
});
