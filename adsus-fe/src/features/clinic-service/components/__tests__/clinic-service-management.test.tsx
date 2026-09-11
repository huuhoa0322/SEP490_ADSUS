import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ClinicServiceManagement } from "../clinic-service-management";
import type { ClinicService } from "../../types";

const mockServices: ClinicService[] = [
  {
    id: "cs-1",
    code: "GENERAL_EXAM",
    name: "Khám thường",
    description: "Khám lâm sàng tổng quát ban đầu",
    price: 100000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
  {
    id: "cs-2",
    code: "ULTRASOUND_EXAM",
    name: "Khám siêu âm",
    description: "Siêu âm phụ khoa bằng đầu dò",
    price: 200000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
  {
    id: "cs-3",
    code: "OLD_SERVICE",
    name: "Dịch vụ cũ ngừng kinh doanh",
    description: "Không còn phục vụ",
    price: 50000,
    isActive: false,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
  },
];

const { mockUseClinicServicesList, mockDeactivateMutate, mockUpdateMutate } = vi.hoisted(() => ({
  mockUseClinicServicesList: vi.fn(),
  mockDeactivateMutate: vi.fn(),
  mockUpdateMutate: vi.fn(),
}));

vi.mock("../../queries", () => ({
  useClinicServicesList: (isActive?: boolean) => mockUseClinicServicesList(isActive),
  useDeactivateClinicService: () => ({
    mutate: mockDeactivateMutate,
    isPending: false,
  }),
  useUpdateClinicService: () => ({
    mutate: mockUpdateMutate,
    isPending: false,
  }),
  useCreateClinicService: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe("ClinicServiceManagement", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseClinicServicesList.mockImplementation((isActive?: boolean) => {
      if (isActive === true) {
        return { data: mockServices.filter((s) => s.isActive), isLoading: false };
      }
      if (isActive === false) {
        return { data: mockServices.filter((s) => !s.isActive), isLoading: false };
      }
      return { data: mockServices, isLoading: false };
    });
  });

  it("renders service table with all columns and active badges", () => {
    renderWithClient(<ClinicServiceManagement />);

    expect(screen.getByText("Quản lý dịch vụ phòng khám")).toBeInTheDocument();
    expect(screen.getByText("GENERAL_EXAM")).toBeInTheDocument();
    expect(screen.getByText("Khám thường")).toBeInTheDocument();
    expect(screen.getByText("ULTRASOUND_EXAM")).toBeInTheDocument();
    expect(screen.getByText("Khám siêu âm")).toBeInTheDocument();
    expect(screen.getByText("Dịch vụ cũ ngừng kinh doanh")).toBeInTheDocument();

    const activeTexts = screen.getAllByText("Hoạt động");
    expect(activeTexts.length).toBe(3); // 1 filter tab + 2 table row badges

    const inactiveTexts = screen.getAllByText("Ngưng hoạt động");
    expect(inactiveTexts.length).toBe(2); // 1 filter tab + 1 table row badge
  });

  it("filters services by search query", () => {
    renderWithClient(<ClinicServiceManagement />);

    const searchInput = screen.getByPlaceholderText(/Tìm theo mã hoặc tên dịch vụ/i);
    fireEvent.change(searchInput, { target: { value: "siêu âm" } });

    expect(screen.getByText("Khám siêu âm")).toBeInTheDocument();
    expect(screen.queryByText("Khám thường")).not.toBeInTheDocument();
    expect(screen.queryByText("Dịch vụ cũ ngừng kinh doanh")).not.toBeInTheDocument();
  });

  it("opens create modal when clicking Thêm dịch vụ", async () => {
    renderWithClient(<ClinicServiceManagement />);

    const addButton = screen.getByTestId("btn-add-service");
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(screen.getByText("Thêm dịch vụ phòng khám mới")).toBeInTheDocument();
    });
  });

  it("opens confirm dialog when clicking Ngưng on an active service", async () => {
    renderWithClient(<ClinicServiceManagement />);

    const deactivateButtons = screen.getAllByTestId("btn-deactivate-service");
    fireEvent.click(deactivateButtons[0]);

    await waitFor(() => {
      expect(screen.getByText("Xác nhận ngưng hoạt động")).toBeInTheDocument();
      expect(screen.getByText(/Hành động này sẽ đánh dấu dịch vụ là ngưng hoạt động/i)).toBeInTheDocument();
    });
  });
});
