import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AuthUser } from "@/store/auth-store";
import { useAuthStore } from "@/store/auth-store";

import { AuthGuard } from "@/features/auth/components/auth-guard";

const { replaceMock, pathnameMock, hasHydratedMock } = vi.hoisted(() => ({
  replaceMock: vi.fn(),
  pathnameMock: vi.fn(() => "/patients"),
  hasHydratedMock: vi.fn(() => true),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
  usePathname: () => pathnameMock(),
}));

// hasHydrated đọc localStorage lúc module load — trong jsdom nó đã "hydrate xong" từ trước
// khi test chạy, nên không có cách nào giả lập trạng thái "đang chờ" qua state Zustand thật.
// Mock riêng phần này, giữ nguyên useAuthStore/getHomePathForRole/isRoleAllowedOnPath thật.
vi.mock("@/store/auth-store", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/store/auth-store")>();
  return { ...actual, useHasHydrated: () => hasHydratedMock() };
});

function nguoiDung(role: AuthUser["role"], overrides: Partial<AuthUser> = {}): AuthUser {
  return {
    userId: "user-1",
    fullName: "Người dùng test",
    email: null,
    role,
    mustChangePassword: false,
    ...overrides,
  };
}

describe("AuthGuard", () => {
  beforeEach(() => {
    replaceMock.mockClear();
    pathnameMock.mockReturnValue("/patients");
    hasHydratedMock.mockReturnValue(true);
    useAuthStore.setState({ accessToken: null, user: null });
  });

  it("chưa hydrate xong — chỉ hiện loading, không điều hướng đi đâu", () => {
    hasHydratedMock.mockReturnValue(false);
    useAuthStore.setState({ accessToken: "token", user: nguoiDung("DOCTOR") });

    render(
      <AuthGuard>
        <div>protected content</div>
      </AuthGuard>,
    );

    expect(screen.getByText(/đang kiểm tra phiên đăng nhập/i)).toBeInTheDocument();
    expect(screen.queryByText("protected content")).not.toBeInTheDocument();
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("không có accessToken — điều hướng về /login", async () => {
    useAuthStore.setState({ accessToken: null, user: null });

    render(
      <AuthGuard>
        <div>protected content</div>
      </AuthGuard>,
    );

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/login"));
  });

  it("UC-25: mustChangePassword=true, chưa ở /change-password — điều hướng tới đó", async () => {
    pathnameMock.mockReturnValue("/patients");
    useAuthStore.setState({
      accessToken: "token",
      user: nguoiDung("DOCTOR", { mustChangePassword: true }),
    });

    render(
      <AuthGuard>
        <div>protected content</div>
      </AuthGuard>,
    );

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/change-password"));
  });

  it("UC-25: mustChangePassword=true nhưng ĐANG ở /change-password — không điều hướng, render children", () => {
    pathnameMock.mockReturnValue("/change-password");
    useAuthStore.setState({
      accessToken: "token",
      user: nguoiDung("DOCTOR", { mustChangePassword: true }),
    });

    render(
      <AuthGuard>
        <div>protected content</div>
      </AuthGuard>,
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it("PRD §3.2: Doctor gõ nhầm /admin — bị đưa về đúng khu vực của vai trò (/patients)", async () => {
    pathnameMock.mockReturnValue("/admin");
    useAuthStore.setState({ accessToken: "token", user: nguoiDung("DOCTOR") });

    render(
      <AuthGuard>
        <div>protected content</div>
      </AuthGuard>,
    );

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/patients"));
  });

  it("hợp lệ — đúng khu vực, không bị ép đổi mật khẩu — render children, không điều hướng", () => {
    pathnameMock.mockReturnValue("/patients");
    useAuthStore.setState({ accessToken: "token", user: nguoiDung("DOCTOR") });

    render(
      <AuthGuard>
        <div>protected content</div>
      </AuthGuard>,
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
    expect(replaceMock).not.toHaveBeenCalled();
  });

  describe("phiên hỏng (thiếu userId)", () => {
    it("chỉ điều hướng đúng MỘT lần tới /login?expired=1, không bị /login trơn đè mất", async () => {
      // Giả lập phiên cũ persist TRƯỚC khi backend thêm userId — ép kiểu vì AuthUser giờ bắt
      // buộc có userId, nhưng dữ liệu thật đang nằm trong localStorage của người dùng cũ thì
      // không có trường này.
      useAuthStore.setState({
        accessToken: "legacy-token",
        user: {
          fullName: "BS. Cũ",
          email: null,
          role: "DOCTOR",
          mustChangePassword: false,
        } as never,
      });

      render(
        <AuthGuard>
          <div>protected</div>
        </AuthGuard>,
      );

      await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/login?expired=1"));

      // signOut() đổi accessToken/user về null ngay trong effect trên — chờ state đó lan tới
      // rồi khẳng định effect không chạy tiếp gọi thêm replace("/login") đè mất query.
      await waitFor(() => expect(useAuthStore.getState().accessToken).toBeNull());
      expect(replaceMock).toHaveBeenCalledTimes(1);
    });
  });
});
