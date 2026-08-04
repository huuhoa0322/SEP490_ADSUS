import { ScanLine } from "lucide-react";
import type { Metadata } from "next";

import { ForgotPasswordForm } from "@/features/auth/components/forgot-password-form";

export const metadata: Metadata = {
  title: "Quên mật khẩu | ADSUS",
  description: "Yêu cầu cấp lại mật khẩu cho tài khoản ADSUS.",
};

// UC-03 FT-06 — mở từ SCR-01. Màn này chưa được PRD đặt SCR-ID (UCS đã ghi nhận là khoảng
// trống, để lại cho FDS), nên không tự bịa ra một mã mới.
export default function ForgotPasswordPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-6 py-14">
      <div className="w-full max-w-md">
        <div className="mb-10 flex items-center gap-3">
          <span className="flex size-11 items-center justify-center rounded-full bg-primary">
            <ScanLine className="size-5 text-primary-foreground" />
          </span>
          <span className="font-heading text-xl font-bold tracking-[-0.02em] text-primary">
            ADSUS
          </span>
        </div>

        <ForgotPasswordForm />
      </div>
    </main>
  );
}
