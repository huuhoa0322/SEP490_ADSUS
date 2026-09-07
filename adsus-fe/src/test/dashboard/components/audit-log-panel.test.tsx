import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { AuditLogPanel } from "@/features/dashboard/components/audit-log-panel";
import type { AuditLogEntry } from "@/features/dashboard/types/audit-log.types";

const { getRecentAuditLogsMock } = vi.hoisted(() => ({
  getRecentAuditLogsMock: vi.fn(),
}));

vi.mock("@/features/dashboard/api/audit-log.api", () => ({
  getRecentAuditLogs: getRecentAuditLogsMock,
}));

function buildEntry(overrides: Partial<AuditLogEntry>): AuditLogEntry {
  return {
    logId: "log-1",
    actorId: "user-1",
    actorName: "Nguyễn Văn A",
    actorRole: "ADMIN",
    action: "CREATE_ACCOUNT",
    detail: "BS. Trần Văn B (0900000000, DOCTOR)",
    performedAt: "2026-07-31T10:00:00Z",
    ...overrides,
  };
}

function renderWithClient() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuditLogPanel />
    </QueryClientProvider>,
  );
}

describe("AuditLogPanel", () => {
  it("danh sách rỗng — hiện thông báo chưa có thao tác", async () => {
    getRecentAuditLogsMock.mockResolvedValue([]);

    renderWithClient();

    expect(await screen.findByText("Chưa có thao tác nào được ghi lại.")).toBeInTheDocument();
  });

  it("lỗi tải — hiện thông báo lỗi trong role alert", async () => {
    getRecentAuditLogsMock.mockRejectedValue(new Error("network down"));

    renderWithClient();

    expect(await screen.findByRole("alert")).toBeInTheDocument();
  });

  it("hành động REACTIVATE_ACCOUNT — hiện đúng nhãn tiếng Việt, không phải mã thô", async () => {
    // Hồi quy cho fix P_FE7: entry này trước đây rơi vào nhánh mặc định (mã tiếng Anh thô).
    getRecentAuditLogsMock.mockResolvedValue([
      buildEntry({ action: "REACTIVATE_ACCOUNT", detail: "khôi phục tài khoản, lý do: test" }),
    ]);

    renderWithClient();

    expect(await screen.findByText("Khôi phục tài khoản")).toBeInTheDocument();
    expect(screen.queryByText("REACTIVATE_ACCOUNT")).not.toBeInTheDocument();
  });

  it("hành động không nằm trong bảng nhãn — hiện nguyên mã thô thay vì bỏ trống", async () => {
    // Module khác (vd quản lý mô hình AI) ghi chung bảng nhật ký này với mã chưa được biết.
    getRecentAuditLogsMock.mockResolvedValue([
      buildEntry({ action: "ACTIVATE_AI_MODEL", detail: null }),
    ]);

    renderWithClient();

    expect(await screen.findByText("ACTIVATE_AI_MODEL")).toBeInTheDocument();
  });
});
