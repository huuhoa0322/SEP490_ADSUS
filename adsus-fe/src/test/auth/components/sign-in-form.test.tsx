import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { SignInForm } from "@/features/auth/components/sign-in-form";

const { mutate, hookState, apkHookState, searchParamsMock, replaceMock } = vi.hoisted(() => ({
  mutate: vi.fn(),
  hookState: {
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null as unknown,
  },
  apkHookState: {
    data: "https://github.com/huuhoa0322/SEP490_ADSUS/releases/download/android-v1.0.0/adsus-mobile-1.0.0.apk" as
      | string
      | null,
    isPending: false,
  },
  searchParamsMock: vi.fn(() => new URLSearchParams()),
  replaceMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParamsMock(),
  useRouter: () => ({ replace: replaceMock }),
}));

vi.mock("@/features/auth/hooks/use-sign-in", () => ({
  useSignIn: () => ({ mutate, ...hookState }),
}));

vi.mock("@/features/auth/hooks/use-latest-android-release", () => ({
  useLatestAndroidRelease: () => ({ ...apkHookState }),
}));

/** Dựng một lỗi axios đúng mã HTTP cần thử. */
function loiHttp(status: number): AxiosError {
  const error = new AxiosError("Request failed");
  error.response = {
    status,
    statusText: "",
    data: {},
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

describe("SignInForm", () => {
  beforeEach(() => {
    mutate.mockReset();
    replaceMock.mockClear();
    hookState.isPending = false;
    hookState.isSuccess = false;
    hookState.isError = false;
    hookState.error = null;
    apkHookState.data =
      "https://github.com/huuhoa0322/SEP490_ADSUS/releases/download/android-v1.0.0/adsus-mobile-1.0.0.apk";
    apkHookState.isPending = false;
    searchParamsMock.mockReturnValue(new URLSearchParams());
  });

  it("bỏ trống số điện thoại và mật khẩu — chặn submit, không gọi mutate", async () => {
    const user = userEvent.setup();

    render(<SignInForm />);
    await user.click(screen.getByRole("button", { name: /đăng nhập/i }));

    expect(mutate).not.toHaveBeenCalled();
    expect(
      screen.getByText(/vui lòng nhập số điện thoại và mật khẩu/i),
    ).toBeInTheDocument();
  });

  it("nhập đủ — gọi mutate với số điện thoại đã trim khoảng trắng", async () => {
    const user = userEvent.setup();

    render(<SignInForm />);
    await user.type(screen.getByLabelText(/số điện thoại/i), "  0900000000  ");
    await user.type(screen.getByLabelText(/^mật khẩu$/i), "Password1");
    await user.click(screen.getByRole("button", { name: /đăng nhập/i }));

    expect(mutate).toHaveBeenCalledWith({
      phoneNumber: "0900000000",
      password: "Password1",
    });
  });

  it("đăng nhập sai (401) — hiện đúng MỘT câu chung GB-06", () => {
    hookState.isError = true;
    hookState.error = loiHttp(401);

    render(<SignInForm />);

    expect(screen.getByText("Số điện thoại hoặc mật khẩu không đúng.")).toBeInTheDocument();
  });

  it("đang đăng nhập (isPending) — disable nút submit", () => {
    hookState.isPending = true;

    render(<SignInForm />);

    expect(screen.getByRole("button", { name: /đang đăng nhập/i })).toBeDisabled();
  });

  it("đăng nhập thành công (isSuccess) — vẫn disable nút để tránh double-submit lúc chờ redirect", () => {
    hookState.isSuccess = true;

    render(<SignInForm />);

    expect(screen.getByRole("button", { name: /đăng nhập/i })).toBeDisabled();
  });

  it("URL có ?expired=1 — hiện banner phiên đăng nhập đã kết thúc", () => {
    searchParamsMock.mockReturnValue(new URLSearchParams("expired=1"));

    render(<SignInForm />);

    expect(screen.getByText(/phiên đăng nhập đã kết thúc/i)).toBeInTheDocument();
  });

  it("bấm icon con mắt — chuyển ô mật khẩu giữa ẩn và hiện", async () => {
    const user = userEvent.setup();

    render(<SignInForm />);
    const passwordInput = screen.getByLabelText(/^mật khẩu$/i) as HTMLInputElement;
    expect(passwordInput.type).toBe("password");

    await user.click(screen.getByRole("button", { name: /hiện mật khẩu/i }));
    expect(passwordInput.type).toBe("text");

    await user.click(screen.getByRole("button", { name: /ẩn mật khẩu/i }));
    expect(passwordInput.type).toBe("password");
  });

  it("đã có link APK — nút tải là link thật, đúng href", () => {
    render(<SignInForm />);

    const link = screen.getByRole("link", { name: /tải ứng dụng android/i });
    expect(link).toHaveAttribute("href", apkHookState.data);
  });

  it("đang tìm bản Android (isPending) — không phải link, hiện đúng câu chờ", () => {
    apkHookState.data = null;
    apkHookState.isPending = true;

    render(<SignInForm />);

    expect(screen.getByText("Đang tìm bản Android mới nhất...")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /tải ứng dụng android/i })).not.toBeInTheDocument();
  });

  it("chưa có release nào (data null, hết pending) — báo chưa có bản, không phải link", () => {
    apkHookState.data = null;
    apkHookState.isPending = false;

    render(<SignInForm />);

    expect(screen.getByText("Chưa có bản Android nào")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /tải ứng dụng android/i })).not.toBeInTheDocument();
  });
});
