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

function signInAs(role: "DOCTOR" | "STAFF") {
  useAuthStore.getState().signIn("access-token", "refresh-token", {
    userId: "user-1",
    fullName: role === "STAFF" ? "ĐD. Võ Thị Thu Hà" : "BS. Nguyễn Văn An",
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
    signInAs("STAFF");
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

  it("không hiển thị mã bệnh nhân giả (#PT-), cột Mã BN, hay tuổi/giới tính tự sinh", () => {
    signInAs("DOCTOR");
    mockList([withProfile, withoutProfile]);

    render(<PatientListView />);

    // Negative assertions: Không render cột Mã BN hay mã giả #PT-
    expect(screen.queryByRole("columnheader", { name: /mã bn/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/#PT-/i)).not.toBeInTheDocument();

    // Negative assertions: Không render tuổi và giới tính tự sinh từ hash
    expect(screen.queryByText(/tuổi/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/,\s*nữ/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/nữ/i)).not.toBeInTheDocument();

    // Đảm bảo đúng 5 cột Preclinic
    const headers = screen.getAllByRole("columnheader");
    expect(headers).toHaveLength(5);
    expect(screen.getByRole("columnheader", { name: /^bệnh nhân$/i })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /^số điện thoại$/i })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /^lần khám gần nhất$/i })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /^trạng thái$/i })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /^thao tác$/i })).toBeInTheDocument();

    // Bệnh nhân chưa có hồ sơ nền hiển thị đúng chỉ báo
    expect(screen.getByText("Chưa lập hồ sơ nền")).toBeInTheDocument();

    // Bệnh nhân đã có hồ sơ hiển thị tên sạch sẽ
    expect(screen.getByRole("link", { name: "Trần Thị Mai" })).toBeInTheDocument();
  });
});
