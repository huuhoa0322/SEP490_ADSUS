import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AuditLogListView } from "../components/audit-log-list-view";
import * as auditHooks from "../hooks/use-audit-logs";
import type { AuditLogEntry } from "../types/audit-logs.types";

vi.mock("../hooks/use-audit-logs", () => ({
  useAuditLogs: vi.fn(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }

  return Wrapper;
}

const SAMPLE_ENTRIES: AuditLogEntry[] = [
  {
    logId: "log-1",
    actorId: "actor-1",
    actorName: "Admin System",
    actorRole: "ADMIN",
    action: "CREATE_ACCOUNT",
    detail: "Tạo tài khoản bác sĩ BS. Hoàng Nam",
    performedAt: "2026-09-10T08:30:00Z",
  },
  {
    logId: "log-2",
    actorId: "actor-2",
    actorName: "Admin System",
    actorRole: "ADMIN",
    action: "UPDATE_ACCOUNT",
    detail: "Cập nhật số điện thoại tài khoản",
    performedAt: "2026-09-10T09:00:00Z",
  },
  {
    logId: "log-3",
    actorId: "actor-3",
    actorName: "Admin Root",
    actorRole: "ADMIN",
    action: "DEACTIVATE_ACCOUNT",
    detail: "Vô hiệu hoá tài khoản vi phạm chính sách",
    performedAt: "2026-09-10T09:15:00Z",
  },
  {
    logId: "log-4",
    actorId: "actor-4",
    actorName: "Admin Root",
    actorRole: "ADMIN",
    action: "REGISTER_AI_MODEL",
    detail: "Đăng ký mô hình AI YOLO26_v2",
    performedAt: "2026-09-10T10:00:00Z",
  },
  {
    logId: "log-5",
    actorId: "actor-5",
    actorName: "Admin Root",
    actorRole: "ADMIN",
    action: "ADMIN_RESET_PASSWORD",
    detail: "Cấp lại mật khẩu cho bác sĩ",
    performedAt: "2026-09-10T10:30:00Z",
  },
  {
    logId: "log-6",
    actorId: "actor-6",
    actorName: "", // Test null / empty actor name fallback
    actorRole: "ADMIN",
    action: "UNKNOWN_CUSTOM_ACTION",
    detail: null,
    performedAt: "2026-09-10T11:00:00Z",
  },
];

describe("AuditLogListView Component", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders container with mx-auto w-full max-w-screen-2xl px-6 py-8", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: [],
        page: 1,
        pageSize: 15,
        totalItems: 0,
        totalPages: 0,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    const { container } = render(<AuditLogListView />, { wrapper: createWrapper() });
    const mainContainer = container.querySelector(".max-w-screen-2xl");
    expect(mainContainer).toHaveClass("mx-auto", "w-full", "px-6", "py-8");
  });

  it("renders card table with overflow-x-auto rounded-3xl border border-border bg-background", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: [],
        page: 1,
        pageSize: 15,
        totalItems: 0,
        totalPages: 0,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    const { container } = render(<AuditLogListView />, { wrapper: createWrapper() });
    const tableContainer = container.querySelector(".rounded-3xl");
    expect(tableContainer).toHaveClass("overflow-x-auto", "border", "border-border", "bg-background");
  });

  it("applies first-child indentation [&>th:first-child]:pl-6 and [&>td:first-child]:pl-6", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: SAMPLE_ENTRIES,
        page: 1,
        pageSize: 15,
        totalItems: 6,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    const { container } = render(<AuditLogListView />, { wrapper: createWrapper() });
    const headerRow = container.querySelector("thead tr");
    expect(headerRow?.className).toContain("[&>th:first-child]:pl-6");

    const dataRow = container.querySelector("tbody tr");
    expect(dataRow?.className).toContain("[&>td:first-child]:pl-6");
  });

  it("renders semantic action badges: Emerald for CREATE, Neutral for UPDATE, Red for DEACTIVATE, Indigo for AI, Amber for PASSWORD", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: SAMPLE_ENTRIES,
        page: 1,
        pageSize: 15,
        totalItems: 6,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });

    // CREATE badge (Emerald)
    expect(screen.getByText("Tạo tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-create")).toBeInTheDocument();

    // UPDATE badge (Neutral)
    expect(screen.getByText("Cập nhật tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-update")).toBeInTheDocument();

    // DEACTIVATE badge (Red)
    expect(screen.getByText("Vô hiệu hoá tài khoản")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-deactivate")).toBeInTheDocument();

    // AI badge (Indigo)
    expect(screen.getByText("Đăng ký mô hình AI")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-ai")).toBeInTheDocument();

    // PASSWORD badge (Amber)
    expect(screen.getByText("Cấp lại mật khẩu")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-password")).toBeInTheDocument();

    // Fallback unknown badge
    expect(screen.getByText("UNKNOWN_CUSTOM_ACTION")).toBeInTheDocument();
    expect(screen.getByTestId("badge-icon-fallback")).toBeInTheDocument();
  });

  it("renders fallback 'Hệ thống' when actorName is missing", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: [SAMPLE_ENTRIES[5]], // empty actorName
        page: 1,
        pageSize: 15,
        totalItems: 1,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });
    expect(screen.getByText("Hệ thống")).toBeInTheDocument();
  });

  it("renders realtime search input with rounded-full h-12 and category select", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: [],
        page: 1,
        pageSize: 15,
        totalItems: 0,
        totalPages: 0,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });

    const searchInput = screen.getByTestId("audit-search-input");
    expect(searchInput).toBeInTheDocument();
    expect(searchInput).toHaveClass("rounded-full", "h-12");

    const categorySelect = screen.getByTestId("audit-category-select");
    expect(categorySelect).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Tất cả danh mục" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Quản trị tài khoản" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Mô hình AI" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Điều dưỡng & Bệnh nhân" })).toBeInTheDocument();
  });

  it("resets page to 1 when changing search keyword or category filter", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: [],
        page: 1,
        pageSize: 15,
        totalItems: 0,
        totalPages: 0,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });

    const searchInput = screen.getByTestId("audit-search-input");
    fireEvent.change(searchInput, { target: { value: "Admin" } });

    const categorySelect = screen.getByTestId("audit-category-select");
    fireEvent.change(categorySelect, { target: { value: "ACCOUNT" } });

    expect(auditHooks.useAuditLogs).toHaveBeenCalledWith(
      expect.objectContaining({
        search: "Admin",
        action: "ACCOUNT",
        page: 1,
        pageSize: 15,
      })
    );
  });

  it("displays empty state with 'Không có nhật ký nào' when items array is empty", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: [],
        page: 1,
        pageSize: 15,
        totalItems: 0,
        totalPages: 0,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });
    expect(screen.getByText("Không có nhật ký nào")).toBeInTheDocument();
  });

  it("displays error alert when query fails and allows retry", () => {
    const mockRefetch = vi.fn();
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error("Failed to fetch"),
      refetch: mockRefetch,
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });

    expect(screen.getByRole("alert")).toBeInTheDocument();
    const retryBtn = screen.getByRole("button", { name: "Thử lại" });
    fireEvent.click(retryBtn);
    expect(mockRefetch).toHaveBeenCalled();
  });

  it("renders PaginationNumbered when totalPages > 1 with exactly 15 items per page", () => {
    vi.mocked(auditHooks.useAuditLogs).mockReturnValue({
      data: {
        items: SAMPLE_ENTRIES,
        page: 2,
        pageSize: 15,
        totalItems: 45,
        totalPages: 3,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
    } as unknown as ReturnType<typeof auditHooks.useAuditLogs>);

    render(<AuditLogListView />, { wrapper: createWrapper() });

    expect(screen.getByText("Đang xem 6 / 45 kết quả")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Trước" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sau" })).toBeInTheDocument();
  });
});
