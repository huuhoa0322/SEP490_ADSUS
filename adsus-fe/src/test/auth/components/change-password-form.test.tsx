import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";

import { ChangePasswordForm } from "@/features/auth/components/change-password-form";

const { mutate, hookState } = vi.hoisted(() => ({
  mutate: vi.fn(),
  hookState: {
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null as unknown,
  },
}));

vi.mock("@/features/auth/hooks/use-change-password", () => ({
  useChangePassword: () => ({ mutate, ...hookState }),
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
    hookState.isPending = false;
    hookState.isSuccess = false;
    hookState.isError = false;
    hookState.error = null;
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

  it("mật khẩu mới chưa đạt chính sách (thiếu chữ hoa/chữ số) — chặn submit", async () => {
    setUser(true);
    const user = userEvent.setup();

    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText(/^mật khẩu mới$/i), "lowercase");
    await user.type(screen.getByLabelText(/xác nhận mật khẩu mới/i), "lowercase");
    await user.click(screen.getByRole("button", { name: /đổi mật khẩu/i }));

    expect(mutate).not.toHaveBeenCalled();
    expect(screen.getByText(/mật khẩu mới chưa đạt yêu cầu bên dưới/i)).toBeInTheDocument();
  });

  it("xác nhận không khớp với mật khẩu mới — chặn submit", async () => {
    setUser(true);
    const user = userEvent.setup();

    render(<ChangePasswordForm />);

    await user.type(screen.getByLabelText(/^mật khẩu mới$/i), VALID_NEW_PASSWORD);
    await user.type(screen.getByLabelText(/xác nhận mật khẩu mới/i), "Valid999");
    await user.click(screen.getByRole("button", { name: /đổi mật khẩu/i }));

    expect(mutate).not.toHaveBeenCalled();
    expect(screen.getByText(/xác nhận mật khẩu không khớp/i)).toBeInTheDocument();
  });

  it("mutation lỗi (isError) — hiện thông báo dịch từ backend", () => {
    setUser(true);
    const error = new AxiosError("Request failed");
    error.response = {
      status: 400,
      statusText: "",
      data: { code: 400, message: "Current password is incorrect.", data: null },
      headers: {},
      config: { headers: new AxiosHeaders() },
    };
    hookState.isError = true;
    hookState.error = error;

    render(<ChangePasswordForm />);

    expect(screen.getByText("Mật khẩu hiện tại không đúng.")).toBeInTheDocument();
  });

  it("mutation thành công (isSuccess) — hiện thông báo đổi mật khẩu thành công", () => {
    setUser(true);
    hookState.isSuccess = true;

    render(<ChangePasswordForm />);

    expect(screen.getByText(/đổi mật khẩu thành công/i)).toBeInTheDocument();
  });

  it("đang gửi (isPending) — disable nút submit", () => {
    setUser(true);
    hookState.isPending = true;

    render(<ChangePasswordForm />);

    expect(screen.getByRole("button", { name: /đang lưu/i })).toBeDisabled();
  });
});
