import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { PatientRecordView } from "@/features/medical-record/components/patient-record-view";

const { profileMock, caseListMock } = vi.hoisted(() => ({
  profileMock: vi.fn(),
  caseListMock: vi.fn(),
}));

vi.mock("@/features/medical-record/hooks/use-patient-profile", () => ({
  usePatientProfile: () => profileMock(),
}));
vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCaseList: () => caseListMock(),
}));

const profile = {
  patientProfileId: "profile-1",
  patientUserId: "user-1",
  fullName: "Trần Thị Mai",
  phone: "0987654321",
  dateOfBirth: "1988-05-12",
  gender: "FEMALE" as const,
  diseases: [{ diseaseId: "d1", diseaseName: "U lành tính", isOther: false, note: null }],
  allergies: [{ allergyTypeId: "a1", allergyName: "Penicillin", isOther: false, note: null }],
  createdBy: "nurse-1",
  createdAt: "2026-08-04T09:00:00Z",
  updatedAt: "2026-08-04T09:00:00Z",
};

describe("PatientRecordView", () => {
  beforeEach(() => {
    profileMock.mockReturnValue({ data: profile, isLoading: false, isError: false, error: null });
    caseListMock.mockReturnValue({
      data: {
        items: [
          {
            caseId: "case-1",
            visitDate: "2026-07-22",
            status: "ANALYZED",
            doctorId: "doctor-1",
            createdAt: "2026-07-22T14:05:00Z",
          },
        ],
        page: 1,
        pageSize: 20,
        totalItems: 1,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
      error: null,
    });
  });

  it("mỗi lần khám có liên kết sang màn chi tiết ca", () => {
    render(<PatientRecordView profileId="profile-1" />);

    expect(screen.getByRole("link", { name: /xem chi tiết ca/i })).toHaveAttribute(
      "href",
      "/cases/case-1",
    );
  });

  it("hiện giờ tạo ca thay vì id thô", () => {
    // Sửa 06/08/2026 — id ca khám (UUID) trước đây hiện thẳng ra màn, không có ích cho người
    // đọc; thay bằng giờ tạo (createdAt, #24 mới thêm) hữu ích hơn hẳn.
    render(<PatientRecordView profileId="profile-1" />);

    expect(screen.queryByText("case-1")).not.toBeInTheDocument();
    expect(screen.getByText(/tạo lúc 22\/07\/2026/i)).toBeInTheDocument();
  });

  it("hiện tiền sử và dị ứng của hồ sơ nền", () => {
    render(<PatientRecordView profileId="profile-1" />);

    expect(screen.getByText("Penicillin")).toBeInTheDocument();
    expect(screen.getByText(/u lành tính/i)).toBeInTheDocument();
  });

  it("không render khối AI hay đơn thuốc", () => {
    render(<PatientRecordView profileId="profile-1" />);

    expect(screen.queryByText(/độ tin cậy/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/đơn thuốc/i)).not.toBeInTheDocument();
  });

  it("hiện trạng thái rỗng khi bệnh nhân chưa có lần khám nào", () => {
    caseListMock.mockReturnValue({
      data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 1 },
      isLoading: false,
      isError: false,
      error: null,
    });

    render(<PatientRecordView profileId="profile-1" />);

    expect(screen.getByText(/chưa có lần khám nào/i)).toBeInTheDocument();
  });
});
