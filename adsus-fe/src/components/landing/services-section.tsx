const SERVICES = [
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M12 2a10 10 0 1 0 10 10" />
        <path d="M12 6v6l4 2" />
        <path d="M17 2v4h4" />
      </svg>
    ),
    title: "Siêu âm thai",
    description: "Theo dõi sự phát triển của thai nhi qua từng giai đoạn, phát hiện sớm dị tật bẩm sinh.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
      </svg>
    ),
    title: "Siêu âm tim mạch",
    description: "Đánh giá chức năng và cấu trúc tim, phát hiện bệnh lý tim bẩm sinh và mắc phải.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <circle cx="12" cy="12" r="10" />
        <path d="M8 12h8M12 8v8" />
      </svg>
    ),
    title: "Siêu âm bụng tổng quát",
    description: "Khảo sát gan, thận, túi mật, tụy và các cơ quan bụng khác.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
        <path d="M12 4v1M17.66 6.34l-.71.71M20 12h-1M17.66 17.66l-.71-.71M12 19v1M6.34 17.66l.71-.71M4 12h1M6.34 6.34l.71.71" />
      </svg>
    ),
    title: "Siêu âm 4D",
    description: "Hình ảnh thai nhi 4 chiều trực quan, giúp phát hiện dị tật chính xác hơn.",
  },
];

export function ServicesSection() {
  return (
    <section
      id="dich-vu"
      className="px-4 py-16 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-canvas)" }}
    >
      <div className="mx-auto max-w-6xl">
        {/* Header */}
        <div className="mb-10 text-center">
          <span
            className="mb-2 inline-block text-xs font-semibold uppercase tracking-widest"
            style={{ color: "var(--lp-teal)" }}
          >
            Dịch vụ
          </span>
          <h2
            className="text-2xl sm:text-3xl"
            style={{
              fontFamily: "var(--lp-font-serif)",
              fontWeight: 600,
              color: "var(--lp-navy)",
            }}
          >
            Dịch vụ siêu âm chuyên sâu
          </h2>
          <p
            className="mx-auto mt-3 max-w-xl text-sm"
            style={{ color: "var(--lp-muted)" }}
          >
            Phòng khám cung cấp đầy đủ các loại hình siêu âm chẩn đoán hình ảnh phổ biến
            với máy móc hiện đại và đội ngũ bác sĩ giàu kinh nghiệm.
          </p>
        </div>

        {/* Cards grid */}
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {SERVICES.map((svc) => (
            <div
              key={svc.title}
              className="group rounded-xl border p-6 transition-shadow hover:shadow-md"
              style={{
                backgroundColor: "var(--lp-surface)",
                borderColor: "var(--lp-border)",
              }}
            >
              <div
                className="mb-4 flex size-12 items-center justify-center rounded-xl"
                style={{ backgroundColor: "var(--lp-teal-tint)", color: "var(--lp-teal)" }}
              >
                {svc.icon}
              </div>
              <h3
                className="mb-2 font-semibold"
                style={{ color: "var(--lp-navy)", fontFamily: "var(--lp-font-serif)" }}
              >
                {svc.title}
              </h3>
              <p className="text-sm leading-relaxed" style={{ color: "var(--lp-muted)" }}>
                {svc.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
