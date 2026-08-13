import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { ChangePasswordForm } from "@/features/auth/components/change-password-form";

const { mutate } = vi.hoisted(() => ({ mutate: vi.fn() }));

vi.mock("@/features/auth/hooks/use-change-password", () => ({
  useChangePassword: () => ({
    mutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  }),
}));

function setUser(mustChangePassword: boolean) {
  useAuthStore.setState({
    accessToken: "token",
    user: {
      userId: "user-1",
      fullName: "Người dùng",
      email: null,
      role: "DOCTOR",
      mustChangePassword,
    },
  });
}

const VALID_NEW_PASSWORD = "Valid123";

describe("ChangePasswordForm", () => {
  beforeEach(() => {
    mutate.mockReset();
    useAuthStore.setState({ accessToken: null, user: null });
  });

  it("bị ép đổi vì dùng mật khẩu tạm — không hiện ô mật khẩu hiện tại", () => {
    // Sửa 06/08/2026 — người dùng vừa chứng minh biết mật khẩu tạm qua bước đăng nhập, nên
    // không hỏi lại nữa.
    setUser(true);

    render(<ChangePasswordForm />);

    expect(screen.queryByLabelText(/mật khẩu hiện tại/i)).not.toBeInTheDocument();
  });

  it("đổi tự nguyện — vẫn hiện và bắt buộc ô mật khẩu hiện tại", async () => {
    setUser(false);
    const user = userEvent.setup();

    render(<ChangePasswordForm />);
    expect(screen.getByLabelText(/mật khẩu hiện tại/i)).toBeInTheDocument();

    await user.type(screen.getByLabelText(/^mật khẩu mới$/i), VALID_NEW_PASSWORD);
    await user.type(screen.getByLabelText(/xác nhận mật khẩu mới/i), VALID_NEW_PASSWORD);
    await user.click(screen.getByRole("button", { name: /đổi mật khẩu/i }));

    // Không gõ ô mật khẩu hiện tại -> phải chặn, không gọi mutate.
    expect(mutate).not.toHaveBeenCalled();
    expect(screen.getByText(/vui lòng điền đầy đủ cả ba ô/i)).toBeInTheDocument();
  });

  it("bị ép đổi — gửi currentPassword null dù không nhập gì vào đó", async () => {
    setUser(true);
    const user = userEvent.setup();

    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText(/^mật khẩu mới$/i), VALID_NEW_PASSWORD);
    await user.type(screen.getByLabelText(/xác nhận mật khẩu mới/i), VALID_NEW_PASSWORD);
    await user.click(screen.getByRole("button", { name: /đổi mật khẩu/i }));

    expect(mutate).toHaveBeenCalledWith({
      currentPassword: null,
      newPassword: VALID_NEW_PASSWORD,
      confirmNewPassword: VALID_NEW_PASSWORD,
    });
  });

  it("đổi tự nguyện — gửi đúng giá trị đã gõ vào ô mật khẩu hiện tại", async () => {
    setUser(false);
    const user = userEvent.setup();

    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText(/mật khẩu hiện tại/i), "OldPass1");
    await user.type(screen.getByLabelText(/^mật khẩu mới$/i), VALID_NEW_PASSWORD);
    await user.type(screen.getByLabelText(/xác nhận mật khẩu mới/i), VALID_NEW_PASSWORD);
    await user.click(screen.getByRole("button", { name: /đổi mật khẩu/i }));

    expect(mutate).toHaveBeenCalledWith({
      currentPassword: "OldPass1",
      newPassword: VALID_NEW_PASSWORD,
      confirmNewPassword: VALID_NEW_PASSWORD,
    });
  });
});
