import { Activity, ScanLine, ShieldCheck } from "lucide-react";
import type { Metadata } from "next";
import { Suspense } from "react";

import { SignInForm } from "@/features/auth/components/sign-in-form";

export const metadata: Metadata = {
  title: "Đăng nhập | ADSUS",
  description: "Đăng nhập hệ thống ADSUS bằng số điện thoại và mật khẩu.",
};

const highlights = [
  {
    icon: ScanLine,
    title: "Phân tích ảnh siêu âm",
    description: "AI hỗ trợ phát hiện và khoanh vùng cấu trúc bất thường.",
  },
  {
    icon: Activity,
    title: "Theo dõi liên tục",
    description: "Quản lý hồ sơ, lịch hẹn và tuân thủ điều trị của bệnh nhân.",
  },
  {
    icon: ShieldCheck,
    title: "Bác sĩ quyết định cuối cùng",
    description: "Kết quả AI chỉ mang tính tham khảo, không thay thế chẩn đoán.",
  },
];

// SCR-01 — web sign-in screen, used by Admin and Doctor.
// Patients sign in through the mobile app (SCR-02) and never see this page.
export default function LoginPage() {
  return (
    <main className="grid min-h-screen lg:grid-cols-[1.05fr_1fr]">
      {/* Marketing column — hidden below 1024px so the form gets the full width */}
      <section className="relative hidden overflow-hidden bg-primary px-14 py-16 text-primary-foreground lg:flex lg:flex-col lg:justify-center">
        {/* Blurred blobs in the two accent colours, for depth */}
        <div
          aria-hidden
          className="pointer-events-none absolute -left-32 -top-32 size-[28rem] rounded-full bg-accent/20 blur-3xl"
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -bottom-40 -right-24 size-[26rem] rounded-full bg-chart-3/25 blur-3xl"
        />

        <div className="relative max-w-lg">
          <div className="flex items-center gap-3.5">
            <span className="flex size-12 items-center justify-center rounded-full bg-accent">
              <ScanLine className="size-6 text-accent-foreground" />
            </span>
            <span className="font-heading text-2xl font-bold tracking-[-0.02em]">
              ADSUS
            </span>
          </div>

          <h2 className="mt-14 font-heading text-[40px] font-bold leading-[1.15] tracking-[-0.02em]">
            Hỗ trợ chẩn đoán bất thường trên ảnh siêu âm
          </h2>

          {/* Short accent rule under the heading — a recurring motif in the template */}
          <span
            aria-hidden
            className="mt-6 block h-1 w-16 rounded-full bg-accent"
          />

          <p className="mt-6 text-[15px] leading-relaxed text-primary-foreground/65">
            Kết hợp trí tuệ nhân tạo với thông tin lâm sàng để rút ngắn thời gian
            đọc ảnh cho bác sĩ.
          </p>

          <ul className="mt-14 flex flex-col gap-7">
            {highlights.map(({ icon: Icon, title, description }) => (
              <li key={title} className="flex items-start gap-4">
                <span className="mt-0.5 flex size-11 shrink-0 items-center justify-center rounded-full bg-white/10 ring-1 ring-white/15">
                  <Icon className="size-5" />
                </span>
                <div>
                  <p className="font-heading text-[15px] font-600">{title}</p>
                  <p className="mt-1 text-sm leading-relaxed text-primary-foreground/55">
                    {description}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* Form column */}
      <section className="flex items-center justify-center bg-background px-6 py-14 sm:px-10">
        <div className="w-full max-w-md">
          {/* Logo only on small screens, where the marketing column is hidden */}
          <div className="mb-10 flex items-center gap-3 lg:hidden">
            <span className="flex size-11 items-center justify-center rounded-full bg-primary">
              <ScanLine className="size-5 text-primary-foreground" />
            </span>
            <span className="font-heading text-xl font-bold tracking-[-0.02em] text-primary">
              ADSUS
            </span>
          </div>

          {/* SignInForm đọc query string (?expired=1) nên phải bọc Suspense — Next.js yêu
              cầu vậy với trang dựng tĩnh. */}
          <Suspense fallback={<div className="min-h-96" />}>
            <SignInForm />
          </Suspense>
        </div>
      </section>
    </main>
  );
}
