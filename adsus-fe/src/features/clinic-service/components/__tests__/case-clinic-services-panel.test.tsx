import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { CaseClinicServicesPanel } from "../case-clinic-services-panel";
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
});
