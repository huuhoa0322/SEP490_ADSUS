import { render, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { AuthGuard } from "@/features/auth/components/auth-guard";

const { replaceMock } = vi.hoisted(() => ({ replaceMock: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
  usePathname: () => "/patients",
}));

describe("AuthGuard — phiên hỏng (thiếu userId)", () => {
  beforeEach(() => {
    replaceMock.mockClear();
    useAuthStore.setState({ accessToken: null, user: null });
  });

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
