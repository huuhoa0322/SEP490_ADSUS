import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ServerStatusBadge } from "@/features/auth/components/server-status-badge";

const { hookState } = vi.hoisted(() => ({
  hookState: { isPending: true, isSuccess: false, isError: false },
}));

vi.mock("@/features/auth/hooks/use-backend-health", () => ({
  useBackendHealth: () => ({ ...hookState }),
}));

describe("ServerStatusBadge", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    hookState.isPending = true;
    hookState.isSuccess = false;
    hookState.isError = false;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("mới vào — đang kiểm tra máy chủ, chưa nói tới việc đánh thức", () => {
    render(<ServerStatusBadge />);

    expect(screen.getByText("Đang kiểm tra máy chủ...")).toBeInTheDocument();
  });

  it("pending quá 3s — đổi sang câu giải thích đang chờ kết nối", () => {
    render(<ServerStatusBadge />);

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(
      screen.getByText(/vui lòng đợi trong giây lát/i),
    ).toBeInTheDocument();
    expect(screen.queryByText("Đang kiểm tra máy chủ...")).not.toBeInTheDocument();
  });

  it("lỗi — báo không kết nối được, không quan tâm còn đang pending hay không", () => {
    hookState.isError = true;

    render(<ServerStatusBadge />);

    expect(screen.getByText("Không kết nối được máy chủ")).toBeInTheDocument();
  });

  it("thành công — báo đã kết nối, rồi tự ẩn sau 2.5s", () => {
    hookState.isPending = false;
    hookState.isSuccess = true;

    render(<ServerStatusBadge />);
    expect(screen.getByText("Đã kết nối đến máy chủ")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(2500);
    });

    expect(screen.queryByText("Đã kết nối đến máy chủ")).not.toBeInTheDocument();
  });

  it("đã hết pending, không lỗi, không thành công — không hiện gì (component trả null)", () => {
    hookState.isPending = false;
    hookState.isSuccess = false;
    hookState.isError = false;

    const { container } = render(<ServerStatusBadge />);

    expect(container).toBeEmptyDOMElement();
  });
});
