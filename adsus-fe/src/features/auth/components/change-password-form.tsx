"use client";

import { AlertCircle, Check, CheckCircle2, Eye, EyeOff, Loader2, Lock, X } from "lucide-react";
import { useState, type FormEvent } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/store/auth-store";

import { useChangePassword } from "../hooks/use-change-password";
import { getChangePasswordErrorMessage } from "../lib/auth-messages";
import { PASSWORD_POLICY } from "../types/auth.types";

const inputClass =
  "h-14 rounded-full border-border bg-white pl-12 pr-12 text-[15px] shadow-none " +
  "focus-visible:border-accent focus-visible:ring-accent/25";

export function ChangePasswordForm() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [visible, setVisible] = useState<Record<string, boolean>>({});
  const [clientError, setClientError] = useState<string | null>(null);

  const mustChangePassword = useAuthStore((s) => s.user?.mustChangePassword ?? false);
  const changePassword = useChangePassword();

  const policyChecks = PASSWORD_POLICY.rules.map((rule) => ({
    label: rule.label,
    passed: rule.test(newPassword),
  }));

  const confirmMatches =
    confirmNewPassword.length > 0 && confirmNewPassword === newPassword;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setClientError(null);

    if (!currentPassword || !newPassword || !confirmNewPassword) {
      setClientError("Vui lòng điền đầy đủ cả ba ô.");
      return;
    }
    if (policyChecks.some((c) => !c.passed)) {
      setClientError("Mật khẩu mới chưa đạt yêu cầu bên dưới.");
      return;
    }
    if (newPassword !== confirmNewPassword) {
      setClientError("Xác nhận mật khẩu không khớp.");
      return;
    }

    changePassword.mutate({ currentPassword, newPassword, confirmNewPassword });
  }

  const errorMessage =
    clientError ??
    (changePassword.isError
      ? getChangePasswordErrorMessage(changePassword.error)
      : null);

  const isSubmitting = changePassword.isPending;
  const succeeded = changePassword.isSuccess;

  function renderField(
    id: string,
    label: string,
    value: string,
    onChange: (v: string) => void,
    autoComplete: string,
  ) {
    return (
      <div className="flex flex-col gap-2.5">
        <Label
          htmlFor={id}
          className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground"
        >
          {label}
        </Label>
        <div className="relative">
          <Lock
            aria-hidden
            className="pointer-events-none absolute left-5 top-1/2 size-4.5 -translate-y-1/2 text-muted-foreground"
          />
          <Input
            id={id}
            name={id}
            type={visible[id] ? "text" : "password"}
            autoComplete={autoComplete}
            placeholder="••••••••"
            value={value}
            onChange={(e) => onChange(e.target.value)}
            disabled={isSubmitting}
            className={inputClass}
          />
          <button
            type="button"
            onClick={() => setVisible((v) => ({ ...v, [id]: !v[id] }))}
            disabled={isSubmitting}
            aria-label={visible[id] ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
            className="absolute right-5 top-1/2 -translate-y-1/2 text-muted-foreground transition-colors hover:text-accent disabled:opacity-50"
          >
            {visible[id] ? <EyeOff className="size-4.5" /> : <Eye className="size-4.5" />}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="w-full max-w-lg">
      {/* Shown to users who arrived here because an admin issued them a temporary password */}
      {mustChangePassword && (
        <div className="mb-7 flex items-start gap-3 rounded-2xl border border-accent/30 bg-accent/8 px-5 py-4">
          <AlertCircle aria-hidden className="mt-0.5 size-5 shrink-0 text-accent" />
          <div className="text-sm leading-relaxed">
            <p className="font-heading font-600 text-foreground">
              Bạn cần đổi mật khẩu trước khi tiếp tục
            </p>
            <p className="mt-1 text-muted-foreground">
              Tài khoản đang dùng mật khẩu tạm do quản trị viên cấp. Hãy đặt mật khẩu
              riêng để tiếp tục sử dụng hệ thống.
            </p>
          </div>
        </div>
      )}

      <h1 className="font-heading text-[32px] font-bold leading-tight tracking-[-0.02em] text-foreground">
        Đổi mật khẩu
      </h1>
      <p className="mt-2 text-[15px] text-muted-foreground">
        Nhập mật khẩu hiện tại và mật khẩu mới bạn muốn dùng.
      </p>

      <form onSubmit={handleSubmit} noValidate className="mt-8 flex flex-col gap-5">
        {renderField("currentPassword", "Mật khẩu hiện tại", currentPassword, setCurrentPassword, "current-password")}
        {renderField("newPassword", "Mật khẩu mới", newPassword, setNewPassword, "new-password")}
        {renderField("confirmNewPassword", "Xác nhận mật khẩu mới", confirmNewPassword, setConfirmNewPassword, "new-password")}

        {/* Requirements update as the user types instead of failing after submission */}
        <ul className="flex flex-col gap-2 rounded-2xl bg-secondary/60 px-5 py-4">
          {policyChecks.map(({ label, passed }) => (
            <li
              key={label}
              className={cn(
                "flex items-center gap-2.5 text-sm transition-colors",
                passed ? "text-accent" : "text-muted-foreground",
              )}
            >
              {passed ? (
                <Check aria-hidden className="size-4 shrink-0" />
              ) : (
                <X aria-hidden className="size-4 shrink-0 opacity-40" />
              )}
              {label}
            </li>
          ))}
          <li
            className={cn(
              "flex items-center gap-2.5 text-sm transition-colors",
              confirmMatches ? "text-accent" : "text-muted-foreground",
            )}
          >
            {confirmMatches ? (
              <Check aria-hidden className="size-4 shrink-0" />
            ) : (
              <X aria-hidden className="size-4 shrink-0 opacity-40" />
            )}
            Xác nhận trùng khớp với mật khẩu mới
          </li>
        </ul>

        {errorMessage && (
          <div
            className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive"
            role="alert"
            aria-live="polite"
          >
            <AlertCircle aria-hidden className="mt-0.5 size-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        {succeeded && (
          <div
            className="flex items-start gap-2.5 rounded-2xl border border-accent/30 bg-accent/8 px-4 py-3 text-sm text-accent"
            role="status"
            aria-live="polite"
          >
            <CheckCircle2 aria-hidden className="mt-0.5 size-4 shrink-0" />
            <span>Đổi mật khẩu thành công. Lần đăng nhập sau hãy dùng mật khẩu mới.</span>
          </div>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="mt-1 flex h-14 w-full items-center justify-center gap-2 rounded-full bg-accent font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90 hover:shadow-accent/35 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSubmitting ? (
            <>
              <Loader2 className="size-4 animate-spin" />
              Đang lưu
            </>
          ) : (
            "Đổi mật khẩu"
          )}
        </button>
      </form>
    </div>
  );
}
