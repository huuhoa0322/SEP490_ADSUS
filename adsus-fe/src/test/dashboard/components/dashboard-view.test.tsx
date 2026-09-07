import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { DashboardView } from "@/features/dashboard/components/dashboard-view";
import type { DashboardStatistics } from "@/features/dashboard/types/dashboard.types";

const { useDashboardStatisticsMock } = vi.hoisted(() => ({
  useDashboardStatisticsMock: vi.fn(),
}));

vi.mock("@/features/dashboard/hooks/use-dashboard", () => ({
  useDashboardStatistics: useDashboardStatisticsMock,
}));

// AuditLogPanel có bài test riêng (audit-log-panel.test.tsx) — cô lập ở đây để
// DashboardView chỉ kiểm đúng phần thuộc về chính nó.
vi.mock("@/features/dashboard/components/audit-log-panel", () => ({
  AuditLogPanel: () => <div data-testid="audit-log-panel-stub" />,
}));

function buildStatistics(overrides: Partial<DashboardStatistics> = {}): DashboardStatistics {
  return {
    fromDate: "2026-07-01",
    toDate: "2026-07-31",
    accounts: {
      total: 42, adminCount: 3, doctorCount: 8, nurseCount: 2, patientCount: 29,
      activeCount: 40, deactivatedCount: 2, newInRange: 3, activeRate: 95.2,
    },
    clinical: { caseCount: 11, aiRunCount: 0, aiConfirmedCount: 0, aiRejectedCount: 0, aiPendingCount: 0, aiConfirmRate: 0 },
    appointments: { bookedCount: 3, cancelledCount: 10, slotCount: 826, cancellationRate: 76.9 },
    adherence: { scheduledDoseCount: 104, takenDoseCount: 1, adherenceRate: 1 },
    activeAiModel: { versionCode: "YOLO26_EffNetV2S_BFV2_512", precision: 1, recall: 1, map50: 100 },
    trend: [],
    ...overrides,
  };
}

describe("DashboardView — trạng thái tải", () => {
  it("đang tải, chưa có dữ liệu — hiện spinner, chưa hiện số liệu", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: undefined, isLoading: true, isError: false, error: null,
    });

    render(<DashboardView />);

    expect(screen.queryByText("Tài khoản")).not.toBeInTheDocument();
  });

  it("lỗi tải — hiện thông báo lỗi trong role alert", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: undefined, isLoading: false, isError: true, error: new Error("network down"),
    });

    render(<DashboardView />);

    expect(screen.getByRole("alert")).toBeInTheDocument();
  });
});

describe("DashboardView — hiện số liệu", () => {
  it("hiện đủ 4 KPI chính từ dữ liệu API", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: buildStatistics(), isLoading: false, isError: false, error: null,
    });

    render(<DashboardView />);

    expect(screen.getByText("42")).toBeInTheDocument(); // Tài khoản
    expect(screen.getByText("11")).toBeInTheDocument(); // Ca khám
    expect(screen.getByText("104")).toBeInTheDocument(); // Liều thuốc theo dõi
  });

  it("hiện card Trạng thái tài khoản với đủ Active/Deactivated (P11 fix)", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: buildStatistics(), isLoading: false, isError: false, error: null,
    });

    render(<DashboardView />);

    expect(screen.getByText("Trạng thái tài khoản")).toBeInTheDocument();
    expect(screen.getByText("Đang hoạt động")).toBeInTheDocument();
    expect(screen.getByText("Đã vô hiệu hoá")).toBeInTheDocument();
  });

  it("nhúng AuditLogPanel bên dưới các biểu đồ", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: buildStatistics(), isLoading: false, isError: false, error: null,
    });

    render(<DashboardView />);

    expect(screen.getByTestId("audit-log-panel-stub")).toBeInTheDocument();
  });

  it("Active AI Model chưa có số liệu — hiện 'Chưa có dữ liệu' thay vì NaN/undefined", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: buildStatistics({
        activeAiModel: { versionCode: "" },
      }),
      isLoading: false, isError: false, error: null,
    });

    render(<DashboardView />);

    // Nội dung tách qua <br />, nên khớp theo textContent gộp thay vì chuỗi nguyên văn.
    expect(
      screen.getAllByText((_, element) => element?.textContent === "Chưa códữ liệu").length,
    ).toBeGreaterThan(0);
  });
});

describe("DashboardView — bộ lọc thời gian", () => {
  it("bấm preset '7 ngày' — gọi lại hook với khoảng 7 ngày mới", () => {
    useDashboardStatisticsMock.mockReturnValue({
      data: buildStatistics(), isLoading: false, isError: false, error: null,
    });

    render(<DashboardView />);
    useDashboardStatisticsMock.mockClear();

    fireEvent.click(screen.getByRole("button", { name: "7 ngày" }));

    expect(useDashboardStatisticsMock).toHaveBeenCalled();
    const lastCallArg = useDashboardStatisticsMock.mock.calls.at(-1)?.[0];
    expect(lastCallArg).toHaveProperty("fromDate");
    expect(lastCallArg).toHaveProperty("toDate");
  });
});
