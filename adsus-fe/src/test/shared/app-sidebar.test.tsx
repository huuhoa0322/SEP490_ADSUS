import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ACCESS_TOKEN_KEY } from "@/lib/api-client";
import type { AuthUser } from "@/store/auth-store";
import { useAuthStore } from "@/store/auth-store";
import { useUiStore } from "@/store/ui-store";
import type { Role } from "@/types/api.types";
import { AppSidebar } from "@/components/shared/app-sidebar";
import { roleAccentVar } from "@/components/shared/role-badge";

const { replaceMock, pathnameMock } = vi.hoisted(() => ({
  replaceMock: vi.fn(),
  pathnameMock: vi.fn(() => "/dashboard"),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
  usePathname: () => pathnameMock(),
}));

function mockUser(role: Role, overrides: Partial<AuthUser> = {}): AuthUser {
  return {
    userId: `user-${role.toLowerCase()}-1`,
    fullName: `Test ${role}`,
    email: `${role.toLowerCase()}@adsus.vn`,
    role,
    mustChangePassword: false,
    ...overrides,
  };
}

describe("AppSidebar", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    pathnameMock.mockReturnValue("/dashboard");
    useUiStore.setState({ sidebarExpanded: true, isMobileMenuOpen: false });
    useAuthStore.setState({ accessToken: "test-token", user: mockUser("ADMIN") });
    if (typeof window !== "undefined" && window.localStorage) {
      window.localStorage.setItem(ACCESS_TOKEN_KEY, "test-token");
    }
  });

  describe("Authentication & Visibility", () => {
    it("returns null when user is not authenticated", () => {
      useAuthStore.setState({ user: null });
      const { container } = render(<AppSidebar />);
      expect(container.firstChild).toBeNull();
    });

    it("renders sidebar aside element with proper borders and visual separation when authenticated", () => {
      const { container } = render(<AppSidebar />);
      const aside = container.querySelector("aside");
      expect(aside).toBeInTheDocument();
      expect(aside).toHaveClass("border-r", "border-slate-200", "bg-slate-50/80", "shadow-sm");
    });
  });

  describe("Role-based Navigation Rendering", () => {
    it("renders correct navigation items for ADMIN", () => {
      useAuthStore.setState({ user: mockUser("ADMIN") });
      render(<AppSidebar />);

      // Admin items
      expect(screen.getByText("Bảng điều khiển")).toBeInTheDocument();
      expect(screen.getByText("Duyệt nghỉ phép bác sĩ")).toBeInTheDocument();
      expect(screen.getByText("Danh mục thuốc")).toBeInTheDocument();
      expect(screen.getByText("Nhà cung cấp")).toBeInTheDocument();
      expect(screen.getByText("Nhập kho")).toBeInTheDocument();
      expect(screen.getByText("Lịch sử kho")).toBeInTheDocument();
      expect(screen.getByText("Cảnh báo kho")).toBeInTheDocument();
      expect(screen.getByText("Tài khoản nhân sự")).toBeInTheDocument();
      expect(screen.getByText("Mô hình AI")).toBeInTheDocument();
      expect(screen.getByText("Blog")).toBeInTheDocument();

      // Common bottom items
      expect(screen.getByText("Đổi mật khẩu")).toBeInTheDocument();
      expect(screen.getByText("Đăng xuất")).toBeInTheDocument();

      // Clinical items shouldn't appear for Admin
      expect(screen.queryByText("Danh sách bệnh nhân")).not.toBeInTheDocument();
      expect(screen.queryByText("Tiếp đón / Check-in")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý lịch")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý hóa đơn")).not.toBeInTheDocument();
    });

    it("renders correct navigation items for DOCTOR", () => {
      useAuthStore.setState({ user: mockUser("DOCTOR") });
      pathnameMock.mockReturnValue("/patients");
      render(<AppSidebar />);

      // Doctor items
      expect(screen.getByText("Danh sách bệnh nhân")).toBeInTheDocument();
      expect(screen.getByText("Quản lý lịch")).toBeInTheDocument();
      expect(screen.getByText("Theo dõi & nhắc uống thuốc")).toBeInTheDocument();

      // Bottom items
      expect(screen.getByText("Đổi mật khẩu")).toBeInTheDocument();
      expect(screen.getByText("Đăng xuất")).toBeInTheDocument();

      // Items that Doctor should not see
      expect(screen.queryByText("Bảng điều khiển")).not.toBeInTheDocument();
      expect(screen.queryByText("Tiếp đón / Check-in")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý hóa đơn")).not.toBeInTheDocument();
      expect(screen.queryByText("Tài khoản nhân sự")).not.toBeInTheDocument();
      expect(screen.getByText("Danh mục thuốc")).toBeInTheDocument();
    });

    it("renders correct navigation items for NURSE", () => {
      useAuthStore.setState({ user: mockUser("NURSE") });
      pathnameMock.mockReturnValue("/patients");
      render(<AppSidebar />);

      // Nurse items
      expect(screen.getByText("Danh sách bệnh nhân")).toBeInTheDocument();
      expect(screen.getByText("Tiếp đón / Check-in")).toBeInTheDocument();
      expect(screen.getByText("Quản lý hóa đơn")).toBeInTheDocument();

      // Bottom items
      expect(screen.getByText("Đổi mật khẩu")).toBeInTheDocument();
      expect(screen.getByText("Đăng xuất")).toBeInTheDocument();

      // Items Nurse should not see
      expect(screen.queryByText("Bảng điều khiển")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý lịch")).not.toBeInTheDocument();
      expect(screen.queryByText("Duyệt nghỉ phép bác sĩ")).not.toBeInTheDocument();
      expect(screen.queryByText("Mô hình AI")).not.toBeInTheDocument();
    });

    it("renders correct navigation items for PHARMACIST", () => {
      useAuthStore.setState({ user: mockUser("PHARMACIST") });
      pathnameMock.mockReturnValue("/medicines");
      render(<AppSidebar />);

      // Pharmacist items
      expect(screen.getByText("Danh mục thuốc")).toBeInTheDocument();
      expect(screen.getByText("Nhà cung cấp")).toBeInTheDocument();
      expect(screen.getByText("Nhập kho")).toBeInTheDocument();
      expect(screen.getByText("Lịch sử kho")).toBeInTheDocument();
      expect(screen.getByText("Cảnh báo kho")).toBeInTheDocument();

      // Bottom items
      expect(screen.getByText("Đổi mật khẩu")).toBeInTheDocument();
      expect(screen.getByText("Đăng xuất")).toBeInTheDocument();

      // Items Pharmacist should not see
      expect(screen.queryByText("Bảng điều khiển")).not.toBeInTheDocument();
      expect(screen.queryByText("Danh sách bệnh nhân")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý lịch")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý hóa đơn")).not.toBeInTheDocument();
      expect(screen.queryByText("Tài khoản nhân sự")).not.toBeInTheDocument();
    });

    it("renders gracefully for PATIENT with no navigation groups", () => {
      useAuthStore.setState({ user: mockUser("PATIENT") });
      render(<AppSidebar />);

      // Patient has no clinical, pharmacy, or admin navigation items
      expect(screen.queryByText("Bảng điều khiển")).not.toBeInTheDocument();
      expect(screen.queryByText("Danh sách bệnh nhân")).not.toBeInTheDocument();
      expect(screen.queryByText("Danh mục thuốc")).not.toBeInTheDocument();
      expect(screen.queryByText("Quản lý hóa đơn")).not.toBeInTheDocument();

      // Bottom section still mounts safely
      expect(screen.getByText("Đổi mật khẩu")).toBeInTheDocument();
      expect(screen.getByText("Đăng xuất")).toBeInTheDocument();
    });
  });

  describe("Active Route & Role-Accent Contrast Styling", () => {
    it("applies correct role accent CSS variable to sidebar container", () => {
      const roles: Role[] = ["ADMIN", "DOCTOR", "NURSE", "PHARMACIST", "PATIENT"];

      roles.forEach((role) => {
        useAuthStore.setState({ user: mockUser(role) });
        const { container, unmount } = render(<AppSidebar />);
        const aside = container.querySelector("aside");
        expect(aside).toHaveStyle({ "--role-accent": roleAccentVar(role) });
        unmount();
      });
    });

    it("applies high-contrast font-bold text-foreground and background accent on active route", () => {
      useAuthStore.setState({ user: mockUser("DOCTOR") });
      pathnameMock.mockReturnValue("/patients");

      render(<AppSidebar />);

      const activeLink = screen.getByText("Danh sách bệnh nhân").closest("a");
      expect(activeLink).toBeInTheDocument();
      expect(activeLink).toHaveClass("bg-[var(--role-accent)]/15", "font-bold", "text-foreground");

      // Inactive links have font-medium and text-slate-700
      const inactiveLink = screen.getByText("Quản lý lịch").closest("a");
      expect(inactiveLink).toBeInTheDocument();
      expect(inactiveLink).toHaveClass("font-medium", "text-slate-700");
      expect(inactiveLink).not.toHaveClass("font-bold");
    });

    it("renders active left indicator bar on the active item", () => {
      useAuthStore.setState({ user: mockUser("ADMIN") });
      pathnameMock.mockReturnValue("/dashboard");

      render(<AppSidebar />);

      const activeLink = screen.getByText("Bảng điều khiển").closest("a");
      expect(activeLink).toBeInTheDocument();

      const indicator = activeLink?.querySelector("span[aria-hidden]");
      expect(indicator).toBeInTheDocument();
      expect(indicator).toHaveClass("absolute", "left-0", "w-1", "bg-[var(--role-accent)]");
    });


    it("correctly distinguishes between /inventory and /inventory/import", () => {
      useAuthStore.setState({ user: mockUser("PHARMACIST") });

      // When at /inventory/import:
      pathnameMock.mockReturnValue("/inventory/import");
      const { unmount } = render(<AppSidebar />);

      const importLink = screen.getByText("Nhập kho").closest("a");
      const historyLink = screen.getByText("Lịch sử kho").closest("a");

      expect(importLink).toHaveClass("font-bold", "text-foreground");
      expect(historyLink).not.toHaveClass("font-bold");
      expect(historyLink).toHaveClass("font-medium", "text-slate-700");

      unmount();

      // When at /inventory:
      pathnameMock.mockReturnValue("/inventory");
      render(<AppSidebar />);

      const importLink2 = screen.getByText("Nhập kho").closest("a");
      const historyLink2 = screen.getByText("Lịch sử kho").closest("a");

      expect(historyLink2).toHaveClass("font-bold", "text-foreground");
      expect(importLink2).not.toHaveClass("font-bold");
    });
  });

  describe("Sidebar Collapse and Expand Toggle", () => {
    it("expands to full width w-64 when sidebarExpanded is true", () => {
      useUiStore.setState({ sidebarExpanded: true });
      const { container } = render(<AppSidebar />);

      const aside = container.querySelector("aside");
      expect(aside).toHaveClass("w-64");
      expect(aside).not.toHaveClass("w-[68px]");

      // Item text is visible
      expect(screen.getByText("Bảng điều khiển")).toBeInTheDocument();
      // Sign out text is visible
      expect(screen.getByText("Đăng xuất")).toBeInTheDocument();
      // Group header is visible for multi-group role and has uppercase styling
      const groupHeader = screen.getByText("Tổng quan");
      expect(groupHeader).toBeInTheDocument();
      expect(groupHeader).toHaveClass("uppercase", "font-bold");
    });

    it("collapses to icon-only width w-[68px] when sidebarExpanded is false", () => {
      useUiStore.setState({ sidebarExpanded: false });
      const { container } = render(<AppSidebar />);

      const aside = container.querySelector("aside");
      expect(aside).toHaveClass("w-[68px]");
      expect(aside).not.toHaveClass("w-64");

      // Item label spans are not rendered in DOM when collapsed
      expect(screen.queryByText("Bảng điều khiển")).not.toBeInTheDocument();
      expect(screen.queryByText("Đăng xuất")).not.toBeInTheDocument();
      expect(screen.queryByText("Tổng quan")).not.toBeInTheDocument();

      // Links have title attributes for accessibility tooltip in collapsed mode
      const dashboardLink = container.querySelector("a[href='/dashboard']");
      expect(dashboardLink).toHaveAttribute("title", "Bảng điều khiển");

      const signOutButton = screen.getByTitle("Đăng xuất");
      expect(signOutButton).toBeInTheDocument();
    });

    it("reacts dynamically to store toggleSidebar call", () => {
      useUiStore.setState({ sidebarExpanded: true });
      const { container, rerender } = render(<AppSidebar />);

      const aside = container.querySelector("aside");
      expect(aside).toHaveClass("w-64");
      expect(screen.getByText("Bảng điều khiển")).toBeInTheDocument();

      // Trigger store toggle
      act(() => {
        useUiStore.getState().toggleSidebar();
      });
      rerender(<AppSidebar />);

      expect(aside).toHaveClass("w-[68px]");
      expect(screen.queryByText("Bảng điều khiển")).not.toBeInTheDocument();

      // Toggle back
      act(() => {
        useUiStore.getState().toggleSidebar();
      });
      rerender(<AppSidebar />);

      expect(aside).toHaveClass("w-64");
      expect(screen.getByText("Bảng điều khiển")).toBeInTheDocument();
    });
  });

  describe("Sign Out Action & High Contrast Styling", () => {
    it("renders sign out button with high-contrast text-slate-700 and hover:text-destructive", () => {
      render(<AppSidebar />);

      const signOutButton = screen.getByTitle("Đăng xuất");
      expect(signOutButton).toBeInTheDocument();
      expect(signOutButton).toHaveClass(
        "font-semibold",
        "text-slate-700",
        "hover:text-destructive",
        "hover:bg-destructive/10",
      );
    });

    it("handles sign out: clears localStorage token, triggers auth store signOut, and redirects to /login", () => {
      render(<AppSidebar />);

      const signOutButton = screen.getByTitle("Đăng xuất");
      fireEvent.click(signOutButton);

      // Local storage token removed
      expect(window.localStorage.getItem(ACCESS_TOKEN_KEY)).toBeNull();

      // Auth store signed out
      expect(useAuthStore.getState().user).toBeNull();
      expect(useAuthStore.getState().accessToken).toBeNull();

      // Navigated to /login
      expect(replaceMock).toHaveBeenCalledWith("/login");
    });
  });
});
