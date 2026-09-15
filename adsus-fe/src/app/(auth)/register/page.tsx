import { Activity, ScanLine, ShieldCheck } from "lucide-react";
import type { Metadata } from "next";
import { Suspense } from "react";

import { RegisterForm } from "@/features/auth/components/register-form";
import { ServerStatusBadge } from "@/features/auth/components/server-status-badge";
import { LOGIN_PALETTE_OVERRIDE } from "@/features/auth/constants/theme";

export const metadata: Metadata = {
  title: "Đăng ký tài khoản bệnh nhân | ADSUS",
  description: "Đăng ký tài khoản bệnh nhân trực tuyến trên hệ thống chẩn đoán ADSUS.",
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

export default function RegisterPage() {
  return (
    <main
      style={LOGIN_PALETTE_OVERRIDE}
      className="grid min-h-screen lg:grid-cols-[1.05fr_1fr]"
    >
      {/* Ping /api/health to awaken backend early */}
      <ServerStatusBadge />

      {/* Marketing column — hidden below 1024px so the form gets full width */}
      <section className="relative hidden overflow-hidden bg-gradient-to-br from-primary via-primary to-[#152744] px-14 py-16 text-primary-foreground lg:flex lg:flex-col lg:justify-center">
        {/* Faint oscilloscope-style grid */}
        <div aria-hidden className="login-scan-grid pointer-events-none absolute inset-0" />

        {/* Blurred blobs in the two accent colours */}
        <div
          aria-hidden
          className="pointer-events-none absolute -left-32 -top-32 size-[28rem] rounded-full bg-[var(--success)]/15 blur-3xl"
        />
        <div
          aria-hidden
          className="pointer-events-none absolute -bottom-40 -right-24 size-[26rem] rounded-full bg-chart-3/20 blur-3xl"
        />

        {/* Probe scan beam */}
        <div aria-hidden className="login-scan-beam pointer-events-none absolute inset-x-0 h-28" />

        <div className="relative max-w-2xl motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-left-4 motion-safe:duration-700">
          <div className="flex items-center gap-3.5">
            <span className="flex size-12 items-center justify-center rounded-full bg-[var(--success)]">
              <ScanLine className="size-6 text-white" />
            </span>
            <span className="text-2xl font-bold tracking-[-0.02em]">ADSUS</span>
          </div>

          <h2 className="mt-14 text-[40px] font-bold leading-[1.15] tracking-[-0.02em]">
            Hỗ trợ chẩn đoán bất thường
            <br />
            trên ảnh siêu âm
          </h2>

          <span
            aria-hidden
            className="mt-6 block h-1 w-16 rounded-full bg-[var(--success)]"
          />

          <p className="mt-6 text-[15px] leading-relaxed text-primary-foreground/65">
            Kết hợp trí tuệ nhân tạo với thông tin lâm sàng để rút ngắn thời gian đọc ảnh
            <br />
            cho bác sĩ và hỗ trợ bệnh nhân theo dõi sức khỏe.
          </p>

          <ul className="mt-14 flex flex-col gap-7">
            {highlights.map(({ icon: Icon, title, description }) => (
              <li key={title} className="flex items-start gap-4">
                <span className="mt-0.5 flex size-11 shrink-0 items-center justify-center rounded-full bg-white/10 ring-1 ring-white/15">
                  <Icon className="size-5" />
                </span>
                <div>
                  <p className="text-[15px] font-700">{title}</p>
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
          {/* Logo only on small screens */}
          <div className="mb-10 flex items-center gap-3 lg:hidden">
            <span className="flex size-11 items-center justify-center rounded-full bg-primary">
              <ScanLine className="size-5 text-primary-foreground" />
            </span>
            <span className="text-xl font-bold tracking-[-0.02em] text-primary">
              ADSUS
            </span>
          </div>

          <Suspense fallback={<div className="min-h-96" />}>
            <RegisterForm />
          </Suspense>
        </div>
      </section>
    </main>
  );
}
