import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { CreateCaseForm } from "@/features/medical-record/components/create-case-form";

const {
  createMutate,
  createCaseMock,
  doctorListMock,
  caseListMock,
  caseDetailMock,
  patientProfileMock,
  routerPushMock,
  routerBackMock,
} = vi.hoisted(() => ({
  createMutate: vi.fn(),
  createCaseMock: vi.fn(),
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
  useCreateCase: () => createCaseMock(),
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

function signInAs(role: "DOCTOR" | "STAFF", userId: string, fullName: string) {
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
    createCaseMock.mockReset();
    doctorListMock.mockReset();
    caseListMock.mockReset();
    caseDetailMock.mockReset();
    patientProfileMock.mockReset();
    routerPushMock.mockReset();
    routerBackMock.mockReset();

    createCaseMock.mockReturnValue({
      mutate: createMutate,
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    });

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
    signInAs("STAFF", "nurse-1", "Điều dưỡng");

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByRole("combobox")).toHaveValue("");
    expect(doctorListMock).toHaveBeenCalledWith(true);
  });

  it("chặn lưu khi Điều dưỡng chưa chọn bác sĩ", async () => {
    signInAs("STAFF", "nurse-1", "Điều dưỡng");
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
    signInAs("STAFF", "nurse-1", "Điều dưỡng");
    const user = userEvent.setup();

    render(<CreateCaseForm patientProfileId="profile-1" />);

    await user.selectOptions(screen.getByRole("combobox"), "doctor-2");
    await user.click(screen.getByRole("button", { name: /lưu ca khám/i }));

    expect(createMutate).toHaveBeenCalledWith(
      expect.objectContaining({ responsibleDoctorId: "doctor-2" }),
      expect.anything(),
    );
  });

  it("áp dụng layout chia đôi 2 cột với header ở trên và các hộp viền đen", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    const { container } = render(<CreateCaseForm patientProfileId="profile-1" />);

    // Outer container
    const outerContainer = container.querySelector(".max-w-\\[90\\%\\]");
    expect(outerContainer).toBeInTheDocument();
    expect(outerContainer).toHaveClass("mx-auto", "w-[90%]", "max-w-[90%]", "py-8");

    // Grid chia đôi 2 cột
    const grid = container.querySelector(".grid.grid-cols-1.lg\\:grid-cols-2");
    expect(grid).toBeInTheDocument();

    // Các hộp có viền đen
    const blackBorderBoxes = container.querySelectorAll(".border-gray-300, .border-border");
    expect(blackBorderBoxes.length).toBeGreaterThanOrEqual(2);
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

  it("an toàn không văng lỗi khi fullName chỉ có khoảng trắng và hiển thị initials mặc định PT", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    patientProfileMock.mockReturnValue({
      data: {
        patientProfileId: "profile-1",
        fullName: "   ",
        dateOfBirth: "1990-01-01",
        allergies: [],
        diseases: [],
      },
      isLoading: false,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByText("PT")).toBeInTheDocument();
    expect(screen.getByText("Bệnh nhân: Chưa cập nhật")).toBeInTheDocument();
  });

  it("định dạng ngày sinh an toàn không bị lệch múi giờ (formatIsoDate)", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    patientProfileMock.mockReturnValue({
      data: {
        patientProfileId: "profile-1",
        fullName: "Nguyễn Thị Hoa",
        dateOfBirth: "1995-12-31",
        allergies: [],
        diseases: [],
      },
      isLoading: false,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByText("Ngày sinh: 31/12/1995")).toBeInTheDocument();
  });

  it("hiển thị cảnh báo khi không tải được hồ sơ bệnh nhân (isError)", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    patientProfileMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: true,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByRole("alert")).toHaveTextContent("Không tải được thông tin bệnh nhân");
  });

  it("hiển thị placeholder khi lần khám trước không có ghi chú lâm sàng", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    caseListMock.mockReturnValue({
      data: { items: [{ caseId: "case-prev-empty" }] },
      isLoading: false,
    });
    caseDetailMock.mockReturnValue({
      data: {
        caseId: "case-prev-empty",
        visitDate: "2026-08-01",
        finalDiagnosis: null,
        doctorConclusion: null,
        symptoms: [],
      },
      isLoading: false,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(
      screen.getByText("Không có ghi chú lâm sàng từ lần khám trước."),
    ).toBeInTheDocument();
  });

  it("hiển thị lỗi từ mutation khi lưu ca khám thất bại", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    createCaseMock.mockReturnValue({
      mutate: createMutate,
      isPending: false,
      isSuccess: false,
      isError: true,
      error: new Error("Network error"),
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    expect(screen.getByRole("alert")).toHaveTextContent("Tạo ca khám thất bại.");
  });

  it("badge dị ứng và tiền sử bệnh nền có class cho phép xuống dòng khi ghi chú dài", () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    patientProfileMock.mockReturnValue({
      data: {
        patientProfileId: "profile-1",
        fullName: "Lê Văn C",
        dateOfBirth: "1988-03-15",
        allergies: [
          {
            allergyTypeId: "a-long",
            allergyName: "Khác",
            isOther: true,
            note: "Dị ứng cực kỳ nghiêm trọng với kháng sinh nhóm cephalosporin thế hệ 3",
          },
        ],
        diseases: [
          {
            diseaseId: "d-long",
            diseaseName: "Cao huyết áp",
            isOther: false,
            note: "Điều trị thuốc kiểm soát huyết áp hàng ngày kèm chế độ ăn giảm muối",
          },
        ],
      },
      isLoading: false,
    });

    render(<CreateCaseForm patientProfileId="profile-1" />);

    const allergyBadge = screen.getByText(
      /Dị ứng cực kỳ nghiêm trọng với kháng sinh nhóm cephalosporin thế hệ 3/,
    );
    expect(allergyBadge).toBeInTheDocument();
    expect(allergyBadge).toHaveClass("whitespace-normal", "break-words");

    const diseaseBadge = screen.getByText(
      /Cao huyết áp: Điều trị thuốc kiểm soát huyết áp hàng ngày kèm chế độ ăn giảm muối/,
    );
    expect(diseaseBadge).toBeInTheDocument();
    expect(diseaseBadge).toHaveClass("whitespace-normal", "break-words");
  });

  it("nút dùng lại triệu chứng gom các triệu chứng cùng categoryId lại liền kề nhau", async () => {
    signInAs("DOCTOR", "doctor-1", "BS. Nguyễn Văn An");
    caseListMock.mockReturnValue({
      data: { items: [{ caseId: "case-prev-dup" }] },
      isLoading: false,
    });
    caseDetailMock.mockReturnValue({
      data: {
        caseId: "case-prev-dup",
        visitDate: "2026-08-01",
        finalDiagnosis: "Viêm",
        doctorConclusion: "Thuốc",
        symptoms: [
          { categoryId: "c1", categoryName: "Khí hư", symptomId: "s1", symptomName: "Ra nhiều", otherNote: null },
          { categoryId: "c2", categoryName: "Xuất huyết", symptomId: "s2", symptomName: "Rong kinh", otherNote: null },
          { categoryId: "c1", categoryName: "Khí hư", symptomId: "s3", symptomName: "Màu xanh", otherNote: null },
        ],
      },
      isLoading: false,
    });

    const user = userEvent.setup();
    render(<CreateCaseForm patientProfileId="profile-1" />);

    const applyBtn = screen.getByRole("button", { name: /\+ dùng lại triệu chứng này/i });
    expect(applyBtn).toBeInTheDocument();
    await user.click(applyBtn);

    // Bấm lưu ca khám để kiểm tra payload gửi lên mutation
    await user.click(screen.getByRole("button", { name: /lưu ca khám/i }));

    expect(createMutate).toHaveBeenCalledWith(
      expect.objectContaining({
        symptoms: [
          expect.objectContaining({ categoryId: "c1", symptomId: "s1" }),
          expect.objectContaining({ categoryId: "c1", symptomId: "s3" }),
          expect.objectContaining({ categoryId: "c2", symptomId: "s2" }),
        ],
      }),
      expect.anything(),
    );
  });
});
