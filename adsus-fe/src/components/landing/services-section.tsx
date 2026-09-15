const SERVICES = [
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <circle cx="12" cy="12" r="9" />
        <circle cx="12" cy="12" r="4" />
        <circle cx="12" cy="12" r="1" fill="currentColor" />
      </svg>
    ),
    title: "Siêu âm thai (2D, 3D, 4D)",
    description:
      "Khám thai, theo dõi sự phát triển của thai nhi qua từng giai đoạn với hình ảnh 2D, 3D và 4D.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <ellipse cx="12" cy="12" rx="9" ry="6" />
        <path d="M3 12h18" />
      </svg>
    ),
    title: "Siêu âm tử cung phần phụ",
    description:
      "Đánh giá tử cung, buồng trứng và các khối u phần phụ, phát hiện sớm bệnh lý phụ khoa.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-3.5-3.5" />
      </svg>
    ),
    title: "Soi cổ tử cung",
    description:
      "Quan sát trực tiếp cổ tử cung qua máy soi, phát hiện sớm các tổn thương và bất thường.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M12 21s-7-4.5-7-11a7 7 0 0 1 14 0c0 6.5-7 11-7 11Z" />
        <circle cx="12" cy="10" r="2.5" />
      </svg>
    ),
    title: "Khám phụ khoa",
    description:
      "Khám, tư vấn và điều trị các bệnh lý phụ khoa thường gặp ở nữ giới.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M12 2v20" />
        <path d="M5 8c0 3 3 5 7 5s7-2 7-5" />
        <path d="M5 16c0 3 3 5 7 5s7-2 7-5" />
      </svg>
    ),
    title: "Khám thai",
    description:
      "Theo dõi thai kỳ định kỳ, tư vấn dinh dưỡng và sức khỏe cho mẹ bầu trong suốt thai kỳ.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M13 2 4 14h7l-1 8 9-12h-7z" />
      </svg>
    ),
    title: "Điều trị tổn thương cổ tử cung bằng đốt điện, nhiệt",
    description:
      "Đốt điện, đốt nhiệt các tổn thương cổ tử cung — phương pháp hiệu quả, an toàn.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <path d="M12 2v6" />
        <path d="m6 8 6-6 6 6" />
        <path d="M5 14h14l-2 8H7z" />
      </svg>
    ),
    title: "Đốt, cắt sùi mào gà âm hộ, âm đạo, tầng sinh môn",
    description:
      "Đốt và cắt sùi mào gà vùng âm hộ, âm đạo, tầng sinh môn — đảm bảo thẩm mỹ, hạn chế tái phát.",
  },
  {
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="size-7">
        <rect x="4" y="3" width="16" height="18" rx="2" />
        <path d="M9 8h6" />
        <path d="M9 12h6" />
        <path d="M9 16h4" />
      </svg>
    ),
    title: "Làm thuốc âm đạo",
    description:
      "Đặt thuốc âm đạo điều trị viêm nhiễm phụ khoa theo chỉ định của bác sĩ.",
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
            Dịch vụ siêu âm & phụ khoa
          </h2>
          <p
            className="mx-auto mt-3 max-w-xl text-sm"
            style={{ color: "var(--lp-muted)" }}
          >
            Phòng khám cung cấp đầy đủ các dịch vụ siêu âm, khám và điều trị
            phụ khoa với máy móc hiện đại và đội ngũ bác sĩ chuyên môn.
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
