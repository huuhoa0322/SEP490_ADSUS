import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, Mock } from "vitest";
import { InventoryAlertsList } from "@/features/medicines/components/inventory-alerts-list";
import { useInventoryAlerts } from "@/features/medicines/api/inventory.api";

vi.mock("@/features/medicines/api/inventory.api", () => ({
  useInventoryAlerts: vi.fn(),
}));

// Mock Link from next/link
vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => <a href={href}>{children}</a>,
}));

describe("InventoryAlertsList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const mockData = {
    lowStockCount: 20,
    expiringSoonCount: 0,
    expiredCount: 0,
    lowStockAlerts: Array.from({ length: 20 }, (_, i) => ({
      medicineId: `med-${i}`,
      medicineName: `Medicine ${i}`,
      currentStock: 5,
      threshold: 10,
      baseUnitName: "Viên",
      severity: "WARNING",
    })),
    expiryAlerts: [],
  };

  it("should render loading state", () => {
    (useInventoryAlerts as Mock).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    });
    render(<InventoryAlertsList />);
    expect(screen.getByText("Đang tải dữ liệu cảnh báo...")).toBeInTheDocument();
  });

  it("should render error state", () => {
    (useInventoryAlerts as Mock).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    });
    render(<InventoryAlertsList />);
    expect(screen.getByText("Lỗi khi tải dữ liệu cảnh báo.")).toBeInTheDocument();
  });

  it("should render empty state when no alerts exist", () => {
    (useInventoryAlerts as Mock).mockReturnValue({
      data: {
        lowStockCount: 0,
        expiringSoonCount: 0,
        expiredCount: 0,
        lowStockAlerts: [],
        expiryAlerts: [],
      },
      isLoading: false,
      isError: false,
    });
    render(<InventoryAlertsList />);
    expect(screen.getByText("Kho hoạt động ổn định")).toBeInTheDocument();
  });

  it("should render alerts and paginate correctly", () => {
    (useInventoryAlerts as Mock).mockReturnValue({
      data: mockData,
      isLoading: false,
      isError: false,
    });
    render(<InventoryAlertsList />);

    // Page 1 should show Med 0 to Med 14
    expect(screen.getByText("Medicine 0")).toBeInTheDocument();
    expect(screen.getByText("Medicine 14")).toBeInTheDocument();
    expect(screen.queryByText("Medicine 15")).not.toBeInTheDocument();

    // Check pagination buttons exist
    const nextBtn = screen.getByText("Sau");
    const prevBtn = screen.getByText("Trước");
    expect(prevBtn).toBeDisabled();
    expect(nextBtn).not.toBeDisabled();

    // Click next page
    fireEvent.click(nextBtn);

    // Page 2 should show Med 15 to Med 19
    expect(screen.queryByText("Medicine 0")).not.toBeInTheDocument();
    expect(screen.getByText("Medicine 15")).toBeInTheDocument();
    expect(screen.getByText("Medicine 19")).toBeInTheDocument();

    expect(prevBtn).not.toBeDisabled();
    expect(nextBtn).toBeDisabled();

    // Click page 1 directly
    const page1Btn = screen.getByText("1");
    fireEvent.click(page1Btn);

    // Back to page 1
    expect(screen.getByText("Medicine 0")).toBeInTheDocument();
    expect(screen.queryByText("Medicine 15")).not.toBeInTheDocument();
  });
});
