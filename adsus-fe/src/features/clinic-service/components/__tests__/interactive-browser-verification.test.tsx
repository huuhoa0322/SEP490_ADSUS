import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NAV_GROUPS, visibleGroupsForRole } from "@/components/shared/nav-config";
import { ClinicServiceManagement } from "../clinic-service-management";
import { CaseClinicServicesPanel } from "../case-clinic-services-panel";
import { InvoiceDetailView } from "@/features/prescription-adherence/components/invoice-detail-view";
import type { ClinicService, CaseClinicService } from "../../types";

// --- Mock Data ---
const mockCatalogServices: ClinicService[] = [
  {
    id: "cs-001",
    code: "GENERAL_EXAM",
    name: "Khám thường",
    description: "Khám lâm sàng tổng quát ban đầu",
    price: 100000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
  {
    id: "cs-002",
    code: "ULTRASOUND_EXAM",
    name: "Khám siêu âm",
    description: "Siêu âm phụ khoa bằng đầu dò",
    price: 200000,
    isActive: true,
    createdAt: "2026-09-11T00:00:00Z",
    updatedAt: "2026-09-11T00:00:00Z",
  },
  {
    id: "cs-003",
    code: "DEACTIVATED_SERVICE",
    name: "Dịch vụ đã ngưng",
    description: "Dịch vụ ngưng cung cấp",
    price: 150000,
    isActive: false,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
  },
];

const mockCaseServices: CaseClinicService[] = [
  {
    id: "ccs-001",
    caseId: "case-999",
    clinicServiceId: "cs-001",
    serviceName: "Khám thường",
    serviceCode: "GENERAL_EXAM",
    priceAtTime: 100000,
    createdAt: "2026-09-11T08:00:00Z",
  },
];

const mockInvoiceWithMixedItems = {
  id: "INV-999",
  caseId: "case-999",
  caseName: "Bệnh nhân Test",
  totalAmount: 350000,
  status: "PENDING" as const,
  createdAt: "2026-09-11T08:30:00Z",
  items: [
    {
      id: "item-1",
      description: "Khám thường (Dịch vụ)",
      quantity: 1,
      unitPrice: 100000,
      totalPrice: 100000,
      itemType: "SERVICE",
    },
    {
      id: "item-2",
      description: "Khám siêu âm (Dịch vụ)",
      quantity: 1,
      unitPrice: 200000,
      totalPrice: 200000,
      itemType: "SERVICE",
    },
    {
      id: "item-3",
      description: "Paracetamol 500mg",
      quantity: 10,
      unitPrice: 5000,
      totalPrice: 50000,
      itemType: "MEDICINE",
    },
  ],
};

// --- Hoisted Mock Functions ---
const {
  mockUseClinicServicesList,
  mockCreateMutate,
  mockUpdateMutate,
  mockDeactivateMutate,
  mockUseCaseClinicServices,
  mockAddCaseClinicServiceMutate,
  mockRemoveCaseClinicServiceMutate,
  mockUseCaseInvoices,
  mockUseInvoiceDetail,
} = vi.hoisted(() => ({
  mockUseClinicServicesList: vi.fn(),
  mockCreateMutate: vi.fn(),
  mockUpdateMutate: vi.fn(),
  mockDeactivateMutate: vi.fn(),
  mockUseCaseClinicServices: vi.fn(),
  mockAddCaseClinicServiceMutate: vi.fn(),
  mockRemoveCaseClinicServiceMutate: vi.fn(),
  mockUseCaseInvoices: vi.fn(),
  mockUseInvoiceDetail: vi.fn(),
}));

vi.mock("../../queries", () => ({
  useClinicServicesList: (isActive?: boolean) => mockUseClinicServicesList(isActive),
  useCreateClinicService: () => ({ mutate: mockCreateMutate, isPending: false }),
  useUpdateClinicService: () => ({ mutate: mockUpdateMutate, isPending: false }),
  useDeactivateClinicService: () => ({ mutate: mockDeactivateMutate, isPending: false }),
  useCaseClinicServices: (_caseId: string) => mockUseCaseClinicServices(_caseId),
  useAddCaseClinicService: (_caseId: string) => ({
    mutate: (dto: string, opts?: { onSuccess?: () => void }) => {
      mockAddCaseClinicServiceMutate(dto);
      opts?.onSuccess?.();
    },
    isPending: false,
  }),
  useRemoveCaseClinicService: (_caseId: string) => ({
    mutate: (id: string, opts?: { onSuccess?: () => void }) => {
      mockRemoveCaseClinicServiceMutate(id);
      opts?.onSuccess?.();
    },
    isPending: false,
  }),
}));

vi.mock("@/features/prescription-adherence/hooks/use-invoices", () => ({
  useCaseInvoices: (caseId?: string) => mockUseCaseInvoices(caseId),
  useInvoiceDetail: (id: string) => mockUseInvoiceDetail(id),
  usePayInvoice: () => ({ mutate: vi.fn(), isPending: false }),
  useCancelInvoice: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/store/auth-store", () => ({
  useAuthStore: vi.fn((selector) => {
    const state = {
      user: { role: "STAFF", userId: "staff-1", fullName: "Nhân viên Thu ngân" },
    };
    return selector ? selector(state) : state;
  }),
}));

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe("Interactive Browser Verification: Clinic Service Workflows", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    mockUseClinicServicesList.mockImplementation((isActive?: boolean) => {
      if (isActive === true) {
        return { data: mockCatalogServices.filter((s) => s.isActive), isLoading: false };
      }
      if (isActive === false) {
        return { data: mockCatalogServices.filter((s) => !s.isActive), isLoading: false };
      }
      return { data: mockCatalogServices, isLoading: false };
    });

    mockUseCaseClinicServices.mockReturnValue({
      data: mockCaseServices,
      isLoading: false,
    });

    mockUseCaseInvoices.mockReturnValue({
      data: [{ status: "PENDING" }],
      isLoading: false,
    });

    mockUseInvoiceDetail.mockReturnValue({
      data: mockInvoiceWithMixedItems,
      isLoading: false,
    });
  });

  describe("1. Admin Navigation Configuration", () => {
    it("configures 'Dịch vụ phòng khám' under 'Hệ thống' with Stethoscope icon and ADMIN role", () => {
      const heThongGroup = NAV_GROUPS.find((g) => g.label === "Hệ thống");
      expect(heThongGroup).toBeDefined();

      const clinicServiceItem = heThongGroup?.items.find(
        (item) => item.href === "/admin/clinic-services",
      );
      expect(clinicServiceItem).toBeDefined();
      expect(clinicServiceItem?.title).toBe("Dịch vụ phòng khám");
      expect(clinicServiceItem?.roles).toContain("ADMIN");
      expect(clinicServiceItem?.icon).toBeDefined();

      // Check role visibility
      const adminGroups = visibleGroupsForRole("ADMIN");
      const adminHeThong = adminGroups.find((g) => g.label === "Hệ thống");
      const adminHasItem = adminHeThong?.items.some((i) => i.href === "/admin/clinic-services");
      expect(adminHasItem).toBe(true);

      const doctorGroups = visibleGroupsForRole("DOCTOR");
      const doctorHeThong = doctorGroups.find((g) => g.label === "Hệ thống");
      const doctorHasItem = doctorHeThong?.items.some((i) => i.href === "/admin/clinic-services");
      expect(doctorHasItem).toBeFalsy();
    });
  });

  describe("2. Admin Management Page Interactive Verification", () => {
    it("renders page header, search input, filter tabs, and full table columns", () => {
      renderWithClient(<ClinicServiceManagement />);

      // Title and Description
      expect(screen.getByText("Quản lý dịch vụ phòng khám")).toBeInTheDocument();
      expect(
        screen.getByText(/Cấu hình danh mục dịch vụ khám và đơn giá áp dụng/i),
      ).toBeInTheDocument();

      // Filter tabs
      expect(screen.getByRole("button", { name: "Tất cả" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Hoạt động" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Ngưng hoạt động" })).toBeInTheDocument();

      // Table headers
      expect(screen.getByText("Mã dịch vụ")).toBeInTheDocument();
      expect(screen.getByText("Tên dịch vụ")).toBeInTheDocument();
      expect(screen.getByText("Mô tả")).toBeInTheDocument();
      expect(screen.getByText("Đơn giá")).toBeInTheDocument();
      expect(screen.getByText("Trạng thái")).toBeInTheDocument();
      expect(screen.getByText("Thao tác")).toBeInTheDocument();

      // Rows and formatted currency
      expect(screen.getByText("GENERAL_EXAM")).toBeInTheDocument();
      expect(screen.getByText("Khám thường")).toBeInTheDocument();
      expect(screen.getByText("ULTRASOUND_EXAM")).toBeInTheDocument();
      expect(screen.getByText("Khám siêu âm")).toBeInTheDocument();
      expect(screen.getByText("Dịch vụ đã ngưng")).toBeInTheDocument();
    });

    it("interactively triggers Create Service modal and validates required fields", async () => {
      renderWithClient(<ClinicServiceManagement />);

      const addBtn = screen.getByTestId("btn-add-service");
      expect(addBtn).toBeInTheDocument();
      fireEvent.click(addBtn);

      await waitFor(() => {
        expect(screen.getByText("Thêm dịch vụ phòng khám mới")).toBeInTheDocument();
      });

      // Submit without input -> should trigger validation errors
      const submitBtn = screen.getByRole("button", { name: "Thêm dịch vụ" });
      fireEvent.click(submitBtn);

      await waitFor(() => {
        expect(screen.getByText("Mã dịch vụ không được để trống.")).toBeInTheDocument();
        expect(screen.getByText("Tên dịch vụ không được để trống.")).toBeInTheDocument();
      });

      // Fill in valid data and submit
      const codeInput = screen.getByPlaceholderText(/Ví dụ: GENERAL_EXAM/i);
      const nameInput = screen.getByPlaceholderText(/Ví dụ: Khám tổng quát/i);
      const priceInput = screen.getByPlaceholderText("100000");

      fireEvent.change(codeInput, { target: { value: "BLOOD_TEST" } });
      fireEvent.change(nameInput, { target: { value: "Xét nghiệm máu" } });
      fireEvent.change(priceInput, { target: { value: "150000" } });

      fireEvent.click(submitBtn);

      expect(mockCreateMutate).toHaveBeenCalledWith(
        {
          code: "BLOOD_TEST",
          name: "Xét nghiệm máu",
          price: 150000,
          description: null,
        },
        expect.anything(),
      );
    });

    it("interactively opens Edit Service modal with disabled code input", async () => {
      renderWithClient(<ClinicServiceManagement />);

      const editButtons = screen.getAllByRole("button", { name: /sửa/i });
      expect(editButtons.length).toBeGreaterThan(0);
      fireEvent.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.getByText("Chỉnh sửa dịch vụ phòng khám")).toBeInTheDocument();
      });

      // Code field must be disabled in edit mode
      const codeInput = screen.getByPlaceholderText(/Ví dụ: GENERAL_EXAM/i);
      expect(codeInput).toBeDisabled();
      expect(
        screen.getByText("Mã dịch vụ là định danh duy nhất và không thể thay đổi sau khi tạo."),
      ).toBeInTheDocument();

      // Submit changes
      const saveBtn = screen.getByRole("button", { name: "Lưu thay đổi" });
      fireEvent.click(saveBtn);

      expect(mockUpdateMutate).toHaveBeenCalled();
    });

    it("interactively triggers Deactivation confirm dialog and executes soft delete", async () => {
      renderWithClient(<ClinicServiceManagement />);

      const deactivateButtons = screen.getAllByTestId("btn-deactivate-service");
      expect(deactivateButtons.length).toBeGreaterThan(0);
      fireEvent.click(deactivateButtons[0]);

      await waitFor(() => {
        expect(screen.getByText("Xác nhận ngưng hoạt động")).toBeInTheDocument();
        expect(
          screen.getByText(/Bạn có chắc chắn muốn ngưng hoạt động dịch vụ/i),
        ).toBeInTheDocument();
      });

      // Click confirm deactivation
      const confirmDeactivateBtn = screen.getByRole("button", { name: "Ngưng hoạt động" });
      fireEvent.click(confirmDeactivateBtn);

      expect(mockDeactivateMutate).toHaveBeenCalledWith("cs-001", expect.anything());
    });
  });

  describe("3. Case Detail Panel Interactive Verification", () => {
    it("renders panel header, attached service rows, price snapshot, and total price summary", () => {
      renderWithClient(<CaseClinicServicesPanel caseId="case-999" caseStatus="IN_PROGRESS" />);

      expect(screen.getByText("Dịch vụ khám")).toBeInTheDocument();
      expect(screen.getByText("Các dịch vụ y tế và thủ thuật áp dụng cho ca khám này")).toBeInTheDocument();

      // Service row details
      expect(screen.getByText("GENERAL_EXAM")).toBeInTheDocument();
      expect(screen.getByText("Khám thường")).toBeInTheDocument();
      expect(screen.getByText("Tổng tiền dịch vụ:")).toBeInTheDocument();
    });

    it("interactively opens Add Service to Case dialog and submits selection", async () => {
      renderWithClient(<CaseClinicServicesPanel caseId="case-999" caseStatus="IN_PROGRESS" />);

      const addBtn = screen.getByRole("button", { name: /thêm dịch vụ/i });
      fireEvent.click(addBtn);

      await waitFor(() => {
        expect(screen.getByText("Thêm dịch vụ vào ca khám")).toBeInTheDocument();
      });

      // cs-002 (ULTRASOUND_EXAM) is active and not attached yet
      expect(screen.getByText(/Khám siêu âm/i)).toBeInTheDocument();

      const submitBtn = screen.getByRole("button", { name: "Thêm vào ca khám" });
      fireEvent.click(submitBtn);

      expect(mockAddCaseClinicServiceMutate).toHaveBeenCalledWith("cs-002");
    });

    it("interactively opens Delete Service confirm dialog and submits removal", async () => {
      renderWithClient(<CaseClinicServicesPanel caseId="case-999" caseStatus="IN_PROGRESS" />);

      const deleteBtn = screen.getByTitle("Xóa dịch vụ này khỏi ca khám");
      fireEvent.click(deleteBtn);

      await waitFor(() => {
        expect(screen.getByText("Xác nhận xóa dịch vụ")).toBeInTheDocument();
        expect(
          screen.getByText(/Bạn có chắc chắn muốn xóa dịch vụ/i),
        ).toBeInTheDocument();
      });

      const confirmDeleteBtn = screen.getByRole("button", { name: "Xóa dịch vụ" });
      fireEvent.click(confirmDeleteBtn);

      expect(mockRemoveCaseClinicServiceMutate).toHaveBeenCalledWith("ccs-001");
    });

    it("enforces PAID invoice guard: displays alert banner and disables trash button", () => {
      mockUseCaseInvoices.mockReturnValue({
        data: [{ status: "PAID" }],
        isLoading: false,
      });

      renderWithClient(<CaseClinicServicesPanel caseId="case-999" caseStatus="IN_PROGRESS" />);

      expect(
        screen.getByText(/Hóa đơn cho ca khám này đã được thanh toán\. Không thể xóa hoặc thay đổi dịch vụ đã áp dụng\./i),
      ).toBeInTheDocument();

      const disabledDeleteBtn = screen.getByTitle("Không thể xóa dịch vụ vì hóa đơn đã được thanh toán.");
      expect(disabledDeleteBtn).toBeDisabled();
    });

    it("enforces closed case guard (END / CANCELLED): hides add button and disables deletion", () => {
      renderWithClient(<CaseClinicServicesPanel caseId="case-999" caseStatus="END" />);

      expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();

      const deleteBtn = screen.getByTitle("Không thể xóa dịch vụ khi ca khám đã kết thúc hoặc đã hủy.");
      expect(deleteBtn).toBeDisabled();
    });

    it("enforces waiting check-in guard (BOOKED): hides add button and disables deletion with alert banner", () => {
      renderWithClient(<CaseClinicServicesPanel caseId="case-999" caseStatus="BOOKED" />);

      expect(screen.queryByRole("button", { name: /thêm dịch vụ/i })).not.toBeInTheDocument();
      expect(
        screen.getByText(/Ca khám đang chờ check-in\. Không thể thêm hoặc xóa dịch vụ khám\./i),
      ).toBeInTheDocument();

      const deleteBtn = screen.getByTitle("Không thể xóa dịch vụ khi ca khám đang chờ check-in.");
      expect(deleteBtn).toBeDisabled();
    });
  });

  describe("4. Invoice Detail View Interactive Verification", () => {
    it("renders 'Chi tiết hóa đơn' card title and 'Loại' column with distinct service and medicine badges", () => {
      render(<InvoiceDetailView invoiceId="INV-999" />);

      // Title
      expect(screen.getByText("Chi tiết hóa đơn")).toBeInTheDocument();

      // Column Header
      expect(screen.getByText("Loại")).toBeInTheDocument();

      // Badges
      const serviceBadges = screen.getAllByText("Dịch vụ");
      expect(serviceBadges.length).toBe(2);

      const medicineBadges = screen.getAllByText("Thuốc");
      expect(medicineBadges.length).toBe(1);

      // Descriptions and Summary
      expect(screen.getByText("Khám thường (Dịch vụ)")).toBeInTheDocument();
      expect(screen.getByText("Khám siêu âm (Dịch vụ)")).toBeInTheDocument();
      expect(screen.getByText("Paracetamol 500mg")).toBeInTheDocument();
      expect(screen.getByText("Tổng cộng:")).toBeInTheDocument();
    });
  });
});
