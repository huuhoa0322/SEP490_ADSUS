import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { CreateCaseForm } from "@/features/medical-record/components/create-case-form";

const {
  createMutate,
  doctorListMock,
  caseListMock,
  caseDetailMock,
  patientProfileMock,
  routerPushMock,
  routerBackMock,
} = vi.hoisted(() => ({
  createMutate: vi.fn(),
  doctorListMock: vi.fn(),
  caseListMock: vi.fn(),
  caseDetailMock: vi.fn(),
  patientProfileMock: vi.fn(),
  routerPushMock: vi.fn(),
  routerBackMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: routerPushMock, back: routerBackMock }),
}));

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCreateCase: () => ({
    mutate: createMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
  useCaseList: () => caseListMock(),
  useCaseDetail: (id: string) => caseDetailMock(id),
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
  usePatientProfile: (id: string) => patientProfileMock(id),
}));

function signInAs(role: "DOCTOR" | "NURSE", userId: string, fullName: string) {
  useAuthStore.getState().signIn("access-token", "refresh-token", {
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
    caseListMock.mockReset();
    caseDetailMock.mockReset();
    patientProfileMock.mockReset();
    routerPushMock.mockReset();
    routerBackMock.mockReset();

    doctorListMock.mockReturnValue({
      data: [
        { userId: "doctor-1", fullName: "BS. Nguyễn Văn An" },
        { userId: "doctor-2", fullName: "BS. Lê Minh Hoàng" },
      ],
      isLoading: false,
      isError: false,
      error: null,
    });
    caseListMock.mockReturnValue({
      data: { items: [] },
      isLoading: false,
    });
    caseDetailMock.mockReturnValue({
      data: null,
      isLoading: false,
    });
    patientProfileMock.mockReturnValue({
      data: null,
      isLoading: false,
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

  it("áp dụng layout 2 cột trải rộng max-w-screen-2xl và chia tỉ lệ 5/7", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    const { container } = render(<CreateCaseForm patientProfileId="profile-1" />);

    // Outer container
    const outerContainer = container.querySelector(".max-w-screen-2xl");
    expect(outerContainer).toBeInTheDocument();
    expect(outerContainer).toHaveClass("mx-auto", "w-full", "px-6", "py-8");

    // Two-column grid
    const grid = container.querySelector(".grid.grid-cols-1.lg\\:grid-cols-12");
    expect(grid).toBeInTheDocument();
    expect(grid).toHaveClass("gap-6");

    // Left column (5 cols) & Right column (7 cols)
    const leftCol = container.querySelector(".lg\\:col-span-5");
    const rightCol = container.querySelector(".lg\\:col-span-7");
    expect(leftCol).toBeInTheDocument();
    expect(rightCol).toBeInTheDocument();
  });

  it("hiển thị thông tin hồ sơ bệnh nhân ở cột trái với badge cảnh báo dị ứng & bệnh nền", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    patientProfileMock.mockReturnValue({
      data: {
        patientProfileId: "profile-1",
        fullName: "Trần Thị Mai",
        dateOfBirth: "1995-05-20",
        allergies: [
          { allergyTypeId: "a1", allergyName: "Penicillin", isOther: false, note: null },
        ],
        diseases: [
          { diseaseId: "d1", diseaseName: "Tiểu đường thai kỳ", isOther: false, note: null },
        ],
      },
      isLoading: false,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByText("Bệnh nhân: Trần Thị Mai")).toBeInTheDocument();
    expect(screen.getByText("Penicillin")).toBeInTheDocument();
    expect(screen.getByText("Tiểu đường thai kỳ")).toBeInTheDocument();
  });

  it("hiển thị tóm tắt lần khám trước khi bệnh nhân đã có lịch sử khám", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    caseListMock.mockReturnValue({
      data: { items: [{ caseId: "case-prev-1" }] },
      isLoading: false,
    });
    caseDetailMock.mockReturnValue({
      data: {
        caseId: "case-prev-1",
        visitDate: "2026-08-01T08:00:00Z",
        finalDiagnosis: "Viêm âm đạo do nấm",
        doctorConclusion: "Kê đơn đặt thuốc 7 ngày",
        symptoms: [
          { categoryId: "c1", categoryName: "Khí hư", symptomId: "s1", symptomName: "Ngứa rát", otherNote: null },
        ],
      },
      isLoading: false,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByText(/Nội dung lần khám gần nhất/i)).toBeInTheDocument();
    expect(screen.getByText("Viêm âm đạo do nấm")).toBeInTheDocument();
    expect(screen.getByText("Kê đơn đặt thuốc 7 ngày")).toBeInTheDocument();
    expect(screen.getByText(/Ngứa rát/i)).toBeInTheDocument();
  });

  it("nút Huỷ bỏ gọi router.back()", async () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    const user = userEvent.setup();

    render(<CreateCaseForm patientProfileId="profile-1" />);

    await user.click(screen.getByRole("button", { name: /huỷ bỏ/i }));
    expect(routerBackMock).toHaveBeenCalledTimes(1);
  });
});
