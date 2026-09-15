"use client";

import type { ConfirmationResult } from "firebase/auth";
import {
  AlertCircle,
  ArrowLeft,
  Check,
  Eye,
  EyeOff,
  Info,
  KeyRound,
  Loader2,
  Lock,
  Mail,
  Phone,
  User,
  X,
} from "lucide-react";
import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

import { useCompleteRegistration } from "../hooks/use-complete-registration";
import {
  confirmPhoneVerificationCode,
  sendPhoneVerificationCode,
} from "../services/firebase-phone-auth.service";
import { PASSWORD_POLICY, type RegisterStep } from "../types/auth.types";

const inputClass =
  "h-14 rounded-full border-border bg-white pl-12 pr-4 text-[15px] shadow-none " +
  "focus-visible:border-[var(--success)] focus-visible:ring-[var(--success)]/25";

export function RegisterForm() {
  const [step, setStep] = useState<RegisterStep>("phone");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [confirmationResult, setConfirmationResult] = useState<ConfirmationResult | null>(null);
  const [otpCode, setOtpCode] = useState("");
  const [countdown, setCountdown] = useState(60);
  const [isSendingCode, setIsSendingCode] = useState(false);
  const [isVerifyingOtp, setIsVerifyingOtp] = useState(false);
  const [firebaseIdToken, setFirebaseIdToken] = useState<string | null>(null);

  // Step 3 Profile fields
  const [fullName, setFullName] = useState("");
  const [gender, setGender] = useState<"MALE" | "FEMALE" | "OTHER" | "">("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const [stepError, setStepError] = useState<string | null>(null);

  const completeRegistrationMutation = useCompleteRegistration();

  // State Guard: If user reloads or navigates at Step 2/3 without necessary context, fallback safely to Step 1
  const activeStep: RegisterStep =
    step === "otp" && !confirmationResult
      ? "phone"
      : step === "profile" && !firebaseIdToken
        ? "phone"
        : step;

  // 60-second countdown timer on Step 2 with automatic cleanup on unmount or step transition
  useEffect(() => {
    if (activeStep !== "otp" || countdown <= 0) return;

    const timer = setInterval(() => {
      setCountdown((prev) => (prev > 0 ? prev - 1 : 0));
    }, 1000);

    return () => clearInterval(timer);
  }, [activeStep, countdown]);

  // Step 1: Submit phone number to Firebase
  async function handleSendOtp(e?: FormEvent) {
    if (e) e.preventDefault();
    setStepError(null);

    const trimmedPhone = phoneNumber.trim();
    if (!trimmedPhone) {
      setStepError("Vui lòng nhập số điện thoại.");
      return;
    }

    // Must be 10 digits starting with 0
    if (!/^0\d{9}$/.test(trimmedPhone)) {
      setStepError("Số điện thoại không hợp lệ. Vui lòng nhập 10 chữ số bắt đầu bằng 0 (ví dụ: 0981111005).");
      return;
    }

    try {
      setIsSendingCode(true);
      const result = await sendPhoneVerificationCode(trimmedPhone);
      setConfirmationResult(result);
      setCountdown(60);
      setStep("otp");
    } catch (err) {
      const msg =
        err instanceof Error
          ? err.message
          : "Không thể gửi mã xác thực SMS. Vui lòng kiểm tra lại số điện thoại.";
      setStepError(msg);
    } finally {
      setIsSendingCode(false);
    }
  }

  // Step 2: Verify OTP
  async function handleVerifyOtp(e: FormEvent) {
    e.preventDefault();
    setStepError(null);

    const trimmedOtp = otpCode.trim();
    if (!trimmedOtp || trimmedOtp.length < 6) {
      setStepError("Vui lòng nhập đầy đủ mã xác thực 6 chữ số.");
      return;
    }

    if (!confirmationResult) {
      setStep("phone");
      return;
    }

    try {
      setIsVerifyingOtp(true);
      const token = await confirmPhoneVerificationCode(confirmationResult, trimmedOtp);
      setFirebaseIdToken(token);
      setStep("profile");
    } catch (err) {
      const msg =
        err instanceof Error
          ? err.message
          : "Mã xác thực không chính xác hoặc đã hết hạn. Vui lòng thử lại.";
      setStepError(msg);
    } finally {
      setIsVerifyingOtp(false);
    }
  }

  // Resend OTP in Step 2
  async function handleResendOtp() {
    if (countdown > 0 || isSendingCode) return;
    setStepError(null);
    try {
      setIsSendingCode(true);
      const result = await sendPhoneVerificationCode(phoneNumber.trim());
      setConfirmationResult(result);
      setCountdown(60);
    } catch (err) {
      const msg =
        err instanceof Error
          ? err.message
          : "Không thể gửi lại mã xác thực. Vui lòng thử lại.";
      setStepError(msg);
    } finally {
      setIsSendingCode(false);
    }
  }

  // Step 3: Password policy validation
  const policyChecks = PASSWORD_POLICY.rules.map((rule) => ({
    label: rule.label,
    passed: rule.test(password),
  }));
  const confirmMatches =
    confirmPassword.length > 0 && confirmPassword === password;

  // Step 3: Complete registration submit
  function handleCompleteRegistration(e: FormEvent) {
    e.preventDefault();
    setStepError(null);

    if (!firebaseIdToken) {
      setStep("phone");
      return;
    }

    if (!fullName.trim()) {
      setStepError("Vui lòng nhập họ và tên.");
      return;
    }

    if (email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setStepError("Địa chỉ email không hợp lệ.");
      return;
    }

    if (dateOfBirth) {
      const dobDate = new Date(dateOfBirth);
      const today = new Date();
      if (dobDate > today) {
        setStepError("Ngày sinh không thể ở tương lai.");
        return;
      }
    }

    if (!password) {
      setStepError("Vui lòng nhập mật khẩu.");
      return;
    }

    if (policyChecks.some((c) => !c.passed)) {
      setStepError("Mật khẩu chưa đáp ứng đầy đủ yêu cầu bảo mật bên dưới.");
      return;
    }

    if (password !== confirmPassword) {
      setStepError("Xác nhận mật khẩu không khớp.");
      return;
    }

    completeRegistrationMutation.mutate({
      firebaseIdToken,
      fullName: fullName.trim(),
      password,
      confirmPassword,
      email: email.trim() ? email.trim() : null,
      dateOfBirth: dateOfBirth || null,
      gender: gender ? gender : null,
    });
  }

  // Detect HTTP 409 Conflict from backend
  const errorObj = completeRegistrationMutation.error as unknown;
  const is409Conflict =
    completeRegistrationMutation.isError &&
    (Boolean(
      errorObj &&
        typeof errorObj === "object" &&
        ("status" in errorObj
          ? (errorObj as { status?: number }).status === 409
          : "response" in errorObj &&
            (errorObj as { response?: { status?: number } }).response?.status === 409),
    ) ||
      (errorObj instanceof Error &&
        errorObj.message.toLowerCase().includes("already registered")) ||
      (Boolean(
        errorObj &&
          typeof errorObj === "object" &&
          "response" in errorObj &&
          (errorObj as { response?: { data?: { detail?: string; message?: string } } })
            .response?.data?.detail?.toLowerCase()
            ?.includes("already registered"),
      )));

  const isSubmittingProfile =
    completeRegistrationMutation.isPending || completeRegistrationMutation.isSuccess;

  return (
    <div className="w-full max-w-md">
      {/* Invisible Recaptcha container mount point */}
      <div id="recaptcha-container" aria-hidden className="hidden" />

      {/* Header & Step progress */}
      <div className="mb-8 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-2 motion-safe:duration-500">
        <span className="inline-flex items-center gap-2 text-sm font-700 uppercase tracking-[0.2em] text-[var(--success)]">
          <span aria-hidden className="relative flex size-1.5">
            <span className="absolute inline-flex size-full animate-ping rounded-full bg-[var(--success)] opacity-75" />
            <span className="relative inline-flex size-1.5 rounded-full bg-[var(--success)]" />
          </span>
          Đăng ký tài khoản bệnh nhân
        </span>
        <h1 className="mt-3 text-[36px] sm:text-[40px] font-bold leading-[1.15] tracking-[-0.02em] text-foreground">
          {activeStep === "phone" && "Nhập số điện thoại"}
          {activeStep === "otp" && "Xác thực OTP"}
          {activeStep === "profile" && "Thông tin cá nhân"}
        </h1>
        <p className="mt-2.5 text-[15px] leading-relaxed text-muted-foreground">
          {activeStep === "phone" && "ADSUS gửi mã xác thực SMS tới số điện thoại của bạn."}
          {activeStep === "otp" && `Nhập mã xác thực gồm 6 chữ số đã gửi tới số ${phoneNumber}.`}
          {activeStep === "profile" && "Hoàn tất các thông tin bên dưới để khởi tạo hồ sơ bệnh nhân."}
        </p>

        {/* Step indicator breadcrumb */}
        <div className="mt-5 flex items-center gap-2 text-xs font-semibold text-muted-foreground">
          <span
            className={cn(
              "flex size-6 items-center justify-center rounded-full transition-colors",
              activeStep === "phone"
                ? "bg-[var(--success)] text-white"
                : "bg-secondary text-foreground",
            )}
          >
            1
          </span>
          <span className={activeStep === "phone" ? "text-foreground font-bold" : ""}>Số điện thoại</span>
          <span className="text-muted-foreground/40">/</span>
          <span
            className={cn(
              "flex size-6 items-center justify-center rounded-full transition-colors",
              activeStep === "otp"
                ? "bg-[var(--success)] text-white"
                : activeStep === "profile"
                  ? "bg-[var(--success)]/20 text-[var(--success)]"
                  : "bg-secondary text-foreground",
            )}
          >
            2
          </span>
          <span className={activeStep === "otp" ? "text-foreground font-bold" : ""}>Mã OTP</span>
          <span className="text-muted-foreground/40">/</span>
          <span
            className={cn(
              "flex size-6 items-center justify-center rounded-full transition-colors",
              activeStep === "profile"
                ? "bg-[var(--success)] text-white"
                : "bg-secondary text-foreground",
            )}
          >
            3
          </span>
          <span className={activeStep === "profile" ? "text-foreground font-bold" : ""}>Thông tin</span>
        </div>
      </div>

      {/* STEP 1: PHONE NUMBER */}
      {activeStep === "phone" && (
        <form onSubmit={handleSendOtp} noValidate className="flex flex-col gap-5">
          {/* Dev Hint banner in development mode */}
          {process.env.NODE_ENV === "development" && (
            <div
              className="rounded-2xl border border-dashed border-[var(--accent)]/40 bg-[var(--accent)]/10 p-4 text-xs text-foreground"
              role="note"
            >
              <div className="flex items-center gap-1.5 font-bold text-[var(--accent)]">
                <Info className="size-4 shrink-0" />
                <span>Chế độ phát triển (Dev Hint)</span>
              </div>
              <p className="mt-1.5 text-muted-foreground leading-relaxed">
                Số điện thoại test Firebase:{" "}
                <button
                  type="button"
                  onClick={() => setPhoneNumber("0981111005")}
                  className="font-mono font-bold text-foreground underline hover:text-[var(--success)]"
                  title="Nhấn để điền số test"
                >
                  0981111005 (+84981111005)
                </button>
                <br />
                Mã OTP thử nghiệm: <strong className="font-mono text-foreground">123456</strong>
              </p>
            </div>
          )}

          <div className="flex flex-col gap-2.5">
            <Label
              htmlFor="phoneNumber"
              className="text-[13px] font-700 uppercase tracking-wider text-foreground"
            >
              Số điện thoại
            </Label>
            <div className="relative">
              <Phone
                aria-hidden
                className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                id="phoneNumber"
                name="phoneNumber"
                type="tel"
                inputMode="numeric"
                autoComplete="tel"
                placeholder="0xxxxxxxxx"
                maxLength={10}
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                disabled={isSendingCode}
                className={inputClass}
              />
            </div>
          </div>

          {stepError && (
            <div
              className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
              role="alert"
            >
              <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
              <span>{stepError}</span>
            </div>
          )}

          <button
            type="submit"
            disabled={isSendingCode}
            className="mt-1 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-[var(--success)] text-sm font-700 uppercase tracking-wider text-white shadow-lg shadow-[var(--success)]/25 transition-all hover:bg-[var(--success)]/90 hover:shadow-[var(--success)]/35 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSendingCode ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Đang gửi mã...
              </>
            ) : (
              "Tiếp tục"
            )}
          </button>

          <p className="text-center text-sm text-muted-foreground">
            Đã có tài khoản?{" "}
            <Link
              href="/login"
              className="font-600 text-[var(--success)] transition-colors hover:text-white/80"
            >
              Đăng nhập ngay
            </Link>
          </p>
        </form>
      )}

      {/* STEP 2: OTP VERIFICATION */}
      {activeStep === "otp" && (
        <form onSubmit={handleVerifyOtp} noValidate className="flex flex-col gap-5">
          <div className="flex flex-col gap-2.5">
            <div className="flex items-center justify-between">
              <Label
                htmlFor="otpCode"
                className="text-[13px] font-700 uppercase tracking-wider text-foreground"
              >
                Mã xác thực OTP
              </Label>
              <button
                type="button"
                onClick={() => {
                  setStep("phone");
                  setOtpCode("");
                }}
                className="inline-flex items-center gap-1 text-xs font-semibold text-[var(--success)] hover:underline"
              >
                <ArrowLeft className="size-3" />
                Đổi số điện thoại
              </button>
            </div>
            <div className="relative">
              <KeyRound
                aria-hidden
                className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                id="otpCode"
                name="otpCode"
                type="text"
                inputMode="numeric"
                autoComplete="one-time-code"
                placeholder="123456"
                maxLength={6}
                value={otpCode}
                onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, ""))}
                disabled={isVerifyingOtp}
                className={cn(inputClass, "font-mono tracking-[0.25em] text-center pr-12 text-lg")}
              />
            </div>
          </div>

          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>Chưa nhận được mã?</span>
            <button
              type="button"
              disabled={countdown > 0 || isSendingCode}
              onClick={handleResendOtp}
              className="font-semibold text-[var(--success)] transition-colors hover:underline disabled:cursor-not-allowed disabled:text-muted-foreground disabled:no-underline"
            >
              {countdown > 0 ? `Gửi lại sau ${countdown}s` : "Gửi lại mã"}
            </button>
          </div>

          {stepError && (
            <div
              className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
              role="alert"
            >
              <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
              <span>{stepError}</span>
            </div>
          )}

          <button
            type="submit"
            disabled={isVerifyingOtp}
            className="mt-1 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-[var(--success)] text-sm font-700 uppercase tracking-wider text-white shadow-lg shadow-[var(--success)]/25 transition-all hover:bg-[var(--success)]/90 hover:shadow-[var(--success)]/35 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isVerifyingOtp ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Đang xác thực...
              </>
            ) : (
              "Xác thực"
            )}
          </button>
        </form>
      )}

      {/* STEP 3: PERSONAL DETAILS & PASSWORD */}
      {activeStep === "profile" && (
        <form onSubmit={handleCompleteRegistration} noValidate className="flex flex-col gap-4">
          {/* Full name */}
          <div className="flex flex-col gap-2">
            <Label
              htmlFor="fullName"
              className="text-[13px] font-700 uppercase tracking-wider text-foreground"
            >
              Họ và tên <span className="text-destructive">*</span>
            </Label>
            <div className="relative">
              <User
                aria-hidden
                className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                id="fullName"
                name="fullName"
                type="text"
                autoComplete="name"
                placeholder="Nguyễn Văn A"
                maxLength={100}
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                disabled={isSubmittingProfile}
                className={inputClass}
              />
            </div>
          </div>

          {/* Gender & Date of Birth row */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Gender */}
            <div className="flex flex-col gap-2">
              <Label
                htmlFor="gender"
                className="text-[13px] font-700 uppercase tracking-wider text-foreground"
              >
                Giới tính
              </Label>
              <select
                id="gender"
                name="gender"
                value={gender}
                onChange={(e) => setGender(e.target.value as "MALE" | "FEMALE" | "OTHER" | "")}
                disabled={isSubmittingProfile}
                className={cn(
                  inputClass,
                  "pl-5 pr-8 appearance-none bg-white cursor-pointer",
                )}
              >
                <option value="">Chọn giới tính</option>
                <option value="FEMALE">Nữ</option>
                <option value="MALE">Nam</option>
                <option value="OTHER">Khác</option>
              </select>
            </div>

            {/* Date of Birth */}
            <div className="flex flex-col gap-2">
              <Label
                htmlFor="dateOfBirth"
                className="text-[13px] font-700 uppercase tracking-wider text-foreground"
              >
                Ngày sinh
              </Label>
              <Input
                id="dateOfBirth"
                name="dateOfBirth"
                type="date"
                max={new Date().toISOString().split("T")[0]}
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
                disabled={isSubmittingProfile}
                className={cn(inputClass, "pl-5 cursor-pointer")}
              />
            </div>
          </div>

          {/* Email (optional) */}
          <div className="flex flex-col gap-2">
            <Label
              htmlFor="email"
              className="text-[13px] font-700 uppercase tracking-wider text-foreground"
            >
              Email (không bắt buộc)
            </Label>
            <div className="relative">
              <Mail
                aria-hidden
                className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                placeholder="name@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={isSubmittingProfile}
                className={inputClass}
              />
            </div>
          </div>

          {/* Password */}
          <div className="flex flex-col gap-2">
            <Label
              htmlFor="password"
              className="text-[13px] font-700 uppercase tracking-wider text-foreground"
            >
              Mật khẩu <span className="text-destructive">*</span>
            </Label>
            <div className="relative">
              <Lock
                aria-hidden
                className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                id="password"
                name="password"
                type={showPassword ? "text" : "password"}
                autoComplete="new-password"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={isSubmittingProfile}
                className={cn(inputClass, "pr-12")}
              />
              <button
                type="button"
                onClick={() => setShowPassword((v) => !v)}
                disabled={isSubmittingProfile}
                aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                className="absolute right-5 top-1/2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-[var(--success)] disabled:opacity-50"
              >
                {showPassword ? <EyeOff className="size-4.5" /> : <Eye className="size-4.5" />}
              </button>
            </div>
          </div>

          {/* Confirm Password */}
          <div className="flex flex-col gap-2">
            <Label
              htmlFor="confirmPassword"
              className="text-[13px] font-700 uppercase tracking-wider text-foreground"
            >
              Xác nhận mật khẩu <span className="text-destructive">*</span>
            </Label>
            <div className="relative">
              <Lock
                aria-hidden
                className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
              />
              <Input
                id="confirmPassword"
                name="confirmPassword"
                type={showConfirmPassword ? "text" : "password"}
                autoComplete="new-password"
                placeholder="••••••••"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                disabled={isSubmittingProfile}
                className={cn(inputClass, "pr-12")}
              />
              <button
                type="button"
                onClick={() => setShowConfirmPassword((v) => !v)}
                disabled={isSubmittingProfile}
                aria-label={showConfirmPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                className="absolute right-5 top-1/2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-[var(--success)] disabled:opacity-50"
              >
                {showConfirmPassword ? <EyeOff className="size-4.5" /> : <Eye className="size-4.5" />}
              </button>
            </div>
          </div>

          {/* Dynamic real-time Password Policy checklist */}
          <ul className="flex flex-col gap-2 rounded-2xl bg-secondary/60 px-5 py-3.5">
            {policyChecks.map(({ label, passed }) => (
              <li
                key={label}
                className={cn(
                  "flex items-center gap-2.5 text-xs transition-colors",
                  passed ? "text-[var(--success)] font-medium" : "text-muted-foreground",
                )}
              >
                {passed ? (
                  <Check aria-hidden className="size-3.5 shrink-0" />
                ) : (
                  <X aria-hidden className="size-3.5 shrink-0 opacity-40" />
                )}
                {label}
              </li>
            ))}
            <li
              className={cn(
                "flex items-center gap-2.5 text-xs transition-colors",
                confirmMatches ? "text-[var(--success)] font-medium" : "text-muted-foreground",
              )}
            >
              {confirmMatches ? (
                <Check aria-hidden className="size-3.5 shrink-0" />
              ) : (
                <X aria-hidden className="size-3.5 shrink-0 opacity-40" />
              )}
              Khớp với mật khẩu
            </li>
          </ul>

          {/* 409 Conflict dedicated error box with "Đăng nhập ngay" button */}
          {is409Conflict ? (
            <div
              className="flex flex-col gap-2.5 rounded-2xl border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive"
              role="alert"
            >
              <div className="flex items-center gap-2 font-bold text-destructive">
                <AlertCircle className="size-4.5 shrink-0" />
                <span>Số điện thoại đã có tài khoản</span>
              </div>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Số điện thoại này đã được đăng ký tài khoản trên hệ thống ADSUS. Vui lòng đăng nhập
                để tiếp tục.
              </p>
              <Link
                href="/login"
                className="mt-1 inline-flex h-11 w-full items-center justify-center gap-2 rounded-full bg-[var(--success)] text-xs font-700 uppercase tracking-wider text-white shadow-md shadow-[var(--success)]/25 transition-all hover:bg-[var(--success)]/90"
              >
                Đăng nhập ngay
              </Link>
            </div>
          ) : (
            (stepError || completeRegistrationMutation.isError) && (
              <div
                className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
                role="alert"
              >
                <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
                <span>
                  {stepError ??
                    ((completeRegistrationMutation.error as Error)?.message ||
                      "Đăng ký thất bại. Vui lòng thử lại.")}
                </span>
              </div>
            )
          )}

          <button
            type="submit"
            disabled={isSubmittingProfile}
            className="mt-2 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-[var(--success)] text-sm font-700 uppercase tracking-wider text-white shadow-lg shadow-[var(--success)]/25 transition-all hover:bg-[var(--success)]/90 hover:shadow-[var(--success)]/35 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmittingProfile ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Đang tạo tài khoản...
              </>
            ) : (
              "Hoàn tất đăng ký"
            )}
          </button>
        </form>
      )}
    </div>
  );
}
