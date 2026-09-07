import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import {
  BarList,
  RateMeter,
  StatTile,
  StatusBreakdown,
} from "@/features/dashboard/components/chart-primitives";

describe("StatTile", () => {
  it("hiện label, value và hint", () => {
    render(<StatTile label="Tài khoản" value={42} hint="3 tài khoản mới" />);

    expect(screen.getByText("Tài khoản")).toBeInTheDocument();
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("3 tài khoản mới")).toBeInTheDocument();
  });
});

describe("BarList", () => {
  it("rỗng — hiện thông báo chưa có dữ liệu thay vì danh sách trống", () => {
    render(<BarList items={[{ label: "Bác sĩ", value: 0 }, { label: "Điều dưỡng", value: 0 }]} />);

    expect(screen.getByText("Chưa có dữ liệu")).toBeInTheDocument();
  });

  it("có dữ liệu — hiện đủ nhãn và số của từng dòng", () => {
    render(
      <BarList
        items={[
          { label: "Bác sĩ", value: 8 },
          { label: "Bệnh nhân", value: 29 },
        ]}
      />,
    );

    expect(screen.getByText("Bác sĩ")).toBeInTheDocument();
    expect(screen.getByText("8")).toBeInTheDocument();
    expect(screen.getByText("Bệnh nhân")).toBeInTheDocument();
    expect(screen.getByText("29")).toBeInTheDocument();
  });
});

describe("StatusBreakdown", () => {
  it("tổng bằng 0 — hiện thông báo trống thay vì chia cho 0", () => {
    render(
      <StatusBreakdown
        segments={[
          { label: "Đang hoạt động", value: 0, tone: "good" },
          { label: "Đã vô hiệu hoá", value: 0, tone: "critical" },
        ]}
      />,
    );

    expect(screen.getByText("Chưa có dữ liệu trong khoảng thời gian này")).toBeInTheDocument();
  });

  it("tính đúng phần trăm từng segment", () => {
    render(
      <StatusBreakdown
        segments={[
          { label: "Đang hoạt động", value: 9, tone: "good" },
          { label: "Đã vô hiệu hoá", value: 1, tone: "critical" },
        ]}
      />,
    );

    expect(screen.getByText("90%")).toBeInTheDocument();
    expect(screen.getByText("10%")).toBeInTheDocument();
  });
});

describe("RateMeter", () => {
  it("hiện đúng giá trị phần trăm và caption", () => {
    render(<RateMeter value={75} caption="15 trên 20 liều" />);

    expect(screen.getByText("75")).toBeInTheDocument();
    expect(screen.getByText("%")).toBeInTheDocument();
    expect(screen.getByText("15 trên 20 liều")).toBeInTheDocument();
  });
});
