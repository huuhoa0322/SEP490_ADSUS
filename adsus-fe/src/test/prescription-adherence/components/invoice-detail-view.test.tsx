import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, Mock } from "vitest";
import { InvoiceDetailView } from "@/features/prescription-adherence/components/invoice-detail-view";
import { invoiceService } from "@/api/invoiceService";
import toast from "react-hot-toast";

// Mock router
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

// Mock invoiceService
vi.mock("@/api/invoiceService", () => ({
  invoiceService: {
    getInvoiceDetail: vi.fn(),
    payAndDispense: vi.fn(),
    cancelInvoice: vi.fn(),
  },
}));

// Mock toast
vi.mock("react-hot-toast", () => ({
  __esModule: true,
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe("InvoiceDetailView", () => {
  const mockInvoice = {
    id: "INV-001",
    caseId: "CASE-001",
    caseName: "Nguyen Van A",
    totalAmount: 100000,
    status: "PENDING",
    createdAt: "2023-01-01T00:00:00Z",
    items: [
      { id: "1", description: "Thuốc A", quantity: 2, unitPrice: 50000, totalPrice: 100000 }
    ]
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should render invoice details successfully", async () => {
    (invoiceService.getInvoiceDetail as Mock).mockResolvedValue(mockInvoice);
    render(<InvoiceDetailView invoiceId="INV-001" />);

    expect(screen.getByText("Đang tải dữ liệu hóa đơn...")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Nguyen Van A")).toBeInTheDocument();
      expect(screen.getByText("Chờ thanh toán")).toBeInTheDocument();
    });
  });

  it("should handle cancel invoice successfully", async () => {
    (invoiceService.getInvoiceDetail as Mock).mockResolvedValue(mockInvoice);
    (invoiceService.cancelInvoice as Mock).mockResolvedValue({});

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() => {
      expect(screen.getByText("Hủy Hóa Đơn")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Hủy Hóa Đơn"));

    // Modal opens
    expect(screen.getByText("Xác Nhận Hủy Hóa Đơn")).toBeInTheDocument();
    
    const reasonInput = screen.getByPlaceholderText("Nhập lý do hủy hóa đơn...");
    fireEvent.change(reasonInput, { target: { value: "Sai thuốc" } });
    
    fireEvent.click(screen.getByText("Xác nhận Hủy"));

    await waitFor(() => {
      expect(invoiceService.cancelInvoice).toHaveBeenCalledWith("INV-001", "Sai thuốc");
      expect(toast.success).toHaveBeenCalledWith("Hủy hóa đơn thành công.");
    });
  });

  it("should require cancel reason", async () => {
    (invoiceService.getInvoiceDetail as Mock).mockResolvedValue(mockInvoice);

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() => {
      expect(screen.getByText("Hủy Hóa Đơn")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Hủy Hóa Đơn"));

    // Modal opens
    expect(screen.getByText("Xác Nhận Hủy Hóa Đơn")).toBeInTheDocument();
    
    // Don't input reason, just submit
    fireEvent.click(screen.getByText("Xác nhận Hủy"));

    await waitFor(() => {
      expect(invoiceService.cancelInvoice).not.toHaveBeenCalled();
      expect(toast.error).toHaveBeenCalledWith("Vui lòng nhập lý do hủy hóa đơn.");
    });
  });
});
