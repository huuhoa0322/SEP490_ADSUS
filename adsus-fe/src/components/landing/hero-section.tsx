import Link from "next/link";
import { CTAButton } from "./cta-button";

export function HeroSection() {
  return (
    <section
      className="relative overflow-hidden px-4 py-24 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-canvas)" }}
    >
      {/* Decorative blob */}
      <div
        aria-hidden
        className="pointer-events-none absolute -right-24 -top-16 size-96 rounded-full opacity-20 blur-3xl"
        style={{ backgroundColor: "var(--lp-teal)" }}
      />
      <div
        aria-hidden
        className="pointer-events-none absolute -bottom-24 -left-16 size-80 rounded-full opacity-10 blur-3xl"
        style={{ backgroundColor: "var(--lp-navy)" }}
      />

      <div className="relative mx-auto max-w-6xl">
        <div className="max-w-3xl">
          {/* Eyebrow */}
          <span
            className="mb-4 inline-block rounded-full px-4 py-1.5 text-xs font-semibold uppercase tracking-widest"
            style={{
              backgroundColor: "var(--lp-teal-tint)",
              color: "var(--lp-teal)",
            }}
          >
            Phòng khám Siêu âm ADSUS
          </span>

          {/* Title */}
          <h1
            className="mb-6 text-4xl leading-tight sm:text-5xl lg:text-6xl"
            style={{
              fontFamily: "var(--lp-font-serif)",
              fontWeight: 700,
              color: "var(--lp-navy)",
            }}
          >
            Siêu Âm Thai Nhi &amp;
            <br />
            <span style={{ color: "var(--lp-teal)" }}>
              Chăm Sóc Sức Khỏe Phụ Khoa Chuyên Sâu
            </span>
          </h1>

          {/* Tagline */}
          <p
            className="mb-10 max-w-xl text-base leading-relaxed sm:text-lg"
            style={{ color: "var(--lp-muted)" }}
          >
            Ứng dụng công nghệ siêu âm 2D, 3D, 4D tích hợp AI cùng bác sĩ Sản
            Phụ khoa giàu kinh nghiệm. Chuẩn đoán sớm và điều trị an toàn các
            bệnh lý phụ khoa.
          </p>

          {/* CTA */}
          <div className="flex flex-wrap items-center gap-4">
            <CTAButton href="/dat-lich">Đặt lịch ngay</CTAButton>
            <Link
              href="/#dich-vu"
              className="rounded-xl border px-7 py-3.5 text-sm font-semibold transition-colors"
              style={{
                borderColor: "var(--lp-border)",
                color: "var(--lp-muted)",
              }}
            >
              Khám phá dịch vụ
            </Link>
          </div>

          {/* Trust signals */}
          <div className="mt-12 flex flex-wrap gap-8">
            {[
              { number: "15+", label: "Năm kinh nghiệm" },
              { number: "10.000+", label: "Ca khám thành công" },
              { number: "4.9★", label: "Đánh giá từ bệnh nhân" },
            ].map((item) => (
              <div key={item.label}>
                <div
                  className="text-2xl font-bold"
                  style={{
                    fontFamily: "var(--lp-font-serif)",
                    color: "var(--lp-navy)",
                  }}
                >
                  {item.number}
                </div>
                <div className="text-xs" style={{ color: "var(--lp-muted)" }}>
                  {item.label}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
