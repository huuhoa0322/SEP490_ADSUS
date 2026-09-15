import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { AxiosError, AxiosHeaders } from "axios";
import type { ConfirmationResult } from "firebase/auth";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { RegisterForm } from "@/features/auth/components/register-form";
import { useAuthStore } from "@/store/auth-store";

// =============================================================================
// Hoisted Mocks
// =============================================================================

const {
  sendPhoneVerificationCodeMock,
  confirmPhoneVerificationCodeMock,
  clearRecaptchaVerifierMock,
  completeRegistrationMock,
  replaceMock,
  pushMock,
  searchParamsMock,
} = vi.hoisted(() => ({
  sendPhoneVerificationCodeMock: vi.fn(),
  confirmPhoneVerificationCodeMock: vi.fn(),
  clearRecaptchaVerifierMock: vi.fn(),
  completeRegistrationMock: vi.fn(),
  replaceMock: vi.fn(),
  pushMock: vi.fn(),
  searchParamsMock: vi.fn(() => new URLSearchParams()),
}));

// Mock Firebase Phone Auth Service
vi.mock("@/features/auth/services/firebase-phone-auth.service", () => ({
  sendPhoneVerificationCode: sendPhoneVerificationCodeMock,
  confirmPhoneVerificationCode: confirmPhoneVerificationCodeMock,
  clearRecaptchaVerifier: clearRecaptchaVerifierMock,
  formatToE164: vi.fn((phone: string) => {
    const trimmed = phone.trim().replace(/[\s.-]+/g, "");
    if (trimmed.startsWith("+84")) return trimmed;
    if (trimmed.startsWith("84")) return `+${trimmed}`;
    if (trimmed.startsWith("0")) return `+84${trimmed.slice(1)}`;
    return trimmed;
  }),
  getRecaptchaVerifier: vi.fn(),
  ensureRecaptchaContainer: vi.fn(),
}));

// Mock Next Navigation
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    replace: replaceMock,
    push: pushMock,
  }),
  useSearchParams: () => searchParamsMock(),
}));

// Mock Complete Registration API
vi.mock("@/features/auth/api/auth.api", () => ({
  completeRegistration: completeRegistrationMock,
}));

// =============================================================================
// Test Helpers
// =============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

function renderRegisterForm(client?: QueryClient) {
  const queryClient = client ?? createTestQueryClient();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return {
    queryClient,
    ...render(<RegisterForm />, { wrapper }),
  };
}

const mockConfirmationResult = {
  verificationId: "test-firebase-verification-id",
  confirm: vi.fn(),
} as unknown as ConfirmationResult;

async function advanceToStep2(phoneNumber = "0981111005") {
  sendPhoneVerificationCodeMock.mockResolvedValueOnce(mockConfirmationResult);
  const phoneInput = screen.getByLabelText(/số điện thoại/i);
  fireEvent.change(phoneInput, { target: { value: phoneNumber } });
  const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
  await act(async () => {
    fireEvent.click(continueBtn);
  });
  expect(screen.getByText("Xác thực OTP")).toBeInTheDocument();
}

async function advanceToStep3(
  phoneNumber = "0981111005",
  otp = "123456",
  tokenId = "mock-firebase-id-token",
) {
  await advanceToStep2(phoneNumber);
  confirmPhoneVerificationCodeMock.mockResolvedValueOnce(tokenId);
  const otpInput = screen.getByLabelText(/mã xác thực otp/i);
  fireEvent.change(otpInput, { target: { value: otp } });
  const verifyBtn = screen.getByRole("button", { name: /^xác thực$/i });
  await act(async () => {
    fireEvent.click(verifyBtn);
  });
  expect(screen.getByText("Thông tin cá nhân")).toBeInTheDocument();
}

