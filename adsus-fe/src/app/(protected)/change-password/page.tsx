import type { Metadata } from "next";

import { ChangePasswordForm } from "@/features/auth/components/change-password-form";

export const metadata: Metadata = {
  title: "Đổi mật khẩu | ADSUS",
};

// SCR-04 — web change-password screen (UC-25). Available to every role.
export default function ChangePasswordPage() {
  return (
    <div className="flex justify-center px-6 py-12">
      <ChangePasswordForm />
    </div>
  );
}
