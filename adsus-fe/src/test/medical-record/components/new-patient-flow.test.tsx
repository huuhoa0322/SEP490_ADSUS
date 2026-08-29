import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { NewPatientFlow } from "@/features/medical-record/components/new-patient-flow";

const { pushMock } = vi.hoisted(() => ({ pushMock: vi.fn() }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

const { listMock } = vi.hoisted(() => ({ listMock: vi.fn() }));
vi.mock("@/features/medical-record/hooks/use-patients", () => ({
  usePatientList: () => listMock(),
}));

vi.mock("@/features/medical-record/components/patient-account-form", () => ({
  PatientAccountForm: () => <div data-testid="patient-account-form" />,
}));

vi.mock("@/features/medical-record/components/patient-profile-form", () => ({
  PatientProfileForm: (props: { patientUserId: string }) => (
    <div data-testid="patient-profile-form">{props.patientUserId}</div>
  ),
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

const account = {
  patientProfileId: null,
  patientUserId: "user-10",
  fullName: "Trần Thị Mai",
  phone: "0987654321",
  latestVisitDate: null,
  latestVisitStatus: null,
};

describe("NewPatientFlow", () => {
  beforeEach(() => {
    listMock.mockReset();
    pushMock.mockReset();
    useAuthStore.getState().signOut();
  });

  it("luồng A: đang tải danh sách tài khoản", () => {
    signInAs("NURSE");
    listMock.mockReturnValue({ isLoading: true, isError: false, data: undefined, error: null });

    render(<NewPatientFlow patientUserId="user-10" />);

    expect(screen.getByText(/đang tải thông tin tài khoản/i)).toBeInTheDocument();
  });

  it("luồng A: lỗi tải danh sách tài khoản", () => {
    signInAs("NURSE");
    listMock.mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
      error: new Error("network"),
    });

    render(<NewPatientFlow patientUserId="user-10" />);

    expect(screen.getByRole("alert")).toBeInTheDocument();
  });

  it("luồng A: tài khoản đã có hồ sơ nền từ trước (tab khác vừa tạo) → hiện fallback và điều hướng về danh sách", async () => {
    signInAs("NURSE");
    listMock.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], page: 1, pageSize: 100, totalItems: 0, totalPages: 1 },
      error: null,
    });

    render(<NewPatientFlow patientUserId="user-10" />);

    expect(screen.getByText(/tài khoản này đã có hồ sơ nền/i)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /về danh sách bệnh nhân/i }));
    expect(pushMock).toHaveBeenCalledWith("/patients");
  });

  it("luồng A: tìm thấy tài khoản → render PatientProfileForm với đúng patientUserId", () => {
    signInAs("NURSE");
    listMock.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [account], page: 1, pageSize: 100, totalItems: 1, totalPages: 1 },
      error: null,
    });

    render(<NewPatientFlow patientUserId="user-10" />);

    expect(screen.getByTestId("patient-profile-form")).toHaveTextContent("user-10");
  });

  it("luồng B: Bác sĩ (không phải Điều dưỡng) bị chặn tạo tài khoản mới", () => {
    signInAs("DOCTOR");
    listMock.mockReturnValue({ isLoading: false, isError: false, data: undefined, error: null });

    render(<NewPatientFlow />);

    expect(screen.getByText(/chỉ điều dưỡng tạo được tài khoản bệnh nhân/i)).toBeInTheDocument();
    expect(screen.queryByTestId("patient-account-form")).not.toBeInTheDocument();
  });

  it("luồng B: Điều dưỡng thấy form tạo tài khoản mới", () => {
    signInAs("NURSE");
    listMock.mockReturnValue({ isLoading: false, isError: false, data: undefined, error: null });

    render(<NewPatientFlow />);

    expect(screen.getByTestId("patient-account-form")).toBeInTheDocument();
  });
});
