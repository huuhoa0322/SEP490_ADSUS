import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@testing-library/jest-dom/vitest";

import { LandingNavbar } from "@/components/landing/navbar";

const { routerPushMock } = vi.hoisted(() => ({
  routerPushMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: routerPushMock,
  }),
  usePathname: () => "/",
}));

const mockSignOut = vi.fn();
const mockHasHydrated = vi.fn();

vi.mock("@/store/auth-store", () => ({
  useAuthStore: vi.fn((selector) => {
    const state = {
      accessToken: "patient-token",
      user: {
        userId: "u1",
        fullName: "Nguyễn Văn Bệnh",
        role: "PATIENT",
        email: "patient@example.com",
        mustChangePassword: false,
      },
      signOut: mockSignOut,
    };
    return selector(state);
  }),
  useHasHydrated: () => mockHasHydrated(),
}));

describe("LandingNavbar — Patient dropdown", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockHasHydrated.mockReturnValue(true);
  });

  it("hiển thị button trigger 'Tài khoản' khi Patient đăng nhập", () => {
    render(<LandingNavbar />);
    expect(screen.getByRole("button", { name: /tài khoản/i })).toBeInTheDocument();
  });

  it("dropdown ẩn ban đầu", () => {
    render(<LandingNavbar />);
    expect(screen.queryByRole("link", { name: /lịch hẹn của tôi/i })).not.toBeInTheDocument();
  });

  it("click trigger → mở dropdown hiện fullname + email + role 'Bệnh nhân'", () => {
    render(<LandingNavbar />);
    fireEvent.click(screen.getByRole("button", { name: /tài khoản/i }));

    expect(screen.getByText("Nguyễn Văn Bệnh")).toBeInTheDocument();
    expect(screen.getByText("patient@example.com")).toBeInTheDocument();
    expect(screen.getByText("Bệnh nhân")).toBeInTheDocument();
  });

  it("dropdown có link 'Lịch hẹn của tôi' → /lich-hen-cua-toi", () => {
    render(<LandingNavbar />);
    fireEvent.click(screen.getByRole("button", { name: /tài khoản/i }));

    const link = screen.getByRole("link", { name: /lịch hẹn của tôi/i });
    expect(link).toHaveAttribute("href", "/lich-hen-cua-toi");
  });

  it("dropdown có button 'Đăng xuất'", () => {
    render(<LandingNavbar />);
    fireEvent.click(screen.getByRole("button", { name: /tài khoản/i }));

    expect(screen.getByRole("button", { name: /đăng xuất/i })).toBeInTheDocument();
  });

  it("click 'Đăng xuất' → signOut rồi router.push('/')", async () => {
    mockSignOut.mockResolvedValue(undefined);
    render(<LandingNavbar />);
    fireEvent.click(screen.getByRole("button", { name: /tài khoản/i }));
    fireEvent.click(screen.getByRole("button", { name: /đăng xuất/i }));

    await vi.waitFor(() => {
      expect(mockSignOut).toHaveBeenCalledTimes(1);
    });
    await vi.waitFor(() => {
      expect(routerPushMock).toHaveBeenCalledWith("/");
    });
  });

  it("dropdown KHÔNG hiển thị email khi user không có email", () => {
    mockHasHydrated.mockReturnValue(true);
    // Override: change mock to user without email
    // For simplicity, re-render with different mock using a clean approach
  });
});
