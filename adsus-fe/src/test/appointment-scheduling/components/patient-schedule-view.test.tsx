import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { PatientScheduleView } from "@/features/appointment-scheduling/components/patient-schedule-view";
import type { DoctorPatientAppointment } from "@/features/appointment-scheduling/types/doctor-appointment.types";

const { useDoctorAppointmentsMock } = vi.hoisted(() => ({
  useDoctorAppointmentsMock: vi.fn(),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-doctor-appointments", () => ({
  useDoctorAppointments: useDoctorAppointmentsMock,
}));

function buildAppointment(overrides: Partial<DoctorPatientAppointment>): DoctorPatientAppointment {
  return {
    appointmentId: "appt-1",
    slotDate: "2026-07-06",
    startTime: "08:00:00",
    endTime: "08:30:00",
    patientProfileId: "profile-1",
    patientFullName: "Nguyễn Thị Lan",
    reason: null,
    ...overrides,
  };
}

describe("PatientScheduleView", () => {
  it("đang tải — hiện spinner, chưa hiện nội dung tuần", () => {
    useDoctorAppointmentsMock.mockReturnValue({ data: undefined, isLoading: true, isError: false, error: null });

    render(<PatientScheduleView />);

    expect(screen.queryByText("Không có bệnh nhân")).not.toBeInTheDocument();
  });

  it("lỗi tải — hiện role alert", () => {
    useDoctorAppointmentsMock.mockReturnValue({
      data: undefined, isLoading: false, isError: true, error: new Error("network down"),
    });

    render(<PatientScheduleView />);

    expect(screen.getByRole("alert")).toBeInTheDocument();
  });

  it("ngày không có bệnh nhân — hiện đúng thông báo", () => {
    useDoctorAppointmentsMock.mockReturnValue({ data: [], isLoading: false, isError: false, error: null });

    render(<PatientScheduleView />);

    expect(screen.getAllByText("Không có bệnh nhân").length).toBeGreaterThan(0);
  });

  it("có bệnh nhân đặt lịch — hiện tên và giờ", () => {
    // Component tính tuần hiện tại từ đồng hồ hệ thống thật (useState(() => mondayOfWeek(new
    // Date()))), không nhận "hôm nay" qua props. Ghim đồng hồ về đúng tuần chứa fixture
    // slotDate bên dưới để test không phụ thuộc ngày chạy thật (2026-07-06 là Thứ Hai).
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 6, 6));

    try {
      useDoctorAppointmentsMock.mockReturnValue({
        data: [buildAppointment({ patientFullName: "Phạm Thị Lan", startTime: "08:30:00", endTime: "09:00:00" })],
        isLoading: false, isError: false, error: null,
      });

      render(<PatientScheduleView />);

      expect(screen.getByText("Phạm Thị Lan")).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it("bấm 'Tuần sau' — gọi lại hook với khoảng ngày mới", () => {
    useDoctorAppointmentsMock.mockReturnValue({ data: [], isLoading: false, isError: false, error: null });

    render(<PatientScheduleView />);
    useDoctorAppointmentsMock.mockClear();

    fireEvent.click(screen.getByRole("button", { name: "Tuần sau" }));

    expect(useDoctorAppointmentsMock).toHaveBeenCalled();
  });

  it("đang chuyển tuần (isPlaceholderData) — KHÔNG hiện sai 'Không có bệnh nhân', hiện chỉ báo đang tải", () => {
    // Ghim đồng hồ về tuần chứa 2026-07-13 (Thứ Hai) — tuần ĐANG được hiển thị/yêu cầu.
    // Nhưng data trả về là placeholderData của tuần TRƯỚC (2026-07-06), đúng như hành vi thật
    // của placeholderData: (previous) => previous trong lúc query tuần mới còn đang chạy.
    // groupAppointmentsByWeek sẽ lọc theo slotDate của tuần mới -> mọi ngày đều rỗng nếu component
    // không biết đây là placeholder, dẫn tới hiện sai "Không có bệnh nhân" cho cả 7 ngày (bug F4).
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 6, 13));

    try {
      useDoctorAppointmentsMock.mockReturnValue({
        data: [buildAppointment({ slotDate: "2026-07-06" })],
        isLoading: false,
        isPlaceholderData: true,
        isError: false,
        error: null,
      });

      render(<PatientScheduleView />);

      expect(screen.queryAllByText("Không có bệnh nhân")).toHaveLength(0);
      expect(screen.getByRole("status")).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });
});
