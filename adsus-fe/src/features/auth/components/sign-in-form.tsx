"use client";

import { AlertCircle, Eye, EyeOff, Info, Loader2, Lock, Phone } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { getHomePathForRole, useAuthStore, useHasHydrated } from "@/store/auth-store";

import { useSignIn } from "../hooks/use-sign-in";
import { getSignInErrorMessage } from "../lib/auth-messages";

export function SignInForm() {
  const router = useRouter();
  const hasHydrated = useHasHydrated();
  const accessToken = useAuthStore((s) => s.accessToken);
  const user = useAuthStore((s) => s.user);

  const [phoneNumber, setPhoneNumber] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [clientError, setClientError] = useState<string | null>(null);

  const signIn = useSignIn();

  // Bị đá ra vì token hết hiệu lực (hết hạn, hoặc tài khoản vừa bị Admin khoá). Không nói
  // gì thì người dùng tưởng hệ thống tự đăng xuất vô cớ.
  const wasExpired = useSearchParams().get("expired") === "1";

  // Vào thẳng "/" hoặc gõ tay "/login" khi phiên trong localStorage vẫn còn hợp lệ — trước
  // đây bị kẹt ở form đăng nhập vì AuthGuard (app/(protected)/layout.tsx) không bọc trang
  // này. userId thiếu nghĩa là phiên cũ trước 04/08/2026, cứ để rơi xuống form đăng nhập
  // lại thay vì tự chuyển, an toàn hơn là chuyển tới trang rồi vỡ vì thiếu field.
  const alreadySignedIn = hasHydrated && Boolean(accessToken) && Boolean(user?.userId);

  useEffect(() => {
    if (!alreadySignedIn || !user) return;
    router.replace(getHomePathForRole(user.role));
  }, [alreadySignedIn, user, router]);

  // Chưa hydrate xong thì CHƯA BIẾT có phiên hay không — hiện spinner trong lúc đó thay vì
  // hiện form thật, nếu không người đã đăng nhập vẫn thấy form loé lên rồi mới bị đá đi
  // (đúng lỗi vừa gặp). Cùng cách AuthGuard xử lý cho các trang protected.
  if (!hasHydrated || alreadySignedIn) {
    return (
      <div className="flex min-h-96 items-center justify-center">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
        <span className="sr-only">Đang kiểm tra phiên đăng nhập...</span>
      </div>
    );
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setClientError(null);

    if (!phoneNumber.trim() || !password) {
      setClientError("Vui lòng nhập số điện thoại và mật khẩu.");
      return;
    }

    signIn.mutate({ phoneNumber: phoneNumber.trim(), password });
  }

  // Every sign-in failure maps to ONE sentence, chosen by HTTP status rather than by the
  // backend's wording. Wrong phone number, wrong password, locked account and deactivated
  // account are indistinguishable to the user (UCS GB-06) — there is a single branch here,
  // so there is nothing to leak.
  const errorMessage =
    clientError ?? (signIn.isError ? getSignInErrorMessage(signIn.error) : null);

  const isSubmitting = signIn.isPending || signIn.isSuccess;

  // 56px tall, fully rounded — matches .services-form .form-group input in the team template.
  const inputClass =
    "h-14 rounded-full border-border bg-white pl-12 pr-4 text-[15px] shadow-none " +
    "focus-visible:border-accent focus-visible:ring-accent/25";

  return (
    <div className="w-full max-w-md">
      <div className="mb-9">
        <span className="font-heading text-sm font-600 uppercase tracking-[0.2em] text-accent">
          Chào mừng trở lại
        </span>
        <h1 className="mt-3 font-heading text-[42px] font-bold leading-[1.15] tracking-[-0.02em] text-foreground">
          Đăng nhập
        </h1>
        <p className="mt-3 text-[15px] leading-relaxed text-muted-foreground">
          Sử dụng số điện thoại đã được cấp để truy cập hệ thống.
        </p>
      </div>

      {wasExpired && (
        <div className="mb-6 flex items-start gap-2.5 rounded-2xl border border-border bg-secondary/50 px-4 py-3 text-sm text-muted-foreground">
          <Info aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>
            Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại. Nếu tình trạng này lặp lại,
            hãy liên hệ quản trị viên.
          </span>
        </div>
      )}

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-5">
        <div className="flex flex-col gap-2.5">
          <Label
            htmlFor="phoneNumber"
            className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground"
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
              autoComplete="username"
              placeholder="0900000000"
              maxLength={15}
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              disabled={isSubmitting}
              aria-invalid={Boolean(errorMessage)}
              className={inputClass}
            />
          </div>
        </div>

        <div className="flex flex-col gap-2.5">
          <Label
            htmlFor="password"
            className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground"
          >
            Mật khẩu
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
              autoComplete="current-password"
              placeholder="••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={isSubmitting}
              aria-invalid={Boolean(errorMessage)}
              className={cn(inputClass, "pr-12")}
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              disabled={isSubmitting}
              aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
              className="absolute right-5 top-1/2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-accent disabled:opacity-50"
            >
              {showPassword ? (
                <EyeOff className="size-4.5" />
              ) : (
                <Eye className="size-4.5" />
              )}
            </button>
          </div>
        </div>

        <div
          className={cn(
            "flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive",
            !errorMessage && "hidden",
          )}
          role="alert"
          aria-live="polite"
        >
          <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>{errorMessage}</span>
        </div>

        {/* Pill shape, teal background, uppercase label — .btn-style-one from the template */}
        <button
          type="submit"
          disabled={isSubmitting}
          className="mt-1 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-accent font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90 hover:shadow-accent/35 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSubmitting ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Đang đăng nhập
            </>
          ) : (
            "Đăng nhập"
          )}
        </button>

        {/* UC-03 Main Flow bước 1 — lối vào chức năng tự cấp lại mật khẩu. */}
        <p className="text-center text-sm text-muted-foreground">
          <Link
            href="/forgot-password"
            className="font-600 text-accent transition-colors hover:text-accent/80"
          >
            Quên mật khẩu?
          </Link>
        </p>
      </form>
    </div>
  );
}
