import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";

import { CaseDetailView } from "@/features/medical-record/components/case-detail-view";

const { detailMock, saveMutate, confirmMutate, endMutate, exportReportMock, pushMock, createFollowUpMutate, useScheduleSlotsMock } = vi.hoisted(() => ({
  detailMock: vi.fn(),
  saveMutate: vi.fn(),
  confirmMutate: vi.fn(),
  endMutate: vi.fn(),
  exportReportMock: vi.fn(),
  pushMock: vi.fn(),
  createFollowUpMutate: vi.fn(),
  useScheduleSlotsMock: vi.fn(),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-doctor-appointments", () => ({
  useCreateFollowUpAppointment: () => ({
    mutateAsync: createFollowUpMutate,
    isPending: false,
  })
}));

vi.mock("@/features/appointment-scheduling/hooks/use-schedule-slot", () => ({
  useScheduleSlots: useScheduleSlotsMock,
}));

let isSaveSuccess = false;

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCaseDetail: () => detailMock(),
  useSaveCaseConclusion: () => ({
    // Component gọi mutate(input, { onSuccess }) — mock phải TỰ gọi onSuccess như react-query
    // thật thì mới kiểm được hành vi khoá tạm sau khi lưu (isLocked chuyển true trong callback đó).
    mutate: (
      input: unknown,
      options?: { onSuccess?: () => void },
    ) => {
      isSaveSuccess = true;
      saveMutate(input);
      options?.onSuccess?.();
    },
    isPending: false,
    get isSuccess() {
      return isSaveSuccess;
    },
    isError: false,
    error: null,
  }),
  useConfirmCase: () => ({
    mutate: confirmMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
  useEndCaseWithoutPrescription: () => ({
    mutate: (
      input: unknown,
      options?: { onSuccess?: () => void },
    ) => {
      endMutate(input);
      options?.onSuccess?.();
    },
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

let isReportPending = false;
let reportErrorMock: unknown = null;

vi.mock("@/features/medical-record/hooks/use-case-report", () => ({
  useExportCaseReport: () => ({
    exportReport: exportReportMock,
    get isPending() {
      return isReportPending;
    },
    get error() {
      return reportErrorMock;
    },
  }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock("@/features/prescriptions/components/prescription-section", () => ({
  PrescriptionSection: () => <div data-testid="prescription-section" />,
}));

vi.mock("@/features/prescription-adherence/hooks/use-invoices", () => ({
  useCaseInvoices: () => ({ data: [], isLoading: false }),
}));

function makeCase(
  status: "BOOKED" | "IN_PROGRESS" | "CONFIRMED" | "END" | "CANCELLED",
  draft?: { finalDiagnosis: string; doctorConclusion: string },
) {
  return {
    data: {
      caseId: "case-1",
      patientProfileId: "profile-1",
      doctorId: "doctor-1",
      doctorName: "BS. Nguyễn Văn An",
      visitDate: "2026-07-22",
      clinicalInfo: "Rong kinh 3 tuần",
      status,
      finalDiagnosis: status === "CONFIRMED" ? "Nhân xơ tử cung" : (draft?.finalDiagnosis ?? null),
      doctorConclusion:
        status === "CONFIRMED" ? "Theo dõi 3 tháng" : (draft?.doctorConclusion ?? null),
      patientProfile: null,
      ultrasoundImages: [
        {
          imageId: "img-1",
          caseId: "case-1",
          imageUrl: "https://example.test/a.png",
          uploadedAt: "2026-07-22T09:05:00Z",
          note: null,
        },
        {
          imageId: "img-2",
          caseId: "case-1",
          imageUrl: null,
          uploadedAt: "2026-07-22T09:12:00Z",
          note: "Bổ sung sau",
        },
      ],
      aiResults: [{ aiResultId: "ai-1", status: "PENDING", findingCount: 3 }],
      prescription: { prescriptionId: "rx-1", status: "ACTIVE" },
      createdAt: "2026-07-22T09:05:00Z",
      updatedAt: "2026-07-22T09:12:00Z",
    },
    isLoading: false,
    isError: false,
    error: null,
  };
}

function signInAs(role: "DOCTOR" | "STAFF", userId: string) {
  useAuthStore.getState().signIn("access-token", "refresh-token", {
    userId,
    fullName: "Người dùng",
    email: null,
    role,
    mustChangePassword: false,
  });
}

describe("CaseDetailView", () => {
  beforeEach(() => {
    isSaveSuccess = false;
    isReportPending = false;
    reportErrorMock = null;
    URL.createObjectURL = vi.fn(() => "blob:fake");
    detailMock.mockReset();
    saveMutate.mockReset();
    confirmMutate.mockReset();
    endMutate.mockReset();
    exportReportMock.mockReset();
    pushMock.mockReset();
    createFollowUpMutate.mockReset();
    useScheduleSlotsMock.mockReset();
    useScheduleSlotsMock.mockReturnValue({ data: undefined, isLoading: false });
    useAuthStore.getState().signOut();
    useDiagnosticStore.getState().clearSession();
  });

  it("không hiển thị nút xuất PDF khi ca chưa kết thúc", () => {
    // UC-12 BR-01 — chỉ xuất được báo cáo của ca đã END.
    detailMock.mockReturnValue(makeCase("CONFIRMED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByRole("button", { name: /xuất báo cáo pdf/i })).not.toBeInTheDocument();
  });

  it("bật nút xuất PDF khi ca đã kết thúc", () => {
    detailMock.mockReturnValue(makeCase("END"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByRole("button", { name: /xuất báo cáo pdf/i })).toBeEnabled();
  });

  it("tắt nút bổ sung ảnh khi ca đã kết luận", () => {
    // GB-01 — ca đã chốt không nhận thêm dữ liệu đầu vào.
    // Nút chỉ render cho đúng Bác sĩ phụ trách ca này (isResponsibleDoctor).
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("CONFIRMED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByRole("button", { name: /bổ sung ảnh/i })).toBeDisabled();
  });

  it("cho bổ sung ảnh khi ca chưa kết luận", () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByRole("button", { name: /bổ sung ảnh/i })).toBeEnabled();
  });

  it("hiện ô hỏng cho ảnh không ký được URL", () => {
    // Flag F5 — imageUrl null khi Storage ký URL thất bại.
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByText(/không tải được ảnh/i)).toBeInTheDocument();
  });

  it("hiện hai trường kết luận tách rời khi ca đã kết luận", () => {
    detailMock.mockReturnValue(makeCase("CONFIRMED"));

    render(<CaseDetailView caseId="case-1" />);

    // DTO thật tách finalDiagnosis và doctorConclusion, không phải một trường `conclusion`
    // như API Spec v0.1 mô tả.
    expect(screen.getByText("Nhân xơ tử cung")).toBeInTheDocument();
    expect(screen.getByText("Theo dõi 3 tháng")).toBeInTheDocument();
  });

  it("không render khối AI hay đơn thuốc dù payload có trả về", () => {
    detailMock.mockReturnValue(makeCase("CONFIRMED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByText(/phát hiện/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/đơn thuốc/i)).not.toBeInTheDocument();
  });

  it("không hiện id ca khám dạng UUID thô trên màn hình", () => {
    // Sửa 07/08/2026 — cùng lý do đã bỏ UUID thô ở SCR-12 (Task C12): không có ích cho người đọc.
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByText("case-1")).not.toBeInTheDocument();
  });

  // ---------- Kết luận: Lưu và Kết thúc ca khám (thêm/sửa 07/08/2026) ----------

  it("hiện form nhập kết luận cho đúng Bác sĩ phụ trách ca này khi chưa CONFIRMED", () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByLabelText(/chẩn đoán cuối cùng/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^lưu kết luận$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /xác nhận kết luận/i })).toBeInTheDocument();
  });

  it("KHÔNG hiện form cho Bác sĩ khác (không phải người phụ trách ca này)", () => {
    // GB-04 — chỉ đúng bác sĩ phụ trách CA NÀY (doctorId "doctor-1"), không phải bác sĩ bất kỳ.
    signInAs("DOCTOR", "doctor-2");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByLabelText(/chẩn đoán cuối cùng/i)).not.toBeInTheDocument();
    expect(screen.getByText(/ca khám chưa được kết luận/i)).toBeInTheDocument();
  });

  it("KHÔNG hiện form cho Điều dưỡng dù ca chưa CONFIRMED", () => {
    signInAs("STAFF", "nurse-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByLabelText(/chẩn đoán cuối cùng/i)).not.toBeInTheDocument();
  });

  it("đổ sẵn kết luận đã lưu nháp trước đó vào form (chưa CONFIRMED)", () => {
    // Ca đã có finalDiagnosis/doctorConclusion (từ lần "Lưu kết luận" trước) nhưng status vẫn
    // chưa CONFIRMED — form phải hiện lại đúng nội dung đó, không trống.
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(
      makeCase("IN_PROGRESS", {
        finalDiagnosis: "Nghi u lành",
        doctorConclusion: "Chờ thêm ảnh siêu âm",
      }),
    );

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByLabelText(/chẩn đoán cuối cùng/i)).toHaveValue("Nghi u lành");
    expect(screen.getByLabelText(/kết luận \/ hướng xử trí/i)).toHaveValue("Chờ thêm ảnh siêu âm");
  });

  it("chặn Lưu kết luận khi bỏ trống chẩn đoán hoặc kết luận", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.click(screen.getByRole("button", { name: /^lưu kết luận$/i }));

    expect(saveMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/chẩn đoán và kết luận/i);
  });

  it("chặn Kết thúc ca khám khi bỏ trống chẩn đoán hoặc kết luận", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.click(screen.getByRole("button", { name: /xác nhận kết luận/i }));

    expect(confirmMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/chẩn đoán và kết luận/i);
  });

  it("Lưu kết luận gọi đúng hàm lưu (không đổi trạng thái), không gọi hàm kết thúc", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.type(screen.getByLabelText(/chẩn đoán cuối cùng/i), "Nhân xơ tử cung");
    await user.type(screen.getByLabelText(/kết luận \/ hướng xử trí/i), "Theo dõi 6 tháng");
    await user.click(screen.getByRole("button", { name: /^lưu kết luận$/i }));

    expect(saveMutate).toHaveBeenCalledWith({
      finalDiagnosis: "Nhân xơ tử cung",
      doctorConclusion: "Theo dõi 6 tháng",
    });
    expect(confirmMutate).not.toHaveBeenCalled();
  });

  it("Kết thúc ca khám gọi đúng hàm khoá ca, không gọi hàm lưu", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.type(screen.getByLabelText(/chẩn đoán cuối cùng/i), "Nhân xơ tử cung");
    await user.type(screen.getByLabelText(/kết luận \/ hướng xử trí/i), "Theo dõi 6 tháng");
    await user.click(screen.getByRole("button", { name: /xác nhận kết luận/i }));

    expect(confirmMutate).toHaveBeenCalledWith({
      finalDiagnosis: "Nhân xơ tử cung",
      doctorConclusion: "Theo dõi 6 tháng",
    });
    expect(saveMutate).not.toHaveBeenCalled();
  });

  // ---------- Khoá tạm sau "Lưu kết luận" (thêm 07/08/2026) ----------

  it("khoá 2 trường và nút Bổ sung ảnh siêu âm ngay sau khi Lưu kết luận thành công", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.type(screen.getByLabelText(/chẩn đoán cuối cùng/i), "Nhân xơ tử cung");
    await user.type(screen.getByLabelText(/kết luận \/ hướng xử trí/i), "Theo dõi 6 tháng");
    await user.click(screen.getByRole("button", { name: /^lưu kết luận$/i }));

    expect(screen.getByLabelText(/chẩn đoán cuối cùng/i)).toBeDisabled();
    expect(screen.getByLabelText(/kết luận \/ hướng xử trí/i)).toBeDisabled();
    expect(screen.getByRole("button", { name: /bổ sung ảnh/i })).toBeDisabled();
    expect(screen.getByRole("button", { name: /^sửa$/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^lưu kết luận$/i })).not.toBeInTheDocument();
  });

  it("bấm Sửa mở khoá lại 2 trường và nút Bổ sung ảnh siêu âm", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.type(screen.getByLabelText(/chẩn đoán cuối cùng/i), "Nhân xơ tử cung");
    await user.type(screen.getByLabelText(/kết luận \/ hướng xử trí/i), "Theo dõi 6 tháng");
    await user.click(screen.getByRole("button", { name: /^lưu kết luận$/i }));
    await user.click(screen.getByRole("button", { name: /^sửa$/i }));

    expect(screen.getByLabelText(/chẩn đoán cuối cùng/i)).toBeEnabled();
    expect(screen.getByLabelText(/kết luận \/ hướng xử trí/i)).toBeEnabled();
    expect(screen.getByRole("button", { name: /bổ sung ảnh/i })).toBeEnabled();
    expect(screen.getByRole("button", { name: /^lưu kết luận$/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^sửa$/i })).not.toBeInTheDocument();
  });

  it("vẫn bấm được Kết thúc ca khám ngay cả khi đang khoá tạm sau Lưu kết luận", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.type(screen.getByLabelText(/chẩn đoán cuối cùng/i), "Nhân xơ tử cung");
    await user.type(screen.getByLabelText(/kết luận \/ hướng xử trí/i), "Theo dõi 6 tháng");
    await user.click(screen.getByRole("button", { name: /^lưu kết luận$/i }));
    await user.click(screen.getByRole("button", { name: /xác nhận kết luận/i }));

    expect(confirmMutate).toHaveBeenCalledWith({
      finalDiagnosis: "Nhân xơ tử cung",
      doctorConclusion: "Theo dõi 6 tháng",
    });
  });

  // ---------- Requirement R2: Typography & Bounding Boxes (Milestone 2) ----------

  it("áp dụng viền phân vùng (bounding boxes) rõ ràng và chữ đậm tối màu cho các khối thông tin", () => {
    const caseWithProfile = {
      ...makeCase("IN_PROGRESS"),
      data: {
        ...makeCase("IN_PROGRESS").data,
        patientProfile: {
          patientProfileId: "profile-1",
          patientUserId: "user-1",
          fullName: "Trần Thị Hoa",
          phone: "0912345678",
          dateOfBirth: "1990-05-15",
          gender: "FEMALE" as const,
          diseases: [{ diseaseId: "d1", diseaseName: "Viêm dạ dày", isOther: false, note: null }],
          allergies: [{ allergyTypeId: "a1", allergyName: "Penicillin", isOther: false, note: null }],
          createdBy: "user-admin",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        },
        symptoms: [
          {
            categoryId: "c1",
            categoryName: "Triệu chứng chung",
            symptomId: "s1",
            symptomName: "Đau bụng dưới",
            otherNote: null,
          },
        ],
      },
    };
    detailMock.mockReturnValue(caseWithProfile);

    const { container } = render(<CaseDetailView caseId="case-1" />);

    // 1. Kiểm tra tiêu đề chính và các heading có font-bold và text-foreground
    const mainHeading = screen.getByRole("heading", { name: /lần khám ngày/i, level: 1 });
    expect(mainHeading).toHaveClass("font-bold");
    expect(mainHeading).toHaveClass("text-foreground");
    expect(mainHeading).not.toHaveClass("text-muted-foreground");

    const clinicalHeading = screen.getByRole("heading", { name: /thông tin lâm sàng/i, level: 2 });
    expect(clinicalHeading).toHaveClass("font-bold");
    expect(clinicalHeading).toHaveClass("text-foreground");
    expect(clinicalHeading).not.toHaveClass("text-muted-foreground");

    const ultrasoundHeading = screen.getByRole("heading", { name: /ảnh siêu âm/i, level: 2 });
    expect(ultrasoundHeading).toHaveClass("font-bold");
    expect(ultrasoundHeading).toHaveClass("text-foreground");
    expect(ultrasoundHeading).not.toHaveClass("text-muted-foreground");

    const patientHeading = screen.getByRole("heading", { name: /bệnh nhân:/i, level: 3 });
    expect(patientHeading).toHaveClass("font-bold");
    expect(patientHeading).toHaveClass("text-foreground");
    expect(patientHeading).not.toHaveClass("text-muted-foreground");

    // 2. Kiểm tra Bounding Boxes: Các section và header có viền rõ ràng (border-black)
    const sections = container.querySelectorAll("section");
    expect(sections.length).toBeGreaterThanOrEqual(3);
    sections.forEach((section) => {
      expect(section.className).toMatch(/border-(gray-300|border)/);
    });

    const header = container.querySelector("header");
    expect(header).toBeInTheDocument();
    expect(header?.className).toMatch(/border-gray-300/);
  });

  // ---------- Milestone 3: Interactive Actions, Prescription & Conclusion Enhancements ----------

  it("khi bấm nút Xuất báo cáo PDF ở trạng thái END thì gọi hàm exportReport", async () => {
    detailMock.mockReturnValue(makeCase("END"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);

    const exportButton = screen.getByRole("button", { name: /xuất báo cáo pdf/i });
    expect(exportButton).toBeEnabled();
    expect(exportButton).toHaveClass("font-bold", "text-primary-foreground");

    await user.click(exportButton);
    expect(exportReportMock).toHaveBeenCalledTimes(1);
  });

  it("khi ca ở trạng thái END thì hiển thị PrescriptionSection", () => {
    detailMock.mockReturnValue(makeCase("END"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByTestId("prescription-section")).toBeInTheDocument();
  });

  it("khi Bác sĩ bấm Kết thúc ca bệnh thì mở Dialog xác nhận và gọi endCaseWithoutPrescription khi xác nhận", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("CONFIRMED"));
    const user = userEvent.setup();

    const { container } = render(<CaseDetailView caseId="case-1" />);

    const endCaseBtn = screen.getByRole("button", { name: /kết thúc ca bệnh/i });
    expect(endCaseBtn).toBeInTheDocument();
    expect(endCaseBtn).toHaveClass("font-bold", "text-primary-foreground");

    await user.click(endCaseBtn);

    // Dialog mở ra
    const dialogTitle = screen.getByRole("heading", { name: /xác nhận kết thúc ca bệnh/i });
    expect(dialogTitle).toBeInTheDocument();
    expect(dialogTitle).toHaveClass("font-bold", "text-foreground");

    const dialogDescription = screen.getByText(/chắc chắn muốn kết thúc ca bệnh mà không có đơn thuốc/i);
    expect(dialogDescription).toBeInTheDocument();
    expect(dialogDescription).toHaveClass("font-bold", "text-foreground");

    // Dialog bounding box check
    const dialogContent = container.ownerDocument.querySelector("[data-slot='dialog-content']");
    expect(dialogContent?.className).toMatch(/border-gray-300/);

    // Bấm nút Kết thúc trong dialog
    const confirmBtn = screen.getByRole("button", { name: /^kết thúc$/i });
    expect(confirmBtn).toHaveClass("font-bold", "text-primary-foreground");
    await user.click(confirmBtn);

    expect(endMutate).toHaveBeenCalledTimes(1);
  });

  it("bác sĩ mở form Bổ sung ảnh siêu âm và gửi ảnh qua Xem kết quả AI sẽ lưu session vào Zustand store và chuyển trang /diagnostic", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);

    const addImageBtn = screen.getByRole("button", { name: /bổ sung ảnh siêu âm/i });
    await user.click(addImageBtn);

    // Form upload xuất hiện
    expect(screen.getByLabelText(/ghi chú cho lô ảnh này/i)).toBeInTheDocument();
    const submitBtn = screen.getByRole("button", { name: /xem kết quả ai/i });
    expect(submitBtn).toBeDisabled();

    // Giả lập chọn file ảnh
    const file = new File(["fake-image"], "test-ultrasound.png", { type: "image/png" });
    const fileInput = screen.getByLabelText(/chọn ảnh siêu âm/i);
    await user.upload(fileInput, file);

    expect(submitBtn).toBeEnabled();
    expect(submitBtn).toHaveClass("font-bold");

    await user.click(submitBtn);

    expect(useDiagnosticStore.getState().caseId).toBe("case-1");
    expect(useDiagnosticStore.getState().images.length).toBe(1);
    expect(pushMock).toHaveBeenCalledWith("/cases/case-1/diagnostic");
  });

  it("áp dụng Bounding Box và chữ đậm tối màu cho phần Kết luận của bác sĩ (Milestone 3)", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);

    // 1. Tiêu đề Kết luận của bác sĩ
    const conclusionHeading = screen.getByRole("heading", { name: /kết luận của bác sĩ/i, level: 2 });
    expect(conclusionHeading).toBeInTheDocument();
    expect(conclusionHeading).toHaveClass("font-heading", "font-bold", "text-foreground");
    expect(conclusionHeading).not.toHaveClass("text-muted-foreground");

    // 2. Section Bounding Box
    const conclusionSection = conclusionHeading.closest("section");
    expect(conclusionSection?.className).toMatch(/border-gray-300/);

    // 3. Form labels và textareas bounding box
    const diagLabel = screen.getByText(/chẩn đoán cuối cùng \*/i);
    expect(diagLabel).toHaveClass("font-bold", "text-foreground");

    const diagInput = screen.getByLabelText(/chẩn đoán cuối cùng/i);
    expect(diagInput.className).toMatch(/border-gray-300/);

    const concLabel = screen.getByText(/kết luận \/ hướng xử trí \*/i);
    expect(concLabel).toHaveClass("font-bold", "text-foreground");

    const concInput = screen.getByLabelText(/kết luận \/ hướng xử trí/i);
    expect(concInput.className).toMatch(/border-gray-300/);

    // 4. Action buttons: font-bold text-foreground / text-primary-foreground
    const saveBtn = screen.getByRole("button", { name: /^lưu kết luận$/i });
    expect(saveBtn).toHaveClass("font-bold", "text-foreground");
    expect(saveBtn.className).toMatch(/border-gray-300/);

    const confirmBtn = screen.getByRole("button", { name: /xác nhận kết luận/i });
    expect(confirmBtn).toHaveClass("font-bold", "text-primary-foreground");

    // 5. Sau khi lưu thành công: trạng thái khoá và nút Sửa
    await user.type(diagInput, "Nhân xơ tử cung");
    await user.type(concInput, "Theo dõi 6 tháng");
    await user.click(saveBtn);

    const statusBanner = screen.getByRole("status");
    expect(statusBanner).toBeInTheDocument();
    expect(statusBanner).toHaveClass("font-bold");

    const editBtn = screen.getByRole("button", { name: /^sửa$/i });
    expect(editBtn).toHaveClass("font-bold", "text-foreground");
    expect(editBtn.className).toMatch(/border-gray-300/);
  });

  // ---------- Requirement R1: Bố cục 90% chiều rộng màn hình (Layout & Width) ----------

  it("giao diện chính bao phủ 90% chiều rộng màn hình với max-w-[90%] và w-[90%] mx-auto", () => {
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    const { container } = render(<CaseDetailView caseId="case-1" />);

    const rootWrapper = container.firstElementChild;
    expect(rootWrapper).toBeInTheDocument();
    expect(rootWrapper).toHaveClass("mx-auto", "w-[90%]", "max-w-[90%]");
  });

  it("trạng thái đang tải (isLoading) giữ nguyên bố cục w-[90%] max-w-[90%] mx-auto và có bounding box border-black", () => {
    detailMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    });

    const { container } = render(<CaseDetailView caseId="case-1" />);

    const rootWrapper = container.firstElementChild;
    expect(rootWrapper).toBeInTheDocument();
    expect(rootWrapper).toHaveClass("mx-auto", "w-[90%]", "max-w-[90%]");

    const loadingCard = container.querySelector("div.rounded-xl");
    expect(loadingCard).toBeInTheDocument();
    expect(loadingCard?.className).toMatch(/border-gray-300/);

    const loadingText = screen.getByText(/đang tải ca khám\.\.\./i);
    expect(loadingText).toBeInTheDocument();
    expect(loadingText).toHaveClass("font-bold", "text-foreground");
    expect(loadingText).not.toHaveClass("text-muted-foreground");
  });

  it("trạng thái lỗi (isError) giữ nguyên bố cục w-[90%] max-w-[90%] mx-auto", () => {
    detailMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error("Lỗi kết nối máy chủ"),
    });

    const { container } = render(<CaseDetailView caseId="case-1" />);

    const rootWrapper = container.firstElementChild;
    expect(rootWrapper).toBeInTheDocument();
    expect(rootWrapper).toHaveClass("mx-auto", "w-[90%]", "max-w-[90%]");

    const errorAlert = screen.getByRole("alert");
    expect(errorAlert).toBeInTheDocument();
    expect(errorAlert).toHaveClass("font-bold", "text-destructive");
  });

  it("lưới làm việc chính chia 2 cột responsive (grid lg:grid-cols-[1.7fr_1fr]) theo Phase 4", () => {
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    const { container } = render(<CaseDetailView caseId="case-1" />);

    const workspaceGrid = container.querySelector("div.lg\\:grid-cols-\\[1\\.7fr_1fr\\]");
    expect(workspaceGrid).toBeInTheDocument();
    expect(workspaceGrid).toHaveClass("grid", "grid-cols-1", "gap-5");
  });

  // ---------- Requirement R2: Typography & Phân vùng (Bounding Boxes) ----------

  it("tất cả tiêu đề (h1-h4, dt, label) tuyệt đối không chứa class text-muted-foreground", () => {
    const caseWithDetails = {
      ...makeCase("IN_PROGRESS"),
      data: {
        ...makeCase("IN_PROGRESS").data,
        patientProfile: {
          patientProfileId: "profile-1",
          patientUserId: "user-1",
          fullName: "Nguyễn Thị Mai",
          phone: "0901234567",
          dateOfBirth: "1988-10-20",
          gender: "FEMALE" as const,
          diseases: [{ diseaseId: "d1", diseaseName: "Tiểu đường", isOther: false, note: null }],
          allergies: [{ allergyTypeId: "a1", allergyName: "Aspirin", isOther: false, note: null }],
          createdBy: "user-admin",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        },
        symptoms: [
          {
            categoryId: "c1",
            categoryName: "Triệu chứng phụ khoa",
            symptomId: "s1",
            symptomName: "Đau hạ vị",
            otherNote: null,
          },
        ],
      },
    };
    detailMock.mockReturnValue(caseWithDetails);
    signInAs("DOCTOR", "doctor-1");

    const { container } = render(<CaseDetailView caseId="case-1" />);

    // Kiểm tra toàn bộ các thẻ tiêu đề và nhãn form
    const headingAndLabels = container.querySelectorAll("h1, h2, h3, h4, dt, label");
    expect(headingAndLabels.length).toBeGreaterThanOrEqual(8);

    headingAndLabels.forEach((el) => {
      expect(el).not.toHaveClass("text-muted-foreground");
    });
  });

  it("link điều hướng danh sách ca khám và subtitle bác sĩ dùng font đậm text-foreground", () => {
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    const navLink = screen.getByRole("link", { name: /danh sách ca khám/i });
    expect(navLink).toBeInTheDocument();
    expect(navLink).toHaveClass("font-bold", "text-foreground");
    expect(navLink).not.toHaveClass("text-muted-foreground");

    const doctorSubtitle = screen.getByText(/bác sĩ phụ trách:/i);
    expect(doctorSubtitle).toHaveClass("font-semibold", "text-foreground");
    expect(doctorSubtitle).not.toHaveClass("text-muted-foreground");

    const doctorName = screen.getByText("BS. Nguyễn Văn An");
    expect(doctorName).toHaveClass("font-bold", "text-foreground");
  });

  it("hộp cảnh báo dành cho bác sĩ không phụ trách ca có viền đứt nét border-black/40 và chữ đậm text-foreground", () => {
    signInAs("DOCTOR", "doctor-different");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    const warningHeading = screen.getByText("Ca khám chưa được kết luận");
    expect(warningHeading).toBeInTheDocument();
    expect(warningHeading).toHaveClass("font-bold", "text-foreground");

    const warningDesc = screen.getByText(/chỉ bác sĩ phụ trách ca này mới chốt được kết luận/i);
    expect(warningDesc).toBeInTheDocument();
    expect(warningDesc).toHaveClass("font-bold", "text-foreground");

    const warningCard = warningHeading.closest("div");
    expect(warningCard?.className).toMatch(/border-border/);
  });

  // ---------- Acceptance Criteria: Interactive Actions & Edge Cases ----------

  it("bác sĩ bấm Hủy trong Dialog kết thúc ca bệnh thì đóng dialog và không gọi endCaseWithoutPrescription", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("CONFIRMED"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);

    const endCaseBtn = screen.getByRole("button", { name: /kết thúc ca bệnh/i });
    await user.click(endCaseBtn);

    // Dialog mở ra
    expect(screen.getByRole("heading", { name: /xác nhận kết thúc ca bệnh/i })).toBeInTheDocument();

    // Bấm Hủy
    const cancelBtn = screen.getByRole("button", { name: /^hủy$/i });
    expect(cancelBtn).toHaveClass("font-bold", "text-foreground");
    await user.click(cancelBtn);

    // Dialog đóng lại và mutation không được gọi
    expect(endMutate).not.toHaveBeenCalled();
  });

  it("ca ở trạng thái CONFIRMED không hiển thị nút Kê đơn thuốc và Kết thúc ca bệnh cho Bác sĩ khác hoặc Điều dưỡng", () => {
    detailMock.mockReturnValue(makeCase("CONFIRMED"));

    // Case 1: Điều dưỡng
    signInAs("STAFF", "nurse-1");
    const { unmount } = render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByRole("link", { name: /kê đơn thuốc/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /kết thúc ca bệnh/i })).not.toBeInTheDocument();
    unmount();

    // Case 2: Bác sĩ khác (không phải người phụ trách doctor-1)
    signInAs("DOCTOR", "doctor-2");
    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByRole("link", { name: /kê đơn thuốc/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /kết thúc ca bệnh/i })).not.toBeInTheDocument();
  });

  it("nút Xuất báo cáo PDF bị disabled và hiện 'Đang tạo file...' khi report.isPending là true", () => {
    detailMock.mockReturnValue(makeCase("END"));
    isReportPending = true;

    render(<CaseDetailView caseId="case-1" />);

    const exportBtn = screen.getByRole("button", { name: /đang tạo file\.\.\./i });
    expect(exportBtn).toBeInTheDocument();
    expect(exportBtn).toBeDisabled();
    expect(exportBtn).toHaveClass("font-bold");
  });

  it("hiển thị thông báo lỗi role='alert' khi exportReport phát sinh lỗi", () => {
    detailMock.mockReturnValue(makeCase("END"));
    reportErrorMock = new Error("Không thể kết nối dịch vụ xuất PDF");

    render(<CaseDetailView caseId="case-1" />);

    const errorBanner = screen.getByRole("alert");
    expect(errorBanner).toBeInTheDocument();
    expect(errorBanner).toHaveClass("font-bold", "text-destructive");
    expect(errorBanner.className).toMatch(/border-destructive/);
  });

  it("bác sĩ nhập ghi chú khi bổ sung ảnh siêu âm thì ghi chú được cập nhật trong form", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);

    const addImageBtn = screen.getByRole("button", { name: /bổ sung ảnh siêu âm/i });
    await user.click(addImageBtn);

    const noteInput = screen.getByLabelText(/ghi chú cho lô ảnh này/i);
    expect(noteInput).toBeInTheDocument();
    await user.type(noteInput, "Chụp góc nghiêng bên trái");
    expect(noteInput).toHaveValue("Chụp góc nghiêng bên trái");
  });

  it("hiển thị thông tin ca khám ở trạng thái BOOKED, hiển thị banner chờ check-in, khóa nút thêm ảnh và hiển thị thông báo chờ check-in ở phần kết luận", () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("BOOKED"));

    render(<CaseDetailView caseId="case-1" />);

    // 1. Banner trạng thái chờ check-in
    expect(screen.getByText(/ca khám đang ở trạng thái chờ check-in/i)).toBeInTheDocument();
    expect(screen.getByText(/bệnh nhân chưa được điều dưỡng tiếp đón check-in tại quầy/i)).toBeInTheDocument();

    // 2. Nút Bổ sung ảnh siêu âm bị disabled kèm thông báo
    const addImageBtn = screen.getByRole("button", { name: /bổ sung ảnh siêu âm/i });
    expect(addImageBtn).toBeDisabled();
    expect(screen.getByText(/chờ điều dưỡng check-in trước khi tải ảnh/i)).toBeInTheDocument();

    // 3. Khung kết luận hiển thị trạng thái chờ check-in thay vì form nhập
    expect(screen.getByText(/^ca khám đang chờ check-in$/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/chẩn đoán cuối cùng/i)).not.toBeInTheDocument();
  });

  it("hiển thị thông tin ca khám ở trạng thái CANCELLED, hiển thị banner đã huỷ, khóa nút thêm ảnh và hiển thị thông báo ca đã huỷ ở phần kết luận", () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("CANCELLED"));

    render(<CaseDetailView caseId="case-1" />);

    // 1. Banner trạng thái đã huỷ
    expect(screen.getByText(/ca khám đã huỷ \/ bệnh nhân vắng mặt/i)).toBeInTheDocument();

    // 2. Nút Bổ sung ảnh siêu âm bị disabled kèm thông báo
    const addImageBtn = screen.getByRole("button", { name: /bổ sung ảnh siêu âm/i });
    expect(addImageBtn).toBeDisabled();
    expect(screen.getByText(/ca đã huỷ không nhận thêm ảnh/i)).toBeInTheDocument();

    // 3. Khung kết luận hiển thị trạng thái đã huỷ
    expect(screen.getByText(/^ca khám đã hủy \/ vắng mặt$/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/chẩn đoán cuối cùng/i)).not.toBeInTheDocument();
  });

  it("bác sĩ khác (Bác sĩ B) không có quyền thêm hoặc xóa dịch vụ khám trên ca của Bác sĩ A", () => {
    signInAs("DOCTOR", "doctor-2"); // Bác sĩ B truy cập ca của Bác sĩ A (doctor-1)
    detailMock.mockReturnValue(makeCase("IN_PROGRESS"));

    render(<CaseDetailView caseId="case-1" />);

    // Nút "Thêm dịch vụ" không được hiển thị cho Bác sĩ B
    expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();
    // Nút xóa dịch vụ không được hiển thị cho Bác sĩ B
    expect(screen.queryByTitle(/xóa dịch vụ/i)).not.toBeInTheDocument();
  });
});
