import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { CreateCaseForm } from "@/features/medical-record/components/create-case-form";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

const { createMutate, doctorListMock } = vi.hoisted(() => ({
  createMutate: vi.fn(),
  doctorListMock: vi.fn(),
}));

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCreateCase: () => ({
    mutate: createMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
  useCaseList: () => ({
    data: { items: [] },
    isLoading: false,
  }),
  useCaseDetail: () => ({
    data: null,
    isLoading: false,
  }),
}));

vi.mock("@/features/medical-record/hooks/use-doctors", () => ({
  useDoctorList: (enabled: boolean) => doctorListMock(enabled),
}));

vi.mock("@/features/medical-record/hooks/use-symptoms", () => ({
  useSymptomCategories: () => ({
    data: [],
    isLoading: false,
  }),
}));

vi.mock("@/features/medical-record/hooks/use-patient-profile", () => ({
  usePatientProfile: () => ({
    data: null,
    isLoading: false,
  }),
}));

function signInAs(role: "DOCTOR" | "NURSE", userId: string, fullName: string) {
  useAuthStore.getState().signIn("token", {
    userId,
    fullName,
    email: null,
    role,
    mustChangePassword: false,
  });
}

describe("CreateCaseForm", () => {
  beforeEach(() => {
    createMutate.mockReset();
    doctorListMock.mockReset();
    doctorListMock.mockReturnValue({
      data: [
        { userId: "doctor-1", fullName: "BS. Nguyễn Văn An" },
        { userId: "doctor-2", fullName: "BS. Lê Minh Hoàng" },
      ],
      isLoading: false,
      isError: false,
      error: null,
    });
    useAuthStore.getState().signOut();
  });

  it("Bác sĩ luôn là người phụ trách chính mình, không có ô chọn khác", () => {
    // Sửa 07/08/2026 — trước đây điền sẵn nhưng vẫn đổi được; giờ khoá cứng, khớp UCS UC-07
    // bước 5 ("... or defaults to the signed-in Doctor").
    signInAs("DOCTOR", "doctor-2", "BS. Lê Minh Hoàng");

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByText("BS. Lê Minh Hoàng")).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });

  it("Bác sĩ không cần tải danh sách bác sĩ khác", () => {
    signInAs("DOCTOR", "doctor-2", "BS. Lê Minh Hoàng");

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(doctorListMock).toHaveBeenCalledWith(false);
  });

  it("Điều dưỡng phải chọn bác sĩ phụ trách, mặc định để trống", () => {
    // UC-07 bước 5 — Điều dưỡng tạo ca hộ thì phải chọn đúng bác sĩ chịu trách nhiệm; điền
    // sẵn một cái tên bất kỳ là mời gọi gán nhầm.
    signInAs("NURSE", "nurse-1", "Điều dưỡng");

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByRole("combobox")).toHaveValue("");
    expect(doctorListMock).toHaveBeenCalledWith(true);
  });

  it("chặn lưu khi Điều dưỡng chưa chọn bác sĩ", async () => {
    signInAs("NURSE", "nurse-1", "Điều dưỡng");
    const user = userEvent.setup();

    render(<CreateCaseForm patientProfileId="profile-1" />);
    await user.click(screen.getByRole("button", { name: /lưu ca khám/i }));

    expect(createMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/chọn bác sĩ/i);
  });

  it("tự động gán Khám định kì khi không chọn triệu chứng nào", async () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    const user = userEvent.setup();

    render(<CreateCaseForm patientProfileId="profile-1" />);

    await user.click(screen.getByRole("button", { name: /lưu ca khám/i }));

    expect(createMutate).toHaveBeenCalledWith(
      {
        patientProfileId: "profile-1",
        responsibleDoctorId: "doctor-1",
        clinicalInfo: "Khám định kì",
        symptoms: [],
        images: [],
      },
      expect.anything(),
    );
  });

  it("gửi đúng bác sĩ đã chọn khi Điều dưỡng lưu ca khám", async () => {
    signInAs("NURSE", "nurse-1", "Điều dưỡng");
    const user = userEvent.setup();

    render(<CreateCaseForm patientProfileId="profile-1" />);

    await user.selectOptions(screen.getByRole("combobox"), "doctor-2");
    await user.click(screen.getByRole("button", { name: /lưu ca khám/i }));

    expect(createMutate).toHaveBeenCalledWith(
      expect.objectContaining({ responsibleDoctorId: "doctor-2" }),
      expect.anything(),
    );
  });
});
