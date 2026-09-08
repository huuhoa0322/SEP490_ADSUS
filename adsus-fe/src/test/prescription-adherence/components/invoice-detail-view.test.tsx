import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { InvoiceDetailView } from "@/features/prescription-adherence/components/invoice-detail-view";
import toast from "react-hot-toast";

const mockInvoice = {
  id: "INV-001",
  caseId: "CASE-001",
  caseName: "Nguyen Van A",
  totalAmount: 100000,
  status: "PENDING" as const,
  createdAt: "2023-01-01T00:00:00Z",
  items: [{ id: "1", description: "Thuốc A", quantity: 2, unitPrice: 50000, totalPrice: 100000 }],
};

// Stable mutable refs so assertions see the same objects across renders
const cancelMutate = vi.fn();
const payMutate = vi.fn();
const cancelState = { mutate: cancelMutate, isPending: false };
const payState = { mutate: payMutate, isPending: false };

// Mock router
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock auth store — Nurse role
vi.mock("@/store/auth-store", () => ({
  useAuthStore: vi.fn(() => ({
    user: { role: "NURSE", userId: "nurse-1", fullName: "Nurse One", email: null, mustChangePassword: false },
    accessToken: "token",
    refreshToken: "refresh",
  })),
}));

// Mock use-invoices hooks with stable refs
vi.mock("@/features/prescription-adherence/hooks/use-invoices", () => ({
  useInvoiceDetail: vi.fn(),
  usePayInvoice: vi.fn(() => payState),
  useCancelInvoice: vi.fn(() => cancelState),
}));

// Mock toast
vi.mock("react-hot-toast", () => ({
  __esModule: true,
  default: { success: vi.fn(), error: vi.fn() },
}));

describe("InvoiceDetailView", () => {
  let useInvoiceDetail: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    vi.clearAllMocks();
    cancelMutate.mockReset();
    payMutate.mockReset();
    cancelState.isPending = false;
    payState.isPending = false;

    const mod = await vi.mocked(
      import("@/features/prescription-adherence/hooks/use-invoices"),
    );
    useInvoiceDetail = mod.useInvoiceDetail;
  });

  it("renders invoice details", async () => {
    useInvoiceDetail.mockReturnValue({ data: mockInvoice, isLoading: false });

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() => {
      expect(screen.getByText("Nguyen Van A")).toBeInTheDocument();
      expect(screen.getByText("Chờ thanh toán")).toBeInTheDocument();
    });
  });

  it("renders loading state", () => {
    useInvoiceDetail.mockReturnValue({ data: null, isLoading: true });

    render(<InvoiceDetailView invoiceId="INV-001" />);

    expect(screen.getByText("Đang tải dữ liệu hóa đơn...")).toBeInTheDocument();
  });

  it("shows cancel dialog and requires reason", async () => {
    useInvoiceDetail.mockReturnValue({ data: mockInvoice, isLoading: false });

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() => expect(screen.getByText("Hủy Hóa Đơn")).toBeInTheDocument());

    await act(async () => {
      fireEvent.click(screen.getByText("Hủy Hóa Đơn"));
    });

    await waitFor(() =>
      expect(screen.getByText("Xác Nhận Hủy Hóa Đơn")).toBeInTheDocument(),
    );

    await act(async () => {
      fireEvent.click(screen.getByText("Xác nhận Hủy"));
    });

    expect(toast.error).toHaveBeenCalledWith("Vui lòng nhập lý do hủy hóa đơn.");
    expect(cancelMutate).not.toHaveBeenCalled();
  });

  it("calls cancelInvoice with reason", async () => {
    useInvoiceDetail.mockReturnValue({ data: mockInvoice, isLoading: false });

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() => expect(screen.getByText("Hủy Hóa Đơn")).toBeInTheDocument());

    await act(async () => {
      fireEvent.click(screen.getByText("Hủy Hóa Đơn"));
    });

    await waitFor(() =>
      expect(screen.getByText("Xác Nhận Hủy Hóa Đơn")).toBeInTheDocument(),
    );

    await act(async () => {
      fireEvent.change(screen.getByPlaceholderText("Nhập lý do hủy hóa đơn..."), {
        target: { value: "Sai thuốc" },
      });
    });

    await act(async () => {
      fireEvent.click(screen.getByText("Xác nhận Hủy"));
    });

    expect(cancelMutate).toHaveBeenCalledWith(
      { id: "INV-001", reason: "Sai thuốc" },
      expect.objectContaining({ onSuccess: expect.any(Function), onError: expect.any(Function) }),
    );
  });

  it("shows case ID link for Nurse", async () => {
    useInvoiceDetail.mockReturnValue({ data: mockInvoice, isLoading: false });

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() => {
      const link = screen.getByText("CASE-001");
      expect(link).toHaveAttribute("href", "/cases/CASE-001");
    });
  });

  it("calls payInvoice with BANK_TRANSFER", async () => {
    useInvoiceDetail.mockReturnValue({ data: mockInvoice, isLoading: false });

    render(<InvoiceDetailView invoiceId="INV-001" />);

    await waitFor(() =>
      expect(screen.getByText("Đã chuyển khoản (Bank)")).toBeInTheDocument(),
    );

    await act(async () => {
      fireEvent.click(screen.getByText("Đã chuyển khoản (Bank)"));
    });

    expect(payMutate).toHaveBeenCalledWith(
      { id: "INV-001", paymentMethod: "BANK_TRANSFER" },
      expect.any(Object),
    );
  });
});
