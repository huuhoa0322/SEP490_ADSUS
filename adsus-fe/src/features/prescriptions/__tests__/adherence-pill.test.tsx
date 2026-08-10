import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import "@testing-library/jest-dom/vitest";

import { AdherencePill } from "../components/adherence-pill";

/**
 * AdherencePill — hiển thị tỉ lệ tuân thủ thuốc của bệnh nhân.
 *
 * Quy tắc nghiệp vụ (CLAUDE.md §11.3.4):
 *   - adherence ≥ 80%  → tuân thủ tốt  (variant "good")
 *   - adherence < 80%  → cần hỗ trợ    (variant "warn")
 *   - KHÔNG dùng màu đỏ / destructive cho adherence thấp — chỉ amber.
 *     Màu đỏ dành cho safety card, validation error, "không được".
 *
 * Giá trị không xác định (null/NaN) → variant "unknown" (màu muted).
 */
describe("AdherencePill", () => {
  it("hiển thị phần trăm khi adherence = 85%", () => {
    render(<AdherencePill percent={85} />);
    expect(screen.getByText("85%")).toBeInTheDocument();
  });

  it("làm tròn phần trăm đến số nguyên", () => {
    render(<AdherencePill percent={79.6} />);
    expect(screen.getByText("80%")).toBeInTheDocument();
  });

  it("hiển thị variant 'good' khi adherence ≥ 80%", () => {
    const { container } = render(<AdherencePill percent={80} />);
    const pill = container.querySelector("[data-adid]")!;
    expect(pill.getAttribute("data-adid")).toBe("good");
  });

  it("hiển thị variant 'warn' khi adherence < 80%", () => {
    const { container } = render(<AdherencePill percent={79} />);
    const pill = container.querySelector("[data-adid]")!;
    expect(pill.getAttribute("data-adid")).toBe("warn");
  });

  it("KHÔNG dùng variant destructive khi adherence = 0%", () => {
    const { container } = render(<AdherencePill percent={0} />);
    const pill = container.querySelector("[data-adid]")!;
    expect(pill.getAttribute("data-adid")).not.toBe("destructive");
    expect(pill.getAttribute("data-adid")).toBe("warn");
  });

  it("hiển thị variant 'unknown' khi percent = null", () => {
    const { container } = render(<AdherencePill percent={null} />);
    const pill = container.querySelector("[data-adid]")!;
    expect(pill.getAttribute("data-adid")).toBe("unknown");
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("chấp nhận label tuỳ biến (vd: 'tuần này', 'tháng này')", () => {
    render(<AdherencePill percent={90} label="tuần này" />);
    expect(screen.getByText(/tuần này/i)).toBeInTheDocument();
  });
});