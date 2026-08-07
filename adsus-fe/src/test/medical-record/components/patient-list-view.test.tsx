import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { PatientListView } from "@/features/medical-record/components/patient-list-view";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

const { listMock } = vi.hoisted(() => ({ listMock: vi.fn() }));

vi.mock("@/features/medical-record/hooks/use-patients", () => ({
  usePatientList: () => listMock(),
}));

function signInAs(role: "DOCTOR" | "NURSE") {
  useAuthStore.getState().signIn("token", {
    userId: "user-1",
    fullName: role === "NURSE" ? "ĐD. Võ Thị Thu Hà" : "BS. Nguyễn Văn An",
    email: null,
    role,
    mustChangePassword: false,
  });
}

const withProfile = {
  patientProfileId: "profile-1",
  patientUserId: "user-10",
  fullName: "Trần Thị Mai",
  phone: "0987654321",
  latestVisitDate: "2026-07-22",
  latestVisitStatus: "CONFIRMED" as const,
};

const withoutProfile = {
  patientProfileId: null,
  patientUserId: "user-11",
  fullName: "Phạm Hồng Hạnh",
  phone: "0912345678",
  latestVisitDate: null,
  latestVisitStatus: null,
};

function mockList(items: unknown[]) {
  listMock.mockReturnValue({
    data: { items, page: 1, pageSize: 20, totalItems: items.length, totalPages: 1 },
    isLoading: false,
    isError: false,
    error: null,
  });
}

describe("PatientListView", () => {
  beforeEach(() => {
    listMock.mockReset();
    useAuthStore.getState().signOut();
  });

  it("dòng chưa có hồ sơ nền hiện nút Tạo hồ sơ nền thay vì Xem hồ sơ", () => {
    signInAs("DOCTOR");
    mockList([withoutProfile]);

    render(<PatientListView />);

    // patientProfileId null nghĩa là tài khoản đã có nhưng chưa lập hồ sơ nền. Không thể
    // "Xem hồ sơ" (chưa có), cũng không thể "Tạo ca khám" (UC-07 đòi hồ sơ nền trước).
    expect(screen.getByRole("link", { name: /tạo hồ sơ nền/i })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /xem hồ sơ bệnh án/i })).not.toBeInTheDocument();
  });

  it("dòng đã có hồ sơ nền hiện cả hai nút", () => {
    signInAs("DOCTOR");
    mockList([withProfile]);

    render(<PatientListView />);

    expect(screen.getByRole("link", { name: /xem hồ sơ bệnh án/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /tạo ca khám/i })).toBeInTheDocument();
  });

  it("ẩn nút Thêm bệnh nhân mới khỏi Bác sĩ", () => {
    // UC-06 BR-03 — chỉ Điều dưỡng tạo được tài khoản. Bác sĩ vẫn phải nhờ Quản trị viên.
    signInAs("DOCTOR");
    mockList([withProfile]);

    render(<PatientListView />);

    expect(screen.queryByRole("link", { name: /thêm bệnh nhân mới/i })).not.toBeInTheDocument();
  });

  it("hiện nút Thêm bệnh nhân mới cho Điều dưỡng", () => {
    signInAs("NURSE");
    mockList([withProfile]);

    render(<PatientListView />);

    expect(screen.getByRole("link", { name: /thêm bệnh nhân mới/i })).toBeInTheDocument();
  });

  it("hiện trạng thái rỗng khi không có kết quả", () => {
    // UC-09 AF-01.
    signInAs("DOCTOR");
    mockList([]);

    render(<PatientListView />);

    expect(screen.getByText(/không tìm thấy bệnh nhân nào/i)).toBeInTheDocument();
  });
});
