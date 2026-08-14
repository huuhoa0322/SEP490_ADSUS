import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ForgotPasswordForm } from "@/features/auth/components/forgot-password-form";

const { mutate, hookState } = vi.hoisted(() => ({
  mutate: vi.fn(),
  hookState: {
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null as unknown,
  },
}));

vi.mock("@/features/auth/hooks/use-forgot-password", () => ({
  useForgotPassword: () => ({ mutate, ...hookState }),
}));

function loiHttp(status: number, message: string): AxiosError {
  const error = new AxiosError("Request failed");
  error.response = {
    status,
    statusText: "",
    data: { code: status, message, data: null },
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

describe("ForgotPasswordForm", () => {
  beforeEach(() => {
    mutate.mockReset();
    hookState.isPending = false;
    hookState.isSuccess = false;
    hookState.isError = false;
    hookState.error = null;
  });

  it("bỏ trống số điện thoại hoặc email — chặn submit, không gọi mutate", async () => {
    const user = userEvent.setup();

    render(<ForgotPasswordForm />);
    await user.click(screen.getByRole("button", { name: /gửi mật khẩu mới/i }));

    expect(mutate).not.toHaveBeenCalled();
    expect(
      screen.getByText(/vui lòng nhập số điện thoại và email đã đăng ký/i),
    ).toBeInTheDocument();
  });

  it("số điện thoại sai định dạng — chặn submit, KHÔNG hé lộ số đó có tồn tại hay không (AF-01)", async () => {
    const user = userEvent.setup();

    render(<ForgotPasswordForm />);
    await user.type(screen.getByPlaceholderText("0900000000"), "12345");
    await user.type(screen.getByPlaceholderText("email@example.com"), "a@b.com");
    await user.click(screen.getByRole("button", { name: /gửi mật khẩu mới/i }));

    expect(mutate).not.toHaveBeenCalled();
    expect(
      screen.getByText("Số điện thoại phải bắt đầu bằng 0 và có đúng 10 chữ số."),
    ).toBeInTheDocument();
  });

  it("nhập hợp lệ — gọi mutate với giá trị đã trim", async () => {
    const user = userEvent.setup();

    render(<ForgotPasswordForm />);
    await user.type(screen.getByPlaceholderText("0900000000"), "  0900000000  ");
    await user.type(screen.getByPlaceholderText("email@example.com"), "  a@b.com  ");
    await user.click(screen.getByRole("button", { name: /gửi mật khẩu mới/i }));

    expect(mutate).toHaveBeenCalledWith({ phoneNumber: "0900000000", email: "a@b.com" });
  });

  it("gửi thành công (isSuccess) — thay hẳn form bằng lời nhắn mơ hồ (AF-01)", () => {
    hookState.isSuccess = true;

    render(<ForgotPasswordForm />);

    expect(screen.getByText(/đã gửi yêu cầu/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /gửi mật khẩu mới/i })).not.toBeInTheDocument();
  });

  it("mutation lỗi (isError) — hiện thông báo dịch từ backend, ví dụ bị giới hạn tần suất", () => {
    hookState.isError = true;
    hookState.error = loiHttp(429, "Too many requests. Please wait before trying again.");

    render(<ForgotPasswordForm />);

    expect(
      screen.getByText("Bạn đã gửi quá nhiều yêu cầu. Vui lòng chờ một lúc rồi thử lại."),
    ).toBeInTheDocument();
  });

  it("đang gửi (isPending) — disable nút submit", () => {
    hookState.isPending = true;

    render(<ForgotPasswordForm />);

    expect(screen.getByRole("button", { name: /đang gửi/i })).toBeDisabled();
  });
});
