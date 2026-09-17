import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { CaseClinicServicesPanel, getServiceDeletionGuard } from "../case-clinic-services-panel";
import type { CaseClinicService, ClinicService } from "../../types";

const mockServices: CaseClinicService[] = [
  {
    id: "ccs-1",
    caseId: "case-123",
    clinicServiceId: "cs-1",
    serviceName: "Khám thường",
    serviceCode: "GENERAL_EXAM",
    priceAtTime: 100000,
    createdAt: "2026-09-11T08:00:00Z",
  },
  {
    id: "ccs-2",
    caseId: "case-123",
    clinicServiceId: "cs-2",
    serviceName: "Khám siêu âm",
    serviceCode: "ULTRASOUND_EXAM",
    priceAtTime: 200000,
    createdAt: "2026-09-11T08:30:00Z",
  },
];

const mockCatalog: ClinicService[] = [
  {
    id: "cs-1",
    code: "GENERAL_EXAM",
    name: "Khám thường",
    price: 100000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
  {
    id: "cs-2",
    code: "ULTRASOUND_EXAM",
    name: "Khám siêu âm",
    price: 200000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
  {
    id: "cs-3",
    code: "BLOOD_TEST",
    name: "Xét nghiệm máu",
    price: 150000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
];

const { mockUseCaseClinicServices, mockUseClinicServicesList, mockUseCaseInvoices, mockAddMutate, mockRemoveMutate } = vi.hoisted(() => ({
  mockUseCaseClinicServices: vi.fn(),
  mockUseClinicServicesList: vi.fn(),
  mockUseCaseInvoices: vi.fn(),
  mockAddMutate: vi.fn(),
  mockRemoveMutate: vi.fn(),
}));

vi.mock("../../queries", () => ({
  useCaseClinicServices: () => mockUseCaseClinicServices(),
  useClinicServicesList: () => mockUseClinicServicesList(),
  useAddCaseClinicService: () => ({
    mutate: mockAddMutate,
    isPending: false,
  }),
  useRemoveCaseClinicService: () => ({
    mutate: mockRemoveMutate,
    isPending: false,
  }),
}));

vi.mock("@/features/prescription-adherence/hooks/use-invoices", () => ({
  useCaseInvoices: () => mockUseCaseInvoices(),
}));

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe("CaseClinicServicesPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseCaseClinicServices.mockReturnValue({
      data: mockServices,
      isLoading: false,
    });
    mockUseClinicServicesList.mockReturnValue({
      data: mockCatalog,
      isLoading: false,
    });
    mockUseCaseInvoices.mockReturnValue({
      data: [{ status: "PENDING" }],
      isLoading: false,
    });
  });

  it("renders panel with service items and formatted total price", () => {
    renderWithClient(<CaseClinicServicesPanel caseId="case-123" caseStatus="IN_PROGRESS" />);

    expect(screen.getByText("Dịch vụ khám")).toBeInTheDocument();
    expect(screen.getByText("Khám thường")).toBeInTheDocument();
    expect(screen.getByText("GENERAL_EXAM")).toBeInTheDocument();
    expect(screen.getByText("Khám siêu âm")).toBeInTheDocument();
    expect(screen.getByText("ULTRASOUND_EXAM")).toBeInTheDocument();
    expect(screen.getByText("Tổng tiền dịch vụ:")).toBeInTheDocument();
  });

  it("renders empty state message when no services attached", () => {
    mockUseCaseClinicServices.mockReturnValue({
      data: [],
      isLoading: false,
    });

    renderWithClient(<CaseClinicServicesPanel caseId="case-123" caseStatus="IN_PROGRESS" />);

    expect(screen.getByText("Chưa có dịch vụ nào được gắn cho ca khám này.")).toBeInTheDocument();
  });

  it("disables delete button and shows alert when invoice is PAID", () => {
    mockUseCaseInvoices.mockReturnValue({
      data: [{ status: "PAID" }],
      isLoading: false,
    });

    renderWithClient(<CaseClinicServicesPanel caseId="case-123" caseStatus="IN_PROGRESS" />);

    expect(
      screen.getByText(/Hóa đơn cho ca khám này đã được thanh toán/i),
    ).toBeInTheDocument();

    const deleteButtons = screen.getAllByTitle("Không thể xóa dịch vụ vì hóa đơn đã được thanh toán.");
    expect(deleteButtons.length).toBe(2);
    expect(deleteButtons[0]).toBeDisabled();
  });

  it("hides add button when case is END or CANCELLED", () => {
    renderWithClient(<CaseClinicServicesPanel caseId="case-123" caseStatus="END" />);

    expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();
  });

  it("hides add button and disables delete button with warning banner when case is BOOKED in card variant", () => {
    renderWithClient(<CaseClinicServicesPanel caseId="case-123" caseStatus="BOOKED" />);

    expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();
    expect(
      screen.getByText(/Ca khám đang chờ check-in\. Không thể thêm hoặc xóa dịch vụ khám\./i),
    ).toBeInTheDocument();

    const deleteButtons = screen.getAllByTitle("Không thể xóa dịch vụ khi ca khám đang chờ check-in.");
    expect(deleteButtons.length).toBe(2);
    expect(deleteButtons[0]).toBeDisabled();
  });

  it("hides add button and delete buttons when case is BOOKED in compact variant", () => {
    renderWithClient(
      <CaseClinicServicesPanel caseId="case-123" caseStatus="BOOKED" variant="compact" />,
    );

    expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();
    expect(screen.queryByTitle("Xóa dịch vụ này khỏi ca khám")).not.toBeInTheDocument();
  });

  it("opens add dialog and allows selecting available services", async () => {
    renderWithClient(<CaseClinicServicesPanel caseId="case-123" caseStatus="IN_PROGRESS" />);

    const addButton = screen.getByRole("button", { name: /thêm dịch vụ/i });
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(screen.getByText("Thêm dịch vụ vào ca khám")).toBeInTheDocument();
    });

    // Blood test is the only unattached service
    expect(screen.getByText(/Xét nghiệm máu/i)).toBeInTheDocument();
  });

  it("hides add button and delete buttons when isResponsibleDoctor is false in compact variant", () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="compact"
        isResponsibleDoctor={false}
      />,
    );

    expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();
    expect(screen.queryByTitle("Xóa dịch vụ này khỏi ca khám")).not.toBeInTheDocument();
  });

  it("disables delete button with tooltip for GENERAL_EXAM in card mode", () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="card"
      />,
    );

    const generalExamDeleteBtn = screen.getByTitle("Không thể xóa dịch vụ khám thường.");
    expect(generalExamDeleteBtn).toBeDisabled();
    expect(generalExamDeleteBtn).toHaveAttribute("aria-label", "Không thể xóa dịch vụ khám thường.");
  });

  it("hides delete X button for GENERAL_EXAM in compact mode", () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="compact"
        hasUltrasoundImages={false}
      />,
    );

    // GENERAL_EXAM delete button should not be rendered
    expect(screen.queryByRole("button", { name: "Xóa Khám thường" })).not.toBeInTheDocument();
    // But ULTRASOUND_EXAM (when no images) should have delete button
    expect(screen.getByRole("button", { name: "Xóa Khám siêu âm" })).toBeInTheDocument();
  });

  it("disables delete button for ULTRASOUND_EXAM in card mode when hasUltrasoundImages is true", () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="card"
        hasUltrasoundImages={true}
      />,
    );

    const usDeleteBtn = screen.getByTitle(
      "Không thể xóa dịch vụ siêu âm khi ca khám đã có ảnh siêu âm.",
    );
    expect(usDeleteBtn).toBeDisabled();
    expect(usDeleteBtn).toHaveAttribute(
      "aria-label",
      "Không thể xóa dịch vụ siêu âm khi ca khám đã có ảnh siêu âm.",
    );
  });

  it("enables delete button for ULTRASOUND_EXAM in card mode when hasUltrasoundImages is false", () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="card"
        hasUltrasoundImages={false}
      />,
    );

    const deleteBtn = screen.getByRole("button", { name: "Xóa dịch vụ Khám siêu âm" });
    expect(deleteBtn).toBeEnabled();
    expect(deleteBtn).toHaveAttribute("title", "Xóa dịch vụ này khỏi ca khám");
  });

  it("hides delete X button for ULTRASOUND_EXAM in compact mode when hasUltrasoundImages is true", () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="compact"
        hasUltrasoundImages={true}
      />,
    );

    // Both GENERAL_EXAM and ULTRASOUND_EXAM should have delete buttons hidden
    expect(screen.queryByRole("button", { name: "Xóa Khám thường" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Xóa Khám siêu âm" })).not.toBeInTheDocument();
  });

  it("opens confirm delete dialog and executes removal for removable service in card mode", async () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="card"
        hasUltrasoundImages={false}
      />,
    );

    const deleteBtn = screen.getByRole("button", { name: "Xóa dịch vụ Khám siêu âm" });
    fireEvent.click(deleteBtn);

    await waitFor(() => {
      expect(screen.getByText("Xác nhận xóa dịch vụ")).toBeInTheDocument();
      expect(
        screen.getByText(/Bạn có chắc chắn muốn xóa dịch vụ/i),
      ).toBeInTheDocument();
    });

    const confirmDeleteBtn = screen.getByRole("button", { name: "Xóa dịch vụ" });
    fireEvent.click(confirmDeleteBtn);

    expect(mockRemoveMutate).toHaveBeenCalledWith("ccs-2", expect.anything());
  });

  it("opens confirm delete dialog and executes removal for removable service in compact mode", async () => {
    renderWithClient(
      <CaseClinicServicesPanel
        caseId="case-123"
        caseStatus="IN_PROGRESS"
        variant="compact"
        hasUltrasoundImages={false}
      />,
    );

    const deleteBtn = screen.getByRole("button", { name: "Xóa Khám siêu âm" });
    fireEvent.click(deleteBtn);

    await waitFor(() => {
      expect(screen.getByText("Xác nhận xóa dịch vụ")).toBeInTheDocument();
      expect(
        screen.getByText(/Bạn có chắc chắn muốn xóa dịch vụ/i),
      ).toBeInTheDocument();
    });

    const confirmDeleteBtn = screen.getByRole("button", { name: "Xóa dịch vụ" });
    fireEvent.click(confirmDeleteBtn);

    expect(mockRemoveMutate).toHaveBeenCalledWith("ccs-2", expect.anything());
  });

  describe("getServiceDeletionGuard helper", () => {
    it("safely handles null or undefined service", () => {
      expect(getServiceDeletionGuard(null, false)).toEqual({ blocked: false, reason: "" });
      expect(getServiceDeletionGuard(undefined, true)).toEqual({ blocked: false, reason: "" });
    });

    it("blocks GENERAL_EXAM regardless of hasUltrasoundImages", () => {
      const generalService = mockServices[0];
      expect(getServiceDeletionGuard(generalService, false)).toEqual({
        blocked: true,
        reason: "Không thể xóa dịch vụ khám thường.",
      });
      expect(getServiceDeletionGuard(generalService, true)).toEqual({
        blocked: true,
        reason: "Không thể xóa dịch vụ khám thường.",
      });
    });

    it("blocks GENERAL_EXAM with lowercase or whitespace in serviceCode", () => {
      const generalLowercase = { ...mockServices[0], serviceCode: "general_exam" };
      const generalWhitespace = { ...mockServices[0], serviceCode: "  GENERAL_EXAM  " };
      expect(getServiceDeletionGuard(generalLowercase, false).blocked).toBe(true);
      expect(getServiceDeletionGuard(generalWhitespace, false).blocked).toBe(true);
    });

    it("blocks ULTRASOUND_EXAM only when hasUltrasoundImages is true", () => {
      const usService = mockServices[1];
      expect(getServiceDeletionGuard(usService, true)).toEqual({
        blocked: true,
        reason: "Không thể xóa dịch vụ siêu âm khi ca khám đã có ảnh siêu âm.",
      });
      expect(getServiceDeletionGuard(usService, false)).toEqual({
        blocked: false,
        reason: "",
      });
    });

    it("blocks ULTRASOUND_EXAM with lowercase or whitespace when hasUltrasoundImages is true", () => {
      const usLowercase = { ...mockServices[1], serviceCode: "ultrasound_exam" };
      const usWhitespace = { ...mockServices[1], serviceCode: "  ULTRASOUND_EXAM  " };
      expect(getServiceDeletionGuard(usLowercase, true).blocked).toBe(true);
      expect(getServiceDeletionGuard(usWhitespace, true).blocked).toBe(true);
      expect(getServiceDeletionGuard(usLowercase, false).blocked).toBe(false);
    });

    it("allows deletion for other services", () => {
      const bloodTest: CaseClinicService = {
        id: "ccs-3",
        caseId: "case-123",
        clinicServiceId: "cs-3",
        serviceName: "Xét nghiệm máu",
        serviceCode: "BLOOD_TEST",
        priceAtTime: 150000,
        createdAt: "2026-09-11T09:00:00Z",
      };
      expect(getServiceDeletionGuard(bloodTest, true)).toEqual({
        blocked: false,
        reason: "",
      });
    });
  });
});
