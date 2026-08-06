"use client";

import { useMutation } from "@tanstack/react-query";
import { AlertCircle, ArrowLeft, Loader2, Mail, MailCheck, Phone } from "lucide-react";
import Link from "next/link";
import { useState, type FormEvent } from "react";

import { getApiErrorMessage } from "@/lib/api-client";
import {
  PHONE_ERROR_MESSAGE,
  PHONE_MAX_LENGTH,
  isValidPhoneNumber,
} from "@/lib/phone-number";

import { forgotPassword } from "../api/auth.api";

/**
 * UC-03 FT-06 — người dùng tự yêu cầu cấp lại mật khẩu.
 *
 * Màn này KHÔNG có SCR-ID riêng trong Screen List của PRD; UCS đã ghi nhận đó là một khoảng
 * trống và để lại cho FDS. Ở đây dựng theo đúng mô tả của Main Flow bước 1–3, mở từ SCR-01.
 *
 * Điểm quan trọng nhất: dù thông tin đúng hay sai, người dùng LUÔN thấy đúng một câu trả lời
 * (AF-01). Nếu báo "số điện thoại không tồn tại" thì màn này thành công cụ để dò xem số nào
 * đã có tài khoản trong hệ thống.
 */
export function ForgotPasswordForm() {
  const [phoneNumber, setPhoneNumber] = useState("");
  const [email, setEmail] = useState("");
  const [clientError, setClientError] = useState<string | null>(null);

  const request = useMutation({
    mutationFn: () =>
      forgotPassword({ phoneNumber: phoneNumber.trim(), email: email.trim() }),
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setClientError(null);

    if (!phoneNumber.trim() || !email.trim()) {
      setClientError("Vui lòng nhập số điện thoại và email đã đăng ký.");
      return;
    }

    // Kiểm định dạng ở đây KHÔNG phạm AF-01: câu báo lỗi chỉ nói "chuỗi này không thể là số
    // điện thoại", đúng với mọi giá trị sai dạng, chứ không hé lộ số đó có tài khoản hay
    // không. Thiếu bước này thì gõ nhầm một chữ số vẫn nhận được câu "đã gửi yêu cầu", rồi
    // ngồi chờ mail mãi không tới mà tưởng hệ thống hỏng.
    if (!isValidPhoneNumber(phoneNumber)) {
      setClientError(PHONE_ERROR_MESSAGE);
      return;
    }

    request.mutate();
  }

  // Gửi xong thì thay hẳn form bằng lời nhắn, để không ai bấm gửi liên tục.
  if (request.isSuccess) {
    return (
      <div className="w-full max-w-md">
        <span className="flex size-12 items-center justify-center rounded-full bg-accent/12">
          <MailCheck className="size-6 text-accent" />
        </span>

        <h1 className="mt-6 font-heading text-[32px] font-bold leading-[1.15] tracking-[-0.02em] text-foreground">
          Đã gửi yêu cầu
        </h1>

        {/* Câu này cố tình mơ hồ — xem chú thích ở đầu tệp (AF-01). */}
        <p className="mt-4 text-[15px] leading-relaxed text-muted-foreground">
          Nếu thông tin bạn nhập khớp với một tài khoản, hệ thống đã gửi mật khẩu mới tới
          email đó. Vui lòng kiểm tra hộp thư, kể cả mục spam.
        </p>

        <p className="mt-3 text-[15px] leading-relaxed text-muted-foreground">
          Đăng nhập bằng mật khẩu mới xong, hệ thống sẽ yêu cầu bạn đặt lại mật khẩu riêng.
        </p>

        <Link
          href="/login"
          className="mt-8 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-accent font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90"
        >
          Về trang đăng nhập
        </Link>
      </div>
    );
  }

  const errorMessage =
    clientError ??
    (request.isError
      ? getApiErrorMessage(request.error, "Không gửi được yêu cầu. Vui lòng thử lại.")
      : null);

  return (
    <div className="w-full max-w-md">
      <Link
        href="/login"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-accent"
      >
        <ArrowLeft className="size-4" />
        Quay lại đăng nhập
      </Link>

      <h1 className="mt-6 font-heading text-[38px] font-bold leading-[1.15] tracking-[-0.02em] text-foreground">
        Quên mật khẩu
      </h1>
      <p className="mt-3 text-[15px] leading-relaxed text-muted-foreground">
        Nhập số điện thoại và email đã đăng ký. Hệ thống sẽ gửi mật khẩu mới tới email đó.
      </p>

      <form onSubmit={handleSubmit} noValidate className="mt-8 flex flex-col gap-5">
        <FieldWithIcon icon={Phone} label="Số điện thoại">
          <input
            value={phoneNumber}
            onChange={(e) => setPhoneNumber(e.target.value)}
            disabled={request.isPending}
            type="tel"
            inputMode="numeric"
            maxLength={PHONE_MAX_LENGTH}
            placeholder="0900000000"
            className={inputClass}
          />
        </FieldWithIcon>

        <FieldWithIcon icon={Mail} label="Email đã đăng ký">
          <input
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            disabled={request.isPending}
            type="email"
            maxLength={255}
            placeholder="email@example.com"
            className={inputClass}
          />
        </FieldWithIcon>

        {errorMessage && (
          <div
            role="alert"
            className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
          >
            <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <button
          type="submit"
          disabled={request.isPending}
          className="mt-1 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-accent font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {request.isPending ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Đang gửi
            </>
          ) : (
            "Gửi mật khẩu mới"
          )}
        </button>

        <p className="text-center text-sm text-muted-foreground">
          Không nhớ email đã đăng ký? Liên hệ quản trị viên để được cấp lại.
        </p>
      </form>
    </div>
  );
}

const inputClass =
  "h-14 w-full rounded-full border border-border bg-white pl-12 pr-4 text-[15px] outline-none transition-colors focus-visible:border-accent";

function FieldWithIcon({
  icon: Icon,
  label,
  children,
}: {
  icon: typeof Phone;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="flex flex-col gap-2.5">
      <span className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground">
        {label}
      </span>
      <span className="relative block">
        <Icon
          aria-hidden
          className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
        />
        {children}
      </span>
    </label>
  );
}