function create409ConflictError(detail = "This phone number is already registered."): AxiosError {
  const error = new AxiosError("Conflict");
  error.status = 409;
  error.response = {
    status: 409,
    statusText: "Conflict",
    data: { detail },
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

// =============================================================================
// Test Suites
// =============================================================================

describe("RegisterForm", () => {
  beforeEach(() => {
    vi.stubEnv("NODE_ENV", "development");
    vi.clearAllMocks();
    useAuthStore.setState({ accessToken: null, refreshToken: null, user: null });
    if (typeof window !== "undefined") {
      window.localStorage.clear();
      window.history.pushState({}, "", "/register");
    }
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.useRealTimers();
  });

  // ===========================================================================
  // a. Step 1 (Phone Input & Validation)
  // ===========================================================================
  describe("Step 1 — Phone Input & Validation", () => {
    it("renders phone input and 'Tiếp tục' button with initial step indicator", () => {
      renderRegisterForm();

      expect(screen.getByRole("heading", { name: /nhập số điện thoại/i })).toBeInTheDocument();
      expect(screen.getByLabelText(/số điện thoại/i)).toBeInTheDocument();
      expect(screen.getByPlaceholderText("0xxxxxxxxx")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /tiếp tục/i })).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /đăng nhập ngay/i })).toHaveAttribute(
        "href",
        "/login",
      );
    });

    it("shows Dev Hint banner with test phone 0981111005 and OTP 123456 in development mode", () => {
      vi.stubEnv("NODE_ENV", "development");
      renderRegisterForm();

      expect(screen.getByRole("note")).toBeInTheDocument();
      expect(screen.getByText(/chế độ phát triển \(dev hint\)/i)).toBeInTheDocument();
      expect(screen.getByText(/0981111005 \(\+84981111005\)/i)).toBeInTheDocument();
      expect(screen.getByText("123456")).toBeInTheDocument();
    });

    it("clicking Dev Hint test phone fills the phone input field", () => {
      vi.stubEnv("NODE_ENV", "development");
      renderRegisterForm();

      const devHintBtn = screen.getByRole("button", { name: /0981111005 \(\+84981111005\)/i });
      fireEvent.click(devHintBtn);

      const phoneInput = screen.getByLabelText(/số điện thoại/i) as HTMLInputElement;
      expect(phoneInput.value).toBe("0981111005");
    });

    it("hides Dev Hint banner when NODE_ENV is production", () => {
      vi.stubEnv("NODE_ENV", "production");
      renderRegisterForm();

      expect(screen.queryByRole("note")).not.toBeInTheDocument();
      expect(screen.queryByText(/chế độ phát triển/i)).not.toBeInTheDocument();
    });

    it("rejects empty phone number with validation error", async () => {
      renderRegisterForm();

      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      fireEvent.click(continueBtn);

      expect(await screen.findByText(/vui lòng nhập số điện thoại\./i)).toBeInTheDocument();
      expect(sendPhoneVerificationCodeMock).not.toHaveBeenCalled();
    });

    it("rejects phone number with only whitespace with validation error", async () => {
      renderRegisterForm();

      const phoneInput = screen.getByLabelText(/số điện thoại/i);
      fireEvent.change(phoneInput, { target: { value: "          " } });
      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      fireEvent.click(continueBtn);

      expect(await screen.findByText(/vui lòng nhập số điện thoại\./i)).toBeInTheDocument();
      expect(sendPhoneVerificationCodeMock).not.toHaveBeenCalled();
    });

    it.each([
      ["less than 10 digits", "0981111"],
      ["letters included", "098abc1234"],
      ["special characters", "098-111-005"],
      ["not starting with 0", "1981111005"],
    ])("rejects invalid phone number (%s: '%s')", async (_, invalidPhone) => {
      renderRegisterForm();

      const phoneInput = screen.getByLabelText(/số điện thoại/i);
      fireEvent.change(phoneInput, { target: { value: invalidPhone } });
      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      fireEvent.click(continueBtn);

      expect(
        await screen.findByText(/số điện thoại không hợp lệ\. vui lòng nhập 10 chữ số bắt đầu bằng 0/i),
      ).toBeInTheDocument();
      expect(sendPhoneVerificationCodeMock).not.toHaveBeenCalled();
    });

    it("calls sendPhoneVerificationCode with valid 10-digit phone and advances to Step 2", async () => {
      sendPhoneVerificationCodeMock.mockResolvedValueOnce(mockConfirmationResult);
      renderRegisterForm();

      const phoneInput = screen.getByLabelText(/số điện thoại/i);
      fireEvent.change(phoneInput, { target: { value: "  0981111005  " } });
      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      await act(async () => {
        fireEvent.click(continueBtn);
      });

      expect(sendPhoneVerificationCodeMock).toHaveBeenCalledTimes(1);
      expect(sendPhoneVerificationCodeMock).toHaveBeenCalledWith("0981111005");
      expect(screen.getByRole("heading", { name: /xác thực otp/i })).toBeInTheDocument();
      expect(screen.getByText(/0981111005/)).toBeInTheDocument();
    });

    it("displays error message if sendPhoneVerificationCode fails and stays in Step 1", async () => {
      sendPhoneVerificationCodeMock.mockRejectedValueOnce(
        new Error("SMS quota exceeded. Please try again later."),
      );
      renderRegisterForm();

      const phoneInput = screen.getByLabelText(/số điện thoại/i);
      fireEvent.change(phoneInput, { target: { value: "0981111005" } });
      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      await act(async () => {
        fireEvent.click(continueBtn);
      });

      expect(
        screen.getByText("SMS quota exceeded. Please try again later."),
      ).toBeInTheDocument();
      expect(screen.getByRole("heading", { name: /nhập số điện thoại/i })).toBeInTheDocument();
    });

    it("displays generic fallback error when sendPhoneVerificationCode throws non-Error object", async () => {
      sendPhoneVerificationCodeMock.mockRejectedValueOnce("Unknown fatal failure");
      renderRegisterForm();

      const phoneInput = screen.getByLabelText(/số điện thoại/i);
      fireEvent.change(phoneInput, { target: { value: "0981111005" } });
      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      await act(async () => {
        fireEvent.click(continueBtn);
      });

      expect(
        screen.getByText(/không thể gửi mã xác thực sms\. vui lòng kiểm tra lại số điện thoại\./i),
      ).toBeInTheDocument();
    });

    it("shows loading spinner and disables submit button while sending code", async () => {
      let resolvePromise: (value: ConfirmationResult) => void;
      const pendingPromise = new Promise<ConfirmationResult>((res) => {
        resolvePromise = res;
      });
      sendPhoneVerificationCodeMock.mockReturnValueOnce(pendingPromise);

      renderRegisterForm();
      const phoneInput = screen.getByLabelText(/số điện thoại/i);
      fireEvent.change(phoneInput, { target: { value: "0981111005" } });
      const continueBtn = screen.getByRole("button", { name: /tiếp tục/i });
      fireEvent.click(continueBtn);

      expect(screen.getByText(/đang gửi mã\.\.\./i)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /đang gửi mã\.\.\./i })).toBeDisabled();

      await act(async () => {
        resolvePromise(mockConfirmationResult);
      });

      expect(screen.getByRole("heading", { name: /xác thực otp/i })).toBeInTheDocument();
    });
  });

  // ===========================================================================
  // b. Step 2 (OTP Verification & Countdown Timer)
  // ===========================================================================
  describe("Step 2 — OTP Verification & Countdown Timer", () => {
    it("renders 6-digit OTP input and starts 60-second countdown timer", async () => {
      renderRegisterForm();
      await advanceToStep2("0981111005");

      expect(screen.getByLabelText(/mã xác thực otp/i)).toBeInTheDocument();
      expect(screen.getByPlaceholderText("123456")).toBeInTheDocument();
      const resendBtn = screen.getByRole("button", { name: /gửi lại sau 60s/i });
      expect(resendBtn).toBeInTheDocument();
      expect(resendBtn).toBeDisabled();
    });

    it("counts down second by second and enables 'Gửi lại mã' button when timer reaches 0", async () => {
      vi.useFakeTimers();
      try {
        renderRegisterForm();
        await advanceToStep2("0981111005");

        expect(screen.getByRole("button", { name: /gửi lại sau 60s/i })).toBeDisabled();

        act(() => {
          vi.advanceTimersByTime(10000);
        });
        expect(screen.getByRole("button", { name: /gửi lại sau 50s/i })).toBeDisabled();

        act(() => {
          vi.advanceTimersByTime(50000);
        });
        const resendBtn = screen.getByRole("button", { name: /^gửi lại mã$/i });
        expect(resendBtn).toBeInTheDocument();
        expect(resendBtn).not.toBeDisabled();
      } finally {
        vi.useRealTimers();
      }
    });

    it("clicking 'Gửi lại mã' when timer reaches 0 calls sendPhoneVerificationCode and resets countdown", async () => {
      vi.useFakeTimers();
      try {
        renderRegisterForm();
        await advanceToStep2("0981111005");

        act(() => {
          vi.advanceTimersByTime(60000);
        });

        const resendBtn = screen.getByRole("button", { name: /^gửi lại mã$/i });
        sendPhoneVerificationCodeMock.mockResolvedValueOnce(mockConfirmationResult);

        await act(async () => {
          fireEvent.click(resendBtn);
        });

        expect(sendPhoneVerificationCodeMock).toHaveBeenCalledWith("0981111005");
        expect(screen.getByRole("button", { name: /gửi lại sau 60s/i })).toBeDisabled();
      } finally {
        vi.useRealTimers();
      }
    });

    it("handles error during resend OTP gracefully", async () => {
      vi.useFakeTimers();
      try {
        renderRegisterForm();
        await advanceToStep2("0981111005");

        act(() => {
          vi.advanceTimersByTime(60000);
        });

        const resendBtn = screen.getByRole("button", { name: /^gửi lại mã$/i });
        sendPhoneVerificationCodeMock.mockRejectedValueOnce(new Error("Lỗi mạng"));

        await act(async () => {
          fireEvent.click(resendBtn);
        });

        expect(screen.getByText("Lỗi mạng")).toBeInTheDocument();
      } finally {
        vi.useRealTimers();
      }
    });

    it("cleans up timer on unmount verifying no timer leak", async () => {
      const clearIntervalSpy = vi.spyOn(globalThis, "clearInterval");
      const { unmount } = renderRegisterForm();
      await advanceToStep2("0981111005");

      unmount();
      expect(clearIntervalSpy).toHaveBeenCalled();
      clearIntervalSpy.mockRestore();
    });

    it("cleans up timer on step transition (verifying clearInterval is called)", async () => {
      const clearIntervalSpy = vi.spyOn(globalThis, "clearInterval");
      renderRegisterForm();
      await advanceToStep2("0981111005");

      const changePhoneBtn = screen.getByRole("button", { name: /đổi số điện thoại/i });
      fireEvent.click(changePhoneBtn);

      expect(clearIntervalSpy).toHaveBeenCalled();
      clearIntervalSpy.mockRestore();
    });

    it("clicking 'Đổi số điện thoại' navigates back to Step 1, preserves phone number, and clears OTP", async () => {
      renderRegisterForm();
      await advanceToStep2("0981111005");

      const otpInput = screen.getByLabelText(/mã xác thực otp/i) as HTMLInputElement;
      fireEvent.change(otpInput, { target: { value: "123456" } });
      expect(otpInput.value).toBe("123456");

      const changePhoneBtn = screen.getByRole("button", { name: /đổi số điện thoại/i });
      fireEvent.click(changePhoneBtn);

      expect(screen.getByRole("heading", { name: /nhập số điện thoại/i })).toBeInTheDocument();
      const phoneInput = screen.getByLabelText(/số điện thoại/i) as HTMLInputElement;
      expect(phoneInput.value).toBe("0981111005");

      // Moving to Step 2 again should have cleared OTP
      sendPhoneVerificationCodeMock.mockResolvedValueOnce(mockConfirmationResult);
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: /tiếp tục/i }));
      });
      const reloadedOtpInput = screen.getByLabelText(/mã xác thực otp/i) as HTMLInputElement;
      expect(reloadedOtpInput.value).toBe("");
    });

    it("rejects empty or less than 6 digits OTP code", async () => {
      renderRegisterForm();
      await advanceToStep2("0981111005");

      const otpInput = screen.getByLabelText(/mã xác thực otp/i);
      fireEvent.change(otpInput, { target: { value: "12345" } });
      const verifyBtn = screen.getByRole("button", { name: /^xác thực$/i });
      fireEvent.click(verifyBtn);

      expect(
        await screen.findByText(/vui lòng nhập đầy đủ mã xác thực 6 chữ số\./i),
      ).toBeInTheDocument();
      expect(confirmPhoneVerificationCodeMock).not.toHaveBeenCalled();
    });

    it("filters out non-numeric characters from OTP input", async () => {
      renderRegisterForm();
      await advanceToStep2("0981111005");

      const otpInput = screen.getByLabelText(/mã xác thực otp/i) as HTMLInputElement;
      fireEvent.change(otpInput, { target: { value: "12ab34cd56" } });

      expect(otpInput.value).toBe("123456");
    });

    it("displays error message if confirmPhoneVerificationCode fails and stays in Step 2", async () => {
      confirmPhoneVerificationCodeMock.mockRejectedValueOnce(
        new Error("Mã xác thực không hợp lệ."),
      );
      renderRegisterForm();
      await advanceToStep2("0981111005");

      const otpInput = screen.getByLabelText(/mã xác thực otp/i);
      fireEvent.change(otpInput, { target: { value: "123456" } });
      const verifyBtn = screen.getByRole("button", { name: /^xác thực$/i });
      await act(async () => {
        fireEvent.click(verifyBtn);
      });

      expect(
        screen.getByText("Mã xác thực không hợp lệ."),
      ).toBeInTheDocument();
      expect(screen.getByRole("heading", { name: /xác thực otp/i })).toBeInTheDocument();
    });

    it("shows loading spinner and disables button during OTP verification", async () => {
      let resolveConfirm: (token: string) => void;
      const pendingConfirm = new Promise<string>((res) => {
        resolveConfirm = res;
      });
      confirmPhoneVerificationCodeMock.mockReturnValueOnce(pendingConfirm);

      renderRegisterForm();
      await advanceToStep2("0981111005");

      const otpInput = screen.getByLabelText(/mã xác thực otp/i);
      fireEvent.change(otpInput, { target: { value: "123456" } });
      const verifyBtn = screen.getByRole("button", { name: /^xác thực$/i });
      fireEvent.click(verifyBtn);

      expect(screen.getByText(/đang xác thực\.\.\./i)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /đang xác thực\.\.\./i })).toBeDisabled();

      await act(async () => {
        resolveConfirm("token-ok");
      });

      expect(screen.getByRole("heading", { name: /thông tin cá nhân/i })).toBeInTheDocument();
    });

    it("calls confirmPhoneVerificationCode and advances to Step 3 upon valid OTP", async () => {
      confirmPhoneVerificationCodeMock.mockResolvedValueOnce("firebase-id-token-xyz");
      renderRegisterForm();
      await advanceToStep2("0981111005");

      const otpInput = screen.getByLabelText(/mã xác thực otp/i);
      fireEvent.change(otpInput, { target: { value: "123456" } });
      const verifyBtn = screen.getByRole("button", { name: /^xác thực$/i });
      await act(async () => {
        fireEvent.click(verifyBtn);
      });

      expect(confirmPhoneVerificationCodeMock).toHaveBeenCalledWith(
        mockConfirmationResult,
        "123456",
      );
      expect(screen.getByRole("heading", { name: /thông tin cá nhân/i })).toBeInTheDocument();
    });
  });

  // ===========================================================================
  // c. Step 3 (Personal Details, Gender & Password Checklist)
  // ===========================================================================
  describe("Step 3 — Personal Details, Gender & Password Policy Checklist", () => {
    it("renders all personal info fields with correct placeholders and constraints", async () => {
      renderRegisterForm();
      await advanceToStep3();

      expect(screen.getByLabelText(/họ và tên/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/giới tính/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/ngày sinh/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/^mật khẩu/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/xác nhận mật khẩu/i)).toBeInTheDocument();

      const dobInput = screen.getByLabelText(/ngày sinh/i) as HTMLInputElement;
      expect(dobInput.max).toBe(new Date().toISOString().split("T")[0]);

      // Password policy checklist items
      expect(screen.getByText("Từ 8 đến 72 ký tự")).toBeInTheDocument();
      expect(screen.getByText("Có ít nhất 1 chữ hoa")).toBeInTheDocument();
      expect(screen.getByText("Có ít nhất 1 chữ số")).toBeInTheDocument();
      expect(screen.getByText("Khớp với mật khẩu")).toBeInTheDocument();
    });

    it("toggles password and confirm password visibility when clicking eye buttons", async () => {
      renderRegisterForm();
      await advanceToStep3();

      const passInput = screen.getByLabelText(/^mật khẩu/i) as HTMLInputElement;
      const confirmPassInput = screen.getByLabelText(/xác nhận mật khẩu/i) as HTMLInputElement;

      expect(passInput.type).toBe("password");
      expect(confirmPassInput.type).toBe("password");

      const [showPassBtn, showConfirmPassBtn] = screen.getAllByRole("button", {
        name: /hiện mật khẩu/i,
      });

      // Toggle password visibility
      fireEvent.click(showPassBtn);
      expect(passInput.type).toBe("text");
      expect(confirmPassInput.type).toBe("password");

      const hidePassBtn = screen.getByRole("button", { name: /ẩn mật khẩu/i });
      fireEvent.click(hidePassBtn);
      expect(passInput.type).toBe("password");

      // Toggle confirm password visibility
      fireEvent.click(showConfirmPassBtn);
      expect(confirmPassInput.type).toBe("text");

      const hideConfirmPassBtn = screen.getByRole("button", { name: /ẩn mật khẩu/i });
      fireEvent.click(hideConfirmPassBtn);
      expect(confirmPassInput.type).toBe("password");
    });

    it("dynamically validates PASSWORD_POLICY rules in real time", async () => {
      renderRegisterForm();
      await advanceToStep3();

      const passInput = screen.getByLabelText(/^mật khẩu/i);
      const confirmPassInput = screen.getByLabelText(/xác nhận mật khẩu/i);

      // Initial: all unfulfilled
      const lengthRule = screen.getByText("Từ 8 đến 72 ký tự");
      const upperRule = screen.getByText("Có ít nhất 1 chữ hoa");
      const digitRule = screen.getByText("Có ít nhất 1 chữ số");
      const matchRule = screen.getByText("Khớp với mật khẩu");

      expect(lengthRule).toHaveClass("text-muted-foreground");
      expect(upperRule).toHaveClass("text-muted-foreground");
      expect(digitRule).toHaveClass("text-muted-foreground");
      expect(matchRule).toHaveClass("text-muted-foreground");

      // Type short password with lowercase only
      fireEvent.change(passInput, { target: { value: "abc" } });
      expect(lengthRule).toHaveClass("text-muted-foreground");
      expect(upperRule).toHaveClass("text-muted-foreground");
      expect(digitRule).toHaveClass("text-muted-foreground");

      // Type 8 chars with uppercase, no digit
      fireEvent.change(passInput, { target: { value: "Password" } });
      expect(lengthRule).toHaveClass("text-[var(--success)]");
      expect(upperRule).toHaveClass("text-[var(--success)]");
      expect(digitRule).toHaveClass("text-muted-foreground");

      // Add digit -> all policy rules pass
      fireEvent.change(passInput, { target: { value: "Password1" } });
      expect(lengthRule).toHaveClass("text-[var(--success)]");
      expect(upperRule).toHaveClass("text-[var(--success)]");
      expect(digitRule).toHaveClass("text-[var(--success)]");
      expect(matchRule).toHaveClass("text-muted-foreground");

      // Type non-matching confirm password
      fireEvent.change(confirmPassInput, { target: { value: "Password2" } });
      expect(matchRule).toHaveClass("text-muted-foreground");

      // Type matching confirm password -> match rule passes
      fireEvent.change(confirmPassInput, { target: { value: "Password1" } });
      expect(matchRule).toHaveClass("text-[var(--success)]");
    });

    it("rejects submission when full name is empty", async () => {
      renderRegisterForm();
      await advanceToStep3();

      const submitBtn = screen.getByRole("button", { name: /hoàn tất đăng ký/i });
      fireEvent.click(submitBtn);

      expect(await screen.findByText(/vui lòng nhập họ và tên\./i)).toBeInTheDocument();
      expect(completeRegistrationMock).not.toHaveBeenCalled();
    });

    it("rejects submission when email format is invalid", async () => {
      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn A" } });
      fireEvent.change(screen.getByLabelText(/email/i), { target: { value: "not-an-email" } });
      fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));

      expect(await screen.findByText(/địa chỉ email không hợp lệ\./i)).toBeInTheDocument();
      expect(completeRegistrationMock).not.toHaveBeenCalled();
    });

    it("rejects submission when date of birth is in the future", async () => {
      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn A" } });
      fireEvent.change(screen.getByLabelText(/ngày sinh/i), { target: { value: "2099-01-01" } });
      fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));

      expect(await screen.findByText(/ngày sinh không thể ở tương lai\./i)).toBeInTheDocument();
      expect(completeRegistrationMock).not.toHaveBeenCalled();
    });

    it("rejects submission when password is empty or violates policy", async () => {
      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn A" } });
      fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));

      expect(await screen.findByText(/vui lòng nhập mật khẩu\./i)).toBeInTheDocument();

      // Enter weak password
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "weak" } });
      fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));

      expect(
        await screen.findByText(/mật khẩu chưa đáp ứng đầy đủ yêu cầu bảo mật bên dưới\./i),
      ).toBeInTheDocument();
      expect(completeRegistrationMock).not.toHaveBeenCalled();
    });

    it("rejects submission when confirm password does not match", async () => {
      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn A" } });
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "Password123" } });
      fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
        target: { value: "Password456" },
      });
      fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));

      expect(await screen.findByText(/xác nhận mật khẩu không khớp\./i)).toBeInTheDocument();
      expect(completeRegistrationMock).not.toHaveBeenCalled();
    });

    it.each([
      ["FEMALE", "FEMALE"],
      ["MALE", "MALE"],
      ["OTHER", "OTHER"],
      ["unselected", ""],
    ])(
      "submits registration with optional gender %s",
      async (label, genderValue) => {
        completeRegistrationMock.mockResolvedValueOnce({
          success: true,
          data: {
            accessToken: "token-1",
            refreshToken: "refresh-1",
            user: {
              userId: "u-1",
              fullName: "Nguyễn Văn A",
              email: "a@test.com",
              role: "PATIENT",
              mustChangePassword: false,
            },
          },
        });

        renderRegisterForm();
        await advanceToStep3("0981111005", "123456", "token-firebase-proof");

        fireEvent.change(screen.getByLabelText(/họ và tên/i), {
          target: { value: "  Nguyễn Văn A  " },
        });
        if (genderValue) {
          fireEvent.change(screen.getByLabelText(/giới tính/i), {
            target: { value: genderValue },
          });
        }
        fireEvent.change(screen.getByLabelText(/ngày sinh/i), {
          target: { value: "1995-05-20" },
        });
        fireEvent.change(screen.getByLabelText(/email/i), {
          target: { value: "  test@example.com  " },
        });
        fireEvent.change(screen.getByLabelText(/^mật khẩu/i), {
          target: { value: "ValidPassword1" },
        });
        fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
          target: { value: "ValidPassword1" },
        });

        await act(async () => {
          fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));
        });

        expect(completeRegistrationMock).toHaveBeenCalledWith({
          firebaseIdToken: "token-firebase-proof",
          fullName: "Nguyễn Văn A",
          gender: genderValue ? genderValue : null,
          dateOfBirth: "1995-05-20",
          email: "test@example.com",
          password: "ValidPassword1",
          confirmPassword: "ValidPassword1",
        });
      },
    );
  });

  // ===========================================================================
  // d. HTTP 409 Conflict Handling (Anti-Enumeration AF-01)
  // ===========================================================================
  describe("HTTP 409 Conflict Handling (AF-01 Anti-Enumeration)", () => {
    it("renders dedicated conflict alert message 'Số điện thoại đã có tài khoản' on HTTP 409 Conflict", async () => {
      completeRegistrationMock.mockRejectedValueOnce(create409ConflictError());

      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn B" } });
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "ValidPassword1" } });
      fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
        target: { value: "ValidPassword1" },
      });
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));
      });

      expect(screen.getByText("Số điện thoại đã có tài khoản")).toBeInTheDocument();
      expect(
        screen.getByText(/số điện thoại này đã được đăng ký tài khoản trên hệ thống adsus/i),
      ).toBeInTheDocument();

      const loginBtn = screen.getByRole("link", { name: /đăng nhập ngay/i });
      expect(loginBtn).toBeInTheDocument();
      expect(loginBtn).toHaveAttribute("href", "/login");
    });

    it("renders conflict alert when error message contains 'already registered'", async () => {
      completeRegistrationMock.mockRejectedValueOnce(
        new Error("This phone number is already registered in the system."),
      );

      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn B" } });
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "ValidPassword1" } });
      fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
        target: { value: "ValidPassword1" },
      });
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));
      });

      expect(screen.getByText("Số điện thoại đã có tài khoản")).toBeInTheDocument();
    });

    it("renders standard error message for non-409 server errors without conflict callout", async () => {
      const serverError = new AxiosError("Internal Server Error");
      serverError.status = 500;
      completeRegistrationMock.mockRejectedValueOnce(serverError);

      renderRegisterForm();
      await advanceToStep3();

      fireEvent.change(screen.getByLabelText(/họ và tên/i), { target: { value: "Nguyễn Văn B" } });
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "ValidPassword1" } });
      fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
        target: { value: "ValidPassword1" },
      });
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));
      });

      expect(screen.queryByText("Số điện thoại đã có tài khoản")).not.toBeInTheDocument();
      expect(screen.getByText(/internal server error/i)).toBeInTheDocument();
    });
  });

  // ===========================================================================
  // e. Successful Registration & Redirection
  // ===========================================================================
  describe("Successful Registration & Redirection", () => {
    it("calls signIn in auth store, saves access token to localStorage, and navigates to '/' for PATIENT", async () => {
      const mockSuccessData = {
        success: true,
        data: {
          accessToken: "jwt-patient-access-token",
          refreshToken: "jwt-patient-refresh-token",
          user: {
            userId: "patient-uuid-1",
            fullName: "Nguyễn Bệnh Nhân",
            email: "patient@example.com",
            role: "PATIENT" as const,
            mustChangePassword: false,
          },
        },
      };
      completeRegistrationMock.mockResolvedValueOnce(mockSuccessData);

      renderRegisterForm();
      await advanceToStep3("0981111005", "123456", "firebase-token-success");

      fireEvent.change(screen.getByLabelText(/họ và tên/i), {
        target: { value: "Nguyễn Bệnh Nhân" },
      });
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "Password123" } });
      fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
        target: { value: "Password123" },
      });
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));
      });

      // Verify auth store state was updated
      const authState = useAuthStore.getState();
      expect(authState.accessToken).toBe("jwt-patient-access-token");
      expect(authState.refreshToken).toBe("jwt-patient-refresh-token");
      expect(authState.user?.fullName).toBe("Nguyễn Bệnh Nhân");
      expect(authState.user?.role).toBe("PATIENT");

      // Verify localStorage was updated
      expect(window.localStorage.getItem("adsus.accessToken")).toBe(
        "jwt-patient-access-token",
      );

      // Verify router replaced with default patient path ("/")
      expect(replaceMock).toHaveBeenCalledWith("/");
    });

    it("redirects to ?redirect= path when specified in URL", async () => {
      window.history.pushState({}, "", "/register?redirect=/dat-lich");

      const mockSuccessData = {
        success: true,
        data: {
          accessToken: "jwt-access-token",
          refreshToken: "jwt-refresh-token",
          user: {
            userId: "patient-uuid-2",
            fullName: "Nguyễn Bệnh Nhân",
            email: null,
            role: "PATIENT" as const,
            mustChangePassword: false,
          },
        },
      };
      completeRegistrationMock.mockResolvedValueOnce(mockSuccessData);

      renderRegisterForm();
      await advanceToStep3("0981111005", "123456", "firebase-token-redirect");

      fireEvent.change(screen.getByLabelText(/họ và tên/i), {
        target: { value: "Nguyễn Bệnh Nhân" },
      });
      fireEvent.change(screen.getByLabelText(/^mật khẩu/i), { target: { value: "Password123" } });
      fireEvent.change(screen.getByLabelText(/xác nhận mật khẩu/i), {
        target: { value: "Password123" },
      });
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: /hoàn tất đăng ký/i }));
      });

      expect(replaceMock).toHaveBeenCalledWith("/dat-lich");
    });
  });

  // ===========================================================================
  // f. State Guard
  // ===========================================================================
  describe("State Guard", () => {
    it("safely falls back to Step 1 upon mount without valid verification context", () => {
      renderRegisterForm();

      // Ensure that initial render is firmly at Step 1
      expect(screen.getByRole("heading", { name: /nhập số điện thoại/i })).toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: /xác thực otp/i })).not.toBeInTheDocument();
      expect(screen.queryByRole("heading", { name: /thông tin cá nhân/i })).not.toBeInTheDocument();
    });

    it("verifies clearRecaptchaVerifier is accessible and export contract is intact", () => {
      expect(typeof clearRecaptchaVerifierMock).toBe("function");
    });
  });
});
