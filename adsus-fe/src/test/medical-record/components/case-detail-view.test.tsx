import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { CaseDetailView } from "@/features/medical-record/components/case-detail-view";

const { detailMock, saveMutate, confirmMutate } = vi.hoisted(() => ({
  detailMock: vi.fn(),
  saveMutate: vi.fn(),
  confirmMutate: vi.fn(),
}));

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCaseDetail: () => detailMock(),
  useAddUltrasoundImages: () => ({
    mutate: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
  useSaveCaseConclusion: () => ({
    // Component gọi mutate(input, { onSuccess }) — mock phải TỰ gọi onSuccess như react-query
    // thật thì mới kiểm được hành vi khoá tạm sau khi lưu (isLocked chuyển true trong callback đó).
    mutate: (
      input: unknown,
      options?: { onSuccess?: () => void },
    ) => {
      saveMutate(input);
      options?.onSuccess?.();
    },
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
    mutate: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

vi.mock("@/features/medical-record/hooks/use-case-report", () => ({
  useExportCaseReport: () => ({ exportReport: vi.fn(), isPending: false, error: null }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/features/prescriptions/components/prescription-section", () => ({
  PrescriptionSection: () => <div data-testid="prescription-section" />,
}));

function makeCase(
  status: "CREATED" | "ANALYZED" | "CONFIRMED" | "END",
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

function signInAs(role: "DOCTOR" | "NURSE", userId: string) {
  useAuthStore.getState().signIn("token", {
    userId,
    fullName: "Người dùng",
    email: null,
    role,
    mustChangePassword: false,
  });
}

describe("CaseDetailView", () => {
  beforeEach(() => {
    detailMock.mockReset();
    saveMutate.mockReset();
    confirmMutate.mockReset();
    useAuthStore.getState().signOut();
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
    detailMock.mockReturnValue(makeCase("CONFIRMED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByRole("button", { name: /bổ sung ảnh/i })).toBeDisabled();
  });

  it("cho bổ sung ảnh khi ca chưa kết luận", () => {
    detailMock.mockReturnValue(makeCase("CREATED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByRole("button", { name: /bổ sung ảnh/i })).toBeEnabled();
  });

  it("hiện ô hỏng cho ảnh không ký được URL", () => {
    // Flag F5 — imageUrl null khi Storage ký URL thất bại.
    detailMock.mockReturnValue(makeCase("ANALYZED"));

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
    detailMock.mockReturnValue(makeCase("ANALYZED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByText("case-1")).not.toBeInTheDocument();
  });

  // ---------- Kết luận: Lưu và Kết thúc ca khám (thêm/sửa 07/08/2026) ----------

  it("hiện form nhập kết luận cho đúng Bác sĩ phụ trách ca này khi chưa CONFIRMED", () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("ANALYZED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.getByLabelText(/chẩn đoán cuối cùng/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^lưu kết luận$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /xác nhận kết luận/i })).toBeInTheDocument();
  });

  it("KHÔNG hiện form cho Bác sĩ khác (không phải người phụ trách ca này)", () => {
    // GB-04 — chỉ đúng bác sĩ phụ trách CA NÀY (doctorId "doctor-1"), không phải bác sĩ bất kỳ.
    signInAs("DOCTOR", "doctor-2");
    detailMock.mockReturnValue(makeCase("ANALYZED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByLabelText(/chẩn đoán cuối cùng/i)).not.toBeInTheDocument();
    expect(screen.getByText(/ca khám chưa được kết luận/i)).toBeInTheDocument();
  });

  it("KHÔNG hiện form cho Điều dưỡng dù ca chưa CONFIRMED", () => {
    signInAs("NURSE", "nurse-1");
    detailMock.mockReturnValue(makeCase("CREATED"));

    render(<CaseDetailView caseId="case-1" />);

    expect(screen.queryByLabelText(/chẩn đoán cuối cùng/i)).not.toBeInTheDocument();
  });

  it("đổ sẵn kết luận đã lưu nháp trước đó vào form (chưa CONFIRMED)", () => {
    // Ca đã có finalDiagnosis/doctorConclusion (từ lần "Lưu kết luận" trước) nhưng status vẫn
    // chưa CONFIRMED — form phải hiện lại đúng nội dung đó, không trống.
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(
      makeCase("ANALYZED", {
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
    detailMock.mockReturnValue(makeCase("CREATED"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.click(screen.getByRole("button", { name: /^lưu kết luận$/i }));

    expect(saveMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/chẩn đoán và kết luận/i);
  });

  it("chặn Kết thúc ca khám khi bỏ trống chẩn đoán hoặc kết luận", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("CREATED"));
    const user = userEvent.setup();

    render(<CaseDetailView caseId="case-1" />);
    await user.click(screen.getByRole("button", { name: /xác nhận kết luận/i }));

    expect(confirmMutate).not.toHaveBeenCalled();
    expect(screen.getByRole("alert")).toHaveTextContent(/chẩn đoán và kết luận/i);
  });

  it("Lưu kết luận gọi đúng hàm lưu (không đổi trạng thái), không gọi hàm kết thúc", async () => {
    signInAs("DOCTOR", "doctor-1");
    detailMock.mockReturnValue(makeCase("ANALYZED"));
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
    detailMock.mockReturnValue(makeCase("ANALYZED"));
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
    detailMock.mockReturnValue(makeCase("ANALYZED"));
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
    detailMock.mockReturnValue(makeCase("ANALYZED"));
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
    detailMock.mockReturnValue(makeCase("ANALYZED"));
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
});
