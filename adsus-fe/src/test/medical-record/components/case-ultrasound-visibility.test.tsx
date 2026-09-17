import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";
import { CaseDetailView } from "@/features/medical-record/components/case-detail-view";
import type { CaseDetail } from "@/features/medical-record/types/medical-record.types";
import type { CaseClinicService } from "@/features/clinic-service/types";

const { detailMock, clinicServicesMock } = vi.hoisted(() => ({
  detailMock: vi.fn(),
  clinicServicesMock: vi.fn(),
}));

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCaseDetail: () => detailMock(),
  useUpdateCaseSymptoms: () => ({ mutate: vi.fn(), isPending: false }),
  useUpdateCaseDiseases: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateCaseAllergies: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useSaveCaseConclusion: () => ({ mutate: vi.fn(), isPending: false, isSuccess: false, isError: false, error: null }),
  useConfirmCase: () => ({ mutate: vi.fn(), isPending: false, isSuccess: false, isError: false, error: null }),
  useEndCaseWithoutPrescription: () => ({ mutate: vi.fn(), isPending: false, isSuccess: false, isError: false, error: null }),
}));

vi.mock("@/features/medical-record/hooks/use-diagnosis", () => ({
  useDiagnosisItems: () => ({
    data: [],
    isLoading: false,
    isError: false,
  }),
  useUpdateCaseDiagnoses: () => ({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock("@/features/clinic-service/queries", () => ({
  useCaseClinicServices: () => clinicServicesMock(),
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

vi.mock("@/features/prescription-adherence/hooks/use-invoices", () => ({
  useCaseInvoices: () => ({ data: [], isLoading: false }),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-doctor-appointments", () => ({
  useCreateFollowUpAppointment: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock("@/features/appointment-scheduling/hooks/use-schedule-slot", () => ({
  useScheduleSlots: () => ({ data: undefined, isLoading: false }),
}));

vi.mock("@/components/ui/rich-text-editor", () => ({
  RichTextEditor: () => <div data-testid="rich-text-editor" />,
}));

vi.mock("@/features/medical-record/components/ultrasound-image-gallery", () => ({
  UltrasoundImageGallery: ({ images }: { images: unknown[] }) => (
    <div data-testid="ultrasound-gallery">
      {images.length} ảnh trong gallery
    </div>
  ),
}));

vi.mock("@/features/clinic-service/components/case-clinic-services-panel", () => ({
  CaseClinicServicesPanel: () => <div data-testid="case-clinic-services-panel" />,
}));

function makeMockCase(overrides: Partial<CaseDetail> = {}): {
  data: CaseDetail;
  isLoading: boolean;
  isError: boolean;
  error: null;
} {
  const base: CaseDetail = {
    caseId: "case-us-100",
    patientProfileId: "profile-us-100",
    doctorId: "doc-100",
    doctorName: "BS. Nguyễn Văn An",
    visitDate: "2026-09-12",
    clinicalInfo: "Khám định kỳ",
    status: "IN_PROGRESS",
    caseDiagnoses: [],
    doctorConclusion: null,
    patientProfile: {
      patientProfileId: "profile-us-100",
      patientUserId: "user-us-100",
      fullName: "Trần Thị Lan",
      phone: "0912345678",
      dateOfBirth: "1995-05-15",
      gender: "FEMALE",
      diseases: [],
      allergies: [],
      createdBy: "staff-1",
      createdAt: "2026-09-12T08:00:00Z",
      updatedAt: "2026-09-12T08:00:00Z",
    },
    ultrasoundImages: [],
    symptoms: [],
    caseDiseases: [],
    caseAllergies: [],
    aiResults: [],
    prescription: null,
    createdAt: "2026-09-12T08:00:00Z",
    updatedAt: "2026-09-12T08:00:00Z",
  };

  return {
    data: { ...base, ...overrides },
    isLoading: false,
    isError: false,
    error: null,
  };
}

describe("Case Ultrasound Section Visibility & Responsive Layout (US-01 -> US-08)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      user: {
        userId: "doc-100",
        role: "DOCTOR",
        email: "doctor@test.com",
        fullName: "BS. Nguyễn Văn An",
        mustChangePassword: false,
      },
      accessToken: "mock-token",
    });
  });

  it("US-01: Ca không có dịch vụ ULTRASOUND_EXAM -> Khung ảnh siêu âm ẩn hoàn toàn và layout chuyển sang lg:grid-cols-2", () => {
    detailMock.mockReturnValue(makeMockCase());
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-1",
          caseId: "case-us-100",
          clinicServiceId: "srv-gen",
          serviceName: "Khám thường",
          serviceCode: "GENERAL_EXAM",
          priceAtTime: 100000,
          createdAt: "2026-09-12T08:00:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });

    const { container } = render(<CaseDetailView caseId="case-us-100" />);

    // Section "Ảnh siêu âm" không được render
    expect(screen.queryByRole("heading", { name: /ảnh siêu âm/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /bổ sung ảnh siêu âm/i })).not.toBeInTheDocument();
    expect(screen.queryByTestId("ultrasound-gallery")).not.toBeInTheDocument();

    // Responsive grid áp dụng lg:grid-cols-2 cân đối
    const gridContainer = container.querySelector(".lg\\:grid-cols-2");
    expect(gridContainer).toBeInTheDocument();
    expect(container.querySelector(".lg\\:grid-cols-\\[1\\.7fr_1fr\\]")).not.toBeInTheDocument();

    // Đảm bảo 2 cột độc lập trực tiếp của Grid (Cột trái 50%: Lâm sàng, Cột phải 50%: Kết luận)
    expect(gridContainer?.children).toHaveLength(2);
    expect(gridContainer?.children[0]).toHaveTextContent(/thông tin lâm sàng/i);
    expect(gridContainer?.children[1]).toHaveTextContent(/kết luận của bác sĩ/i);
  });

  it("US-02: Ca CÓ dịch vụ ULTRASOUND_EXAM -> Khung ảnh siêu âm hiển thị đầy đủ và layout chuyển sang lg:grid-cols-[1.7fr_1fr]", () => {
    detailMock.mockReturnValue(makeMockCase());
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-1",
          caseId: "case-us-100",
          clinicServiceId: "srv-gen",
          serviceName: "Khám thường",
          serviceCode: "GENERAL_EXAM",
          priceAtTime: 100000,
          createdAt: "2026-09-12T08:00:00Z",
        },
        {
          id: "cs-2",
          caseId: "case-us-100",
          clinicServiceId: "srv-us",
          serviceName: "Khám siêu âm",
          serviceCode: "ULTRASOUND_EXAM",
          priceAtTime: 200000,
          createdAt: "2026-09-12T08:15:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });

    const { container } = render(<CaseDetailView caseId="case-us-100" />);

    // Section "Ảnh siêu âm" hiển thị đầy đủ
    expect(screen.getByRole("heading", { name: /ảnh siêu âm/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /bổ sung ảnh siêu âm/i })).toBeInTheDocument();
    expect(screen.getByTestId("ultrasound-gallery")).toBeInTheDocument();

    // Layout áp dụng lg:grid-cols-[1.7fr_1fr]
    const gridContainer = container.querySelector(".lg\\:grid-cols-\\[1\\.7fr_1fr\\]");
    expect(gridContainer).toBeInTheDocument();
    expect(container.querySelector(".lg\\:grid-cols-2")).not.toBeInTheDocument();

    // Cột 1 là Ảnh siêu âm, Cột 2 là wrapper space-y-6 chứa lâm sàng & kết luận
    expect(gridContainer?.children).toHaveLength(2);
    expect(gridContainer?.children[0]).toHaveTextContent(/ảnh siêu âm/i);
    expect(gridContainer?.children[1]).toHaveClass("space-y-6");
  });

  it("US-03: Ca có ảnh siêu âm trong payload nhưng dịch vụ ULTRASOUND_EXAM đã bị xóa/chưa có -> Khung ảnh vẫn bị ẩn (đúng kịch bản chuẩn)", () => {
    detailMock.mockReturnValue(
      makeMockCase({
        ultrasoundImages: [
          {
            imageId: "img-1",
            caseId: "case-us-100",
            imageUrl: "https://example.com/us1.png",
            uploadedAt: "2026-09-12T08:30:00Z",
            note: "Ảnh siêu âm trước đó",
          },
        ],
      }),
    );
    // Danh sách dịch vụ không có ULTRASOUND_EXAM
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-1",
          caseId: "case-us-100",
          clinicServiceId: "srv-gen",
          serviceName: "Khám thường",
          serviceCode: "GENERAL_EXAM",
          priceAtTime: 100000,
          createdAt: "2026-09-12T08:00:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });

    render(<CaseDetailView caseId="case-us-100" />);

    // Đúng theo quyết định: không có service -> ẩn hoàn toàn
    expect(screen.queryByRole("heading", { name: /ảnh siêu âm/i })).not.toBeInTheDocument();
    expect(screen.queryByTestId("ultrasound-gallery")).not.toBeInTheDocument();
  });

  it("US-04: Ca có dịch vụ khác (ví dụ: XET_NGHIEM_MAU, NOI_SOI) nhưng không có ULTRASOUND_EXAM -> Khung ảnh vẫn bị ẩn", () => {
    detailMock.mockReturnValue(makeMockCase());
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-3",
          caseId: "case-us-100",
          clinicServiceId: "srv-blood",
          serviceName: "Xét nghiệm máu",
          serviceCode: "BLOOD_TEST",
          priceAtTime: 150000,
          createdAt: "2026-09-12T08:00:00Z",
        },
        {
          id: "cs-4",
          caseId: "case-us-100",
          clinicServiceId: "srv-endo",
          serviceName: "Nội soi dạ dày",
          serviceCode: "ENDOSCOPY",
          priceAtTime: 300000,
          createdAt: "2026-09-12T08:00:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });

    render(<CaseDetailView caseId="case-us-100" />);
    expect(screen.queryByRole("heading", { name: /ảnh siêu âm/i })).not.toBeInTheDocument();
  });

  it("US-05: Khi danh sách dịch vụ rỗng (data = []) hoặc undefined -> Khung ảnh không bị crash và bị ẩn", () => {
    detailMock.mockReturnValue(makeMockCase());
    clinicServicesMock.mockReturnValue({
      data: undefined,
      isLoading: false,
    });

    render(<CaseDetailView caseId="case-us-100" />);
    expect(screen.queryByRole("heading", { name: /ảnh siêu âm/i })).not.toBeInTheDocument();
  });

  it("US-06: Ca CÓ dịch vụ ULTRASOUND_EXAM nhưng người xem là Điều dưỡng (STAFF) -> Khung ảnh hiện gallery nhưng không hiện nút Bổ sung ảnh", () => {
    useAuthStore.setState({
      user: {
        userId: "nurse-101",
        role: "STAFF",
        email: "nurse@test.com",
        fullName: "ĐD. Lê Thị Mai",
        mustChangePassword: false,
      },
      accessToken: "mock-token",
    });

    detailMock.mockReturnValue(makeMockCase());
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-2",
          caseId: "case-us-100",
          clinicServiceId: "srv-us",
          serviceName: "Khám siêu âm",
          serviceCode: "ULTRASOUND_EXAM",
          priceAtTime: 200000,
          createdAt: "2026-09-12T08:15:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });

    render(<CaseDetailView caseId="case-us-100" />);

    // Section và gallery hiển thị
    expect(screen.getByRole("heading", { name: /ảnh siêu âm/i })).toBeInTheDocument();
    expect(screen.getByTestId("ultrasound-gallery")).toBeInTheDocument();
    // Nút Bổ sung ảnh siêu âm bị ẩn đối với điều dưỡng
    expect(screen.queryByRole("button", { name: /bổ sung ảnh siêu âm/i })).not.toBeInTheDocument();
  });

  it("US-07: Ca CÓ dịch vụ ULTRASOUND_EXAM và đã kết thúc (END) -> Khung ảnh hiện gallery, nút Bổ sung ảnh bị disabled kèm thông báo", () => {
    detailMock.mockReturnValue(makeMockCase({ status: "END" }));
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-2",
          caseId: "case-us-100",
          clinicServiceId: "srv-us",
          serviceName: "Khám siêu âm",
          serviceCode: "ULTRASOUND_EXAM",
          priceAtTime: 200000,
          createdAt: "2026-09-12T08:15:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });

    render(<CaseDetailView caseId="case-us-100" />);

    expect(screen.getByRole("heading", { name: /ảnh siêu âm/i })).toBeInTheDocument();
    const addBtn = screen.getByRole("button", { name: /bổ sung ảnh siêu âm/i });
    expect(addBtn).toBeDisabled();
    expect(screen.getByText(/Ca đã kết luận nên không nhận thêm ảnh/i)).toBeInTheDocument();
  });

  it("US-08: Tuân thủ tuyệt đối Rules of Hooks khi trạng thái thay đổi từ loading sang loaded", () => {
    // Render lần 1: Đang tải
    detailMock.mockReturnValue({ data: undefined, isLoading: true, isError: false, error: null });
    clinicServicesMock.mockReturnValue({ data: undefined, isLoading: true });
    const { rerender } = render(<CaseDetailView caseId="case-us-100" />);
    expect(screen.getByText(/Đang tải ca khám/i)).toBeInTheDocument();

    // Render lần 2: Đã tải, có dịch vụ siêu âm
    detailMock.mockReturnValue(makeMockCase());
    clinicServicesMock.mockReturnValue({
      data: [
        {
          id: "cs-2",
          caseId: "case-us-100",
          clinicServiceId: "srv-us",
          serviceName: "Khám siêu âm",
          serviceCode: "ULTRASOUND_EXAM",
          priceAtTime: 200000,
          createdAt: "2026-09-12T08:15:00Z",
        },
      ] as CaseClinicService[],
      isLoading: false,
    });
    rerender(<CaseDetailView caseId="case-us-100" />);
    expect(screen.getByRole("heading", { name: /ảnh siêu âm/i })).toBeInTheDocument();

    // Render lần 3: Dịch vụ bị gỡ bỏ
    clinicServicesMock.mockReturnValue({ data: [], isLoading: false });
    rerender(<CaseDetailView caseId="case-us-100" />);
    expect(screen.queryByRole("heading", { name: /ảnh siêu âm/i })).not.toBeInTheDocument();
  });
});
