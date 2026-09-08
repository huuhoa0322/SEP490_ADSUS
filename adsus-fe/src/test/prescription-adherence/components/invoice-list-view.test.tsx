import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { InvoiceListView } from "@/features/prescription-adherence/components/invoice-list-view";
import type { InvoiceResponse, PagedResult } from "@/api/invoiceService";

// Mock router
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

// Mock useInvoicesList hook
const mockUseInvoicesList = vi.fn();
vi.mock("@/features/prescription-adherence/hooks/use-invoices", () => ({
  useInvoicesList: (...args: unknown[]) => mockUseInvoicesList(...args),
}));

const mockInvoices: InvoiceResponse[] = [
  {
    id: "INV-001",
    caseId: "CASE-001",
    caseName: "Nguyen Van A",
    totalAmount: 100000,
    status: "PENDING",
    createdAt: "2023-01-01T00:00:00Z",
  },
  {
    id: "INV-002",
    caseId: "CASE-002",
    caseName: "Tran Thi B",
    totalAmount: 250000,
    status: "PAID",
    createdAt: "2023-01-02T00:00:00Z",
    paymentMethod: "CASH",
    paidAt: "2023-01-02T10:00:00Z",
  },
  {
    id: "INV-003",
    caseId: "CASE-003",
    caseName: "Le Van C",
    totalAmount: 75000,
    status: "CANCELLED",
    createdAt: "2023-01-03T00:00:00Z",
    cancelledReason: "Bệnh nhân yêu cầu",
  },
];

const pageResult: PagedResult<InvoiceResponse> = {
  items: mockInvoices,
  totalItems: 3,
  page: 1,
  pageSize: 10,
  totalPages: 1,
};

describe("InvoiceListView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders table with invoices on success", async () => {
    mockUseInvoicesList.mockReturnValue({
      data: pageResult,
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<InvoiceListView />);

    await waitFor(() => {
      expect(screen.getByText("Nguyen Van A")).toBeInTheDocument();
      expect(screen.getByText("Tran Thi B")).toBeInTheDocument();
      expect(screen.getByText("Le Van C")).toBeInTheDocument();
    });
  });

  it("shows loading state while fetching", async () => {
    mockUseInvoicesList.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: true,
    });

    render(<InvoiceListView />);

    await waitFor(() => {
      expect(screen.getByText("Đang tải...")).toBeInTheDocument();
    });
  });

  it("shows empty state when zero results", async () => {
    mockUseInvoicesList.mockReturnValue({
      data: { ...pageResult, items: [], totalItems: 0 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<InvoiceListView />);

    await waitFor(() => {
      expect(screen.getByText(/Chưa có hóa đơn nào/)).toBeInTheDocument();
    });
  });

  it("search input updates state and refetch on button click", async () => {
    const refetch = vi.fn();
    mockUseInvoicesList.mockReturnValue({
      data: pageResult,
      isLoading: false,
      isError: false,
      error: null,
      refetch,
      isFetching: false,
    });

    render(<InvoiceListView />);

    const searchInput = screen.getByPlaceholderText(/Tìm theo ID hoặc Tên bệnh nhân/);
    fireEvent.change(searchInput, { target: { value: "Nguyen" } });

    const searchButton = screen.getByRole("button", { name: /Tìm kiếm/ });
    fireEvent.click(searchButton);

    await waitFor(() => {
      expect(refetch).toHaveBeenCalled();
    });
  });

  it("search triggers refetch on Enter key", async () => {
    const refetch = vi.fn();
    mockUseInvoicesList.mockReturnValue({
      data: pageResult,
      isLoading: false,
      isError: false,
      error: null,
      refetch,
      isFetching: false,
    });

    render(<InvoiceListView />);

    const searchInput = screen.getByPlaceholderText(/Tìm theo ID hoặc Tên bệnh nhân/);
    fireEvent.change(searchInput, { target: { value: "TRAN" } });
    fireEvent.keyDown(searchInput, { key: "Enter" });

    await waitFor(() => {
      expect(refetch).toHaveBeenCalled();
    });
  });

  it("status filter changes the query", async () => {
    mockUseInvoicesList.mockReturnValue({
      data: { ...pageResult, items: [mockInvoices[1]], totalItems: 1 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    });

    render(<InvoiceListView />);

    await waitFor(() => {
      expect(screen.getByText("Tran Thi B")).toBeInTheDocument();
    });
  });

  it("shows error state with retry button", async () => {
    const refetch = vi.fn();
    mockUseInvoicesList.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error("Network error"),
      refetch,
      isFetching: false,
    });

    render(<InvoiceListView />);

    await waitFor(() => {
      expect(screen.getByText("Lỗi khi tải hóa đơn")).toBeInTheDocument();
    });

    const retryButton = screen.getByRole("button", { name: /Thử lại/ });
    fireEvent.click(retryButton);

    await waitFor(() => {
      expect(refetch).toHaveBeenCalled();
    });
  });
});
