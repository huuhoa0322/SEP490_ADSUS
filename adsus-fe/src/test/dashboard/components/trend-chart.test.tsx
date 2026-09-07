import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { TrendChart } from "@/features/dashboard/components/trend-chart";
import type { DailyPoint } from "@/features/dashboard/types/dashboard.types";

function buildPoint(overrides: Partial<DailyPoint>): DailyPoint {
  return { date: "2026-07-01", newAccounts: 0, cases: 0, appointments: 0, ...overrides };
}

describe("TrendChart", () => {
  it("mảng rỗng — hiện thông báo chưa có dữ liệu, không vẽ SVG", () => {
    render(<TrendChart points={[]} measure="newAccounts" label="Tài khoản mới" />);

    expect(screen.getByText("Chưa có dữ liệu trong khoảng thời gian này")).toBeInTheDocument();
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });

  it("toàn bộ điểm đều là 0 — hiện thông báo trống (AF-01, không vẽ đường phẳng gây hiểu nhầm)", () => {
    render(
      <TrendChart
        points={[buildPoint({ date: "2026-07-01" }), buildPoint({ date: "2026-07-02" })]}
        measure="cases"
        label="Ca khám"
      />,
    );

    expect(screen.getByText("Chưa có dữ liệu trong khoảng thời gian này")).toBeInTheDocument();
  });

  it("có dữ liệu — hiện tổng, giá trị cao nhất/ngày, và ngày đầu/cuối", () => {
    render(
      <TrendChart
        points={[
          buildPoint({ date: "2026-07-01", newAccounts: 2 }),
          buildPoint({ date: "2026-07-02", newAccounts: 5 }),
          buildPoint({ date: "2026-07-03", newAccounts: 1 }),
        ]}
        measure="newAccounts"
        label="Tài khoản mới"
      />,
    );

    expect(screen.getByRole("img", { name: /tổng 8 trong kỳ, cao nhất 5 một ngày/ })).toBeInTheDocument();
    expect(screen.getByText("2026-07-01")).toBeInTheDocument();
    expect(screen.getByText("2026-07-03")).toBeInTheDocument();
  });
});
