import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DOMPurify from "isomorphic-dompurify";

import { useAuthStore } from "@/store/auth-store";
import { CaseDetailView } from "@/features/medical-record/components/case-detail-view";
import type { CaseDetail } from "@/features/medical-record/types/medical-record.types";

const {
  detailMock,
  updateSymptomsMutate,
  updateDiseasesMutate,
  updateAllergiesMutate,
  saveConclusionMutate,
  confirmMutate,
  endMutate,
  exportReportMock,
  pushMock,
} = vi.hoisted(() => ({
  detailMock: vi.fn(),
  updateSymptomsMutate: vi.fn(),
  updateDiseasesMutate: vi.fn(),
  updateAllergiesMutate: vi.fn(),
  saveConclusionMutate: vi.fn(),
  confirmMutate: vi.fn(),
  endMutate: vi.fn(),
  exportReportMock: vi.fn(),
  pushMock: vi.fn(),
}));

let isUpdateDiseasesPending = false;
let isUpdateSymptomsPending = false;

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCaseDetail: () => detailMock(),
  useUpdateCaseSymptoms: () => ({
    mutate: (input: unknown, options?: { onSuccess?: () => void }) => {
      updateSymptomsMutate(input);
      options?.onSuccess?.();
    },
    get isPending() {
      return isUpdateSymptomsPending;
    },
  }),
  useUpdateCaseDiseases: () => ({
    mutateAsync: async (input: unknown) => {
      updateDiseasesMutate(input);
    },
    get isPending() {
      return isUpdateDiseasesPending;
    },
  }),
  useUpdateCaseAllergies: () => ({
    mutateAsync: async (input: unknown) => {
      updateAllergiesMutate(input);
    },
    isPending: false,
  }),
  useSaveCaseConclusion: () => ({
    mutate: saveConclusionMutate,
    isPending: false,
    isSuccess: false,
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
    mutate: endMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

vi.mock("@/features/medical-record/hooks/use-case-report", () => ({
  useExportCaseReport: () => ({
    exportReport: exportReportMock,
    isPending: false,
    error: null,
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

vi.mock("@/features/appointment-scheduling/hooks/use-doctor-appointments", () => ({
  useCreateFollowUpAppointment: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-schedule-slot", () => ({
  useScheduleSlots: () => ({ data: undefined, isLoading: false }),
}));

vi.mock("@/components/ui/rich-text-editor", () => ({
  RichTextEditor: ({
    value,
    onChange,
    disabled,
    placeholder,
    className,
  }: {
    value: string;
    onChange: (val: string) => void;
    disabled?: boolean;
    placeholder?: string;
    className?: string;
  }) => (
    <textarea
      id="finalDiagnosis"
      aria-label="Chẩn đoán & Kết luận *"
      data-testid="rich-text-editor"
      className={className ?? "border border-gray-300"}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      placeholder={placeholder}
    />
  ),
}));

vi.mock("@/features/medical-record/components/medical-history-selector", () => ({
  MedicalHistorySelector: ({
    value,
    onChange,
  }: {
    value: Array<{ diseaseId: string; note: string | null }>;
    onChange: (v: Array<{ diseaseId: string; note: string | null }>) => void;
  }) => (
    <div data-testid="medical-history-selector">
      {value?.map((d) => (
        <span key={d.diseaseId} data-testid={`pre-disease-${d.diseaseId}`}>
          {d.note ?? "no-note"}
        </span>
      ))}
      <button
        type="button"
        data-testid="add-disease-btn"
        onClick={() => onChange([...(value ?? []), { diseaseId: "d-added", note: "Ghi chú mới" }])}
      >
        Thêm bệnh
      </button>
    </div>
  ),
}));

vi.mock("@/features/medical-record/components/allergy-selector", () => ({
  AllergySelector: ({
    value,
  }: {
    value: Array<{ allergyTypeId: string; note: string | null }>;
    onChange?: unknown;
  }) => (
    <div data-testid="allergy-selector">
      {value?.map((a) => (
        <span key={a.allergyTypeId} data-testid={`pre-allergy-${a.allergyTypeId}`}>
          {a.note ?? "no-note"}
        </span>
      ))}
    </div>
  ),
}));

vi.mock("@/features/medical-record/components/symptom-selector", () => ({
  SymptomSelector: ({
    value,
    onChange,
  }: {
    value: Array<{ categoryId: string; symptomId: string | null; otherNote: string | null }>;
    onChange: (v: Array<{ categoryId: string; symptomId: string | null; otherNote: string | null }>) => void;
  }) => (
    <div data-testid="symptom-selector">
      {value?.map((s, idx) => (
        <span key={idx} data-testid={`symptom-item-${s.categoryId}`}>
          {s.symptomId ?? s.otherNote}
        </span>
      ))}
      <button
        type="button"
        data-testid="add-symptom-btn"
        onClick={() =>
          onChange([...(value ?? []), { categoryId: "cat-new", symptomId: null, otherNote: "Khác: sốt nhẹ" }])
        }
      >
        Thêm triệu chứng
      </button>
    </div>
  ),
}));

function makeMockCase(overrides: Partial<CaseDetail> = {}): {
  data: CaseDetail;
  isLoading: boolean;
  isError: boolean;
  error: null;
} {
  const base: CaseDetail = {
    caseId: "case-100",
    patientProfileId: "profile-100",
    doctorId: "doc-100",
    doctorName: "BS. Nguyễn Văn An",
    visitDate: "2026-09-12",
    clinicalInfo: "Khám định kỳ",
    status: "IN_PROGRESS",
    finalDiagnosis: "<p>Chẩn đoán <strong>u tuyến xơ</strong></p>",
    doctorConclusion: "",
    patientProfile: {
      patientProfileId: "profile-100",
      patientUserId: "user-100",
      fullName: "Trần Thị B",
      phone: "0909123456",
      dateOfBirth: "1995-04-10",
      gender: "FEMALE",
      diseases: [],
      allergies: [],
      createdBy: "user-100",
      createdAt: "2026-09-01",
      updatedAt: "2026-09-01",
    },
    ultrasoundImages: [],
    symptoms: [
      {
        categoryId: "cat-1",
        categoryName: "Triệu chứng chung",
        symptomId: "sym-1",
        symptomName: "Đau đầu",
        otherNote: null,
      },
      {
        categoryId: "cat-2",
        categoryName: "Triệu chứng đặc biệt",
        symptomId: null,
        symptomName: null,
        otherNote: "Khác: Buồn nôn vào buổi sáng",
      },
    ],
    caseDiseases: [
      {
        diseaseId: "d-1",
        diseaseName: "Viêm dạ dày mạn",
        isOther: false,
        note: "Điều trị 2 năm",
      },
    ],
    caseAllergies: [
      {
        allergyTypeId: "a-1",
        allergyName: "Penicillin",
        isOther: false,
        note: "Mẩn ngứa",
      },
    ],
    aiResults: [],
    prescription: null,
    createdAt: "2026-09-12T08:00:00Z",
    updatedAt: "2026-09-12T08:30:00Z",
  };

  return {
    data: { ...base, ...overrides },
    isLoading: false,
    isError: false,
    error: null,
  };
}

function signInAsDoctor(doctorId = "doc-100") {
  useAuthStore.getState().signIn("access-token", "refresh-token", {
    userId: doctorId,
    fullName: "Bác sĩ thử nghiệm",
    email: "doctor@adsus.test",
    role: "DOCTOR",
    mustChangePassword: false,
  });
}

describe("Case Snapshot & Inline Edit Frontend Test Suite (F1-F10)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    isUpdateDiseasesPending = false;
    isUpdateSymptomsPending = false;
    useAuthStore.getState().signOut();
    signInAsDoctor("doc-100");
  });

  // =========================================================================
  // F1: Click "Sửa" tiền sử bệnh -> MedicalHistorySelector pre-populated with caseDiseases
  // =========================================================================
  it("F1: Bấm 'Sửa tiền sử & dị ứng' -> MedicalHistorySelector được đổ sẵn dữ liệu từ caseDiseases", async () => {
    const mockCase = makeMockCase({
      caseDiseases: [
        { diseaseId: "d-pre-1", diseaseName: "Tiểu đường", isOther: false, note: "Đang dùng insulin" },
      ],
    });
    detailMock.mockReturnValue(mockCase);
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-100" />);

    // Lúc đầu ở chế độ xem: hiển thị badge tiền sử bệnh
    expect(screen.getByText(/Tiểu đường: Đang dùng insulin/i)).toBeInTheDocument();

    // Bấm nút "Sửa tiền sử & dị ứng"
    const editHistoryBtn = screen.getByRole("button", { name: /sửa tiền sử & dị ứng/i });
    await user.click(editHistoryBtn);

    // Selector hiển thị và pre-populated với dữ liệu snapshot caseDiseases
    expect(screen.getByTestId("medical-history-selector")).toBeInTheDocument();
    expect(screen.getByTestId("pre-disease-d-pre-1")).toHaveTextContent("Đang dùng insulin");
  });

  // =========================================================================
  // F2: Click "Sửa triệu chứng" -> SymptomSelector rendered (triệu chứng Khác hiển thị đúng)
  // =========================================================================
  it("F2: Bấm 'Sửa triệu chứng' -> SymptomSelector hiển thị đúng triệu chứng và ghi chú 'Khác'", async () => {
    const mockCase = makeMockCase();
    detailMock.mockReturnValue(mockCase);
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-100" />);

    const editSymptomsBtn = screen.getByRole("button", { name: /sửa triệu chứng/i });
    await user.click(editSymptomsBtn);

    expect(screen.getByTestId("symptom-selector")).toBeInTheDocument();
    expect(screen.getByTestId("symptom-item-cat-1")).toHaveTextContent("sym-1");
    expect(screen.getByTestId("symptom-item-cat-2")).toHaveTextContent("Khác: Buồn nôn vào buổi sáng");
  });

  // =========================================================================
  // F3: Save diseases -> loading state
  // =========================================================================
  it("F3: Lưu tiền sử & dị ứng -> hiển thị loading state 'Đang lưu...' và vô hiệu hoá nút", async () => {
    isUpdateDiseasesPending = true;
    detailMock.mockReturnValue(makeMockCase());
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-100" />);

    await user.click(screen.getByRole("button", { name: /sửa tiền sử & dị ứng/i }));

    const saveBtn = screen.getByRole("button", { name: /đang lưu\.\.\./i });
    expect(saveBtn).toBeInTheDocument();
    expect(saveBtn).toBeDisabled();

    const cancelBtn = screen.getByRole("button", { name: /^hủy$/i });
    expect(cancelBtn).toBeDisabled();
  });

  // =========================================================================
  // F4: Rich text formatting -> rendered with prose class
  // =========================================================================
  it("F4: Khi ca CONFIRMED -> chẩn đoán rich text được render với class 'prose'", () => {
    const richTextDiagnosis = "<p>Bệnh nhân bị <strong>u xơ tử cung</strong>:</p><ul><li>Kích thước 3cm</li></ul>";
    const mockCase = makeMockCase({
      status: "CONFIRMED",
      finalDiagnosis: richTextDiagnosis,
    });
    detailMock.mockReturnValue(mockCase);

    const { container } = render(<CaseDetailView caseId="case-100" />);

    const proseDiv = container.querySelector(".prose");
    expect(proseDiv).toBeInTheDocument();
    expect(proseDiv?.innerHTML).toContain("<strong>u xơ tử cung</strong>");
    expect(proseDiv?.innerHTML).toContain("<li>Kích thước 3cm</li>");
  });

  // =========================================================================
  // F5: XSS sanitization -> script tags sanitized by DOMPurify
  // =========================================================================
  it("F5: Ngăn chặn mã độc XSS -> script và thẻ độc hại được DOMPurify thanh lọc an toàn", () => {
    const dangerousHtml = "<p>Chẩn đoán</p><script>alert('XSS_ATTACK')</script><img src=x onerror=alert('x') />";
    const sanitized = DOMPurify.sanitize(dangerousHtml);

    expect(sanitized).not.toContain("<script>");
    expect(sanitized).not.toContain("onerror");
    expect(sanitized).toContain("<p>Chẩn đoán</p>");

    const mockCase = makeMockCase({
      status: "CONFIRMED",
      finalDiagnosis: dangerousHtml,
    });
    detailMock.mockReturnValue(mockCase);

    const { container } = render(<CaseDetailView caseId="case-100" />);
    const proseDiv = container.querySelector(".prose");

    expect(proseDiv?.innerHTML).not.toContain("<script>");
    expect(proseDiv?.innerHTML).not.toContain("onerror");
  });

  // =========================================================================
  // F6: Click "Hủy" edit symptoms -> reverts state, no API call
  // =========================================================================
  it("F6: Bấm 'Hủy' khi đang sửa triệu chứng -> đóng form sửa và không gọi API", async () => {
    detailMock.mockReturnValue(makeMockCase());
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-100" />);

    // Mở form sửa
    await user.click(screen.getByRole("button", { name: /sửa triệu chứng/i }));
    expect(screen.getByTestId("symptom-selector")).toBeInTheDocument();

    // Bấm thêm triệu chứng tạm thời
    await user.click(screen.getByTestId("add-symptom-btn"));

    // Bấm nút Hủy
    await user.click(screen.getByRole("button", { name: /^hủy$/i }));

    // Form sửa đóng lại, nút "Sửa triệu chứng" tái xuất hiện
    expect(screen.queryByTestId("symptom-selector")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sửa triệu chứng/i })).toBeInTheDocument();

    // Không có API call nào được thực thi
    expect(updateSymptomsMutate).not.toHaveBeenCalled();
  });

  // =========================================================================
  // F7: Sync logic on case change -> updates state properly
  // =========================================================================
  it("F7: Đồng bộ trạng thái khi case thay đổi (syncedCaseId) -> reset edit mode và cập nhật dữ liệu mới", () => {
    const caseA = makeMockCase({
      caseId: "case-A",
      finalDiagnosis: "Chẩn đoán ca A",
      caseDiseases: [{ diseaseId: "dA", diseaseName: "Bệnh A", isOther: false, note: null }],
    });
    const caseB = makeMockCase({
      caseId: "case-B",
      finalDiagnosis: "Chẩn đoán ca B",
      caseDiseases: [{ diseaseId: "dB", diseaseName: "Bệnh B", isOther: false, note: null }],
    });

    detailMock.mockReturnValue(caseA);
    const { rerender } = render(<CaseDetailView caseId="case-A" />);

    expect(screen.getByText("Bệnh A")).toBeInTheDocument();

    // Rerender với case mới (case-B)
    detailMock.mockReturnValue(caseB);
    rerender(<CaseDetailView caseId="case-B" />);

    expect(screen.queryByText("Bệnh A")).not.toBeInTheDocument();
    expect(screen.getByText("Bệnh B")).toBeInTheDocument();
  });

  // =========================================================================
  // F8: No React hook order errors (Rules of Hooks verification)
  // =========================================================================
  it("F8: Tuân thủ tuyệt đối Rules of Hooks -> không phát sinh lỗi thứ tự hook khi render có điều kiện", () => {
    // 1. Render khi đang tải (isLoading = true)
    detailMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    });
    const { rerender } = render(<CaseDetailView caseId="case-100" />);
    expect(screen.getByText(/đang tải ca khám/i)).toBeInTheDocument();

    // 2. Chuyển sang có dữ liệu
    detailMock.mockReturnValue(makeMockCase());
    expect(() => rerender(<CaseDetailView caseId="case-100" />)).not.toThrow();

    // 3. Chuyển sang trạng thái lỗi (isError = true)
    detailMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error("Lỗi tải ca khám"),
    });
    expect(() => rerender(<CaseDetailView caseId="case-100" />)).not.toThrow();
  });

  // =========================================================================
  // F9: Independent edit states for symptoms, diseases, allergies
  // =========================================================================
  it("F9: Các khu vực chỉnh sửa (Triệu chứng, Tiền sử/Dị ứng) hoạt động độc lập không xung đột", async () => {
    detailMock.mockReturnValue(makeMockCase());
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-100" />);

    // Mở đồng thời cả sửa Triệu chứng và sửa Tiền sử/Dị ứng
    await user.click(screen.getByRole("button", { name: /sửa triệu chứng/i }));
    await user.click(screen.getByRole("button", { name: /sửa tiền sử & dị ứng/i }));

    expect(screen.getByTestId("symptom-selector")).toBeInTheDocument();
    expect(screen.getByTestId("medical-history-selector")).toBeInTheDocument();
    expect(screen.getByTestId("allergy-selector")).toBeInTheDocument();

    // Hủy sửa Triệu chứng: chỉ có symptom selector đóng, tiền sử/dị ứng vẫn đang ở chế độ sửa
    const cancelButtons = screen.getAllByRole("button", { name: /^hủy$/i });
    // Bấm hủy ở khối triệu chứng
    await user.click(cancelButtons[1]);

    expect(screen.queryByTestId("symptom-selector")).not.toBeInTheDocument();
    expect(screen.getByTestId("medical-history-selector")).toBeInTheDocument();
  });

  // =========================================================================
  // F10: Case CONFIRMED -> all edit buttons hidden
  // =========================================================================
  it("F10: Ca ở trạng thái CONFIRMED -> toàn bộ các nút sửa (Triệu chứng, Tiền sử, Kết luận) bị ẩn", () => {
    const confirmedCase = makeMockCase({ status: "CONFIRMED" });
    detailMock.mockReturnValue(confirmedCase);

    render(<CaseDetailView caseId="case-100" />);

    // Các nút chỉnh sửa lâm sàng bị ẩn
    expect(screen.queryByRole("button", { name: /sửa triệu chứng/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /sửa tiền sử & dị ứng/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^lưu kết luận$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /xác nhận kết luận/i })).not.toBeInTheDocument();
    expect(screen.queryByTestId("rich-text-editor")).not.toBeInTheDocument();
  });
});
