import Link from "next/link";

const CLINIC_INFO = {
  name: "Phòng khám Siêu âm ADSUS",
  address: "123 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
  phone: "0901 234 567",
  hours: "Thứ 2 – Thứ 7: 7:00 – 17:00",
};

export function LandingFooter() {
  return (
    <footer
      className="px-4 py-12 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-navy)", color: "rgba(255,255,255,0.75)" }}
    >
      <div className="mx-auto max-w-6xl">
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {/* Brand */}
          <div>
            <div
              className="mb-4 flex items-center gap-2.5 text-xl font-semibold"
              style={{ fontFamily: "var(--lp-font-serif)", color: "#fff" }}
            >
              <span
                className="flex size-9 items-center justify-center rounded-lg"
                style={{ backgroundColor: "var(--lp-teal)" }}
              >
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  className="size-5 text-white"
                >
                  <circle cx="12" cy="12" r="10" />
                  <path d="M8 12h8M12 8v8" />
                </svg>
              </span>
              ADSUS
            </div>
            <p className="text-sm leading-relaxed" style={{ color: "rgba(255,255,255,0.6)" }}>
              Hệ thống phát hiện và phân đoạn bất thường trên ảnh siêu âm có hỗ trợ AI.
            </p>
          </div>

          {/* Links */}
          <div>
            <h3
              className="mb-4 text-xs font-semibold uppercase tracking-widest"
              style={{ color: "var(--lp-teal)" }}
            >
              Liên kết
            </h3>
            <ul className="flex flex-col gap-2">
              {[
                { href: "/", label: "Trang chủ" },
                { href: "/#dich-vu", label: "Dịch vụ" },
                { href: "/blog", label: "Bài viết" },
                { href: "/login", label: "Đăng nhập" },
              ].map((l) => (
                <li key={l.href}>
                  <Link
                    href={l.href}
                    className="text-sm transition-opacity hover:opacity-80"
                  >
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Contact */}
          <div>
            <h3
              className="mb-4 text-xs font-semibold uppercase tracking-widest"
              style={{ color: "var(--lp-teal)" }}
            >
              Liên hệ
            </h3>
            <ul className="flex flex-col gap-3">
              <li className="flex items-start gap-2">
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  className="mt-0.5 size-4 shrink-0"
                >
                  <path d="M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0Z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
                <span className="text-sm">{CLINIC_INFO.address}</span>
              </li>
              <li className="flex items-center gap-2">
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  className="size-4 shrink-0"
                >
                  <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.07 13a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.07 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 21 16.92z" />
                </svg>
                <span
                  className="text-sm"
                  style={{ fontFamily: "var(--lp-font-mono)" }}
                >
                  {CLINIC_INFO.phone}
                </span>
              </li>
              <li className="flex items-center gap-2">
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  className="size-4 shrink-0"
                >
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
                <span
                  className="text-sm"
                  style={{ fontFamily: "var(--lp-font-mono)" }}
                >
                  {CLINIC_INFO.hours}
                </span>
              </li>
            </ul>
          </div>

          {/* Social */}
          <div>
            <h3
              className="mb-4 text-xs font-semibold uppercase tracking-widest"
              style={{ color: "var(--lp-teal)" }}
            >
              Theo dõi
            </h3>
            <div className="flex gap-3">
              {/* Facebook */}
              <a
                href="#"
                aria-label="Facebook"
                className="flex size-9 items-center justify-center rounded-lg transition-opacity hover:opacity-80"
                style={{ backgroundColor: "rgba(255,255,255,0.1)" }}
              >
                <svg viewBox="0 0 24 24" fill="white" className="size-4">
                  <path d="M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z" />
                </svg>
              </a>
              {/* Zalo */}
              <a
                href="#"
                aria-label="Zalo"
                className="flex size-9 items-center justify-center rounded-lg transition-opacity hover:opacity-80"
                style={{ backgroundColor: "rgba(255,255,255,0.1)" }}
              >
                <span className="text-xs font-bold text-white">Zalo</span>
              </a>
            </div>
          </div>
        </div>

        <div
          className="mt-10 border-t pt-6 text-center text-xs"
          style={{ borderColor: "rgba(255,255,255,0.1)", color: "rgba(255,255,255,0.45)" }}
        >
          © {new Date().getFullYear()} {CLINIC_INFO.name}. Mọi quyền được bảo lưu.
        </div>
      </div>
    </footer>
  );
}
