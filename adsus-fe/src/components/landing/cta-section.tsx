import { CTAButton } from "./cta-button";

export function CTASection() {
  return (
    <section
      className="px-4 py-20 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-navy)" }}
    >
      <div className="mx-auto max-w-4xl text-center">
        {/* Decorative line */}
        <span
          className="mx-auto mb-6 block h-1 w-16 rounded-full"
          style={{ backgroundColor: "var(--lp-teal)" }}
        />

        <h2
          className="mb-4 text-3xl font-semibold sm:text-4xl"
          style={{ fontFamily: "var(--lp-font-serif)", color: "#fff" }}
        >
          Sẵn sàng đặt lịch khám?
        </h2>
        <p
          className="mx-auto mb-8 max-w-xl text-base leading-relaxed"
          style={{ color: "rgba(255,255,255,0.7)" }}
        >
          Đặt lịch siêu âm ngay hôm nay để được bác sĩ tư vấn và chăm sóc sức khỏe
          tốt nhất cho mẹ và bé.
        </p>

        <CTAButton href="/auth/register" size="lg" showIcon>
          Đặt lịch ngay
        </CTAButton>
      </div>
    </section>
  );
}
