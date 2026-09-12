"use client";

import Link from "next/link";
import { useState } from "react";
import { Menu, X } from "lucide-react";

const NAV_LINKS = [
  { href: "/", label: "Trang chủ" },
  { href: "/#dich-vu", label: "Dịch vụ" },
  { href: "/blog", label: "Bài viết" },
];

export function LandingNavbar() {
  const [open, setOpen] = useState(false);

  return (
    <nav
      className="sticky top-0 z-50 border-b"
      style={{
        backgroundColor: "var(--lp-canvas)",
        borderColor: "var(--lp-border)",
      }}
    >
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
        {/* Logo */}
        <Link
          href="/"
          className="flex items-center gap-2.5 font-semibold text-xl leading-none tracking-tight"
          style={{ fontFamily: "var(--lp-font-serif)", color: "var(--lp-navy)" }}
        >
          <span
            className="flex size-9 items-center justify-center rounded-lg text-white"
            style={{ backgroundColor: "var(--lp-teal)" }}
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              className="size-5"
            >
              <circle cx="12" cy="12" r="10" />
              <path d="M8 12h8M12 8v8" />
            </svg>
          </span>
          ADSUS
        </Link>

        {/* Desktop nav */}
        <ul className="hidden items-center gap-8 md:flex">
          {NAV_LINKS.map((l) => (
            <li key={l.href}>
              <Link
                href={l.href}
                className="text-sm font-medium transition-colors hover:opacity-70"
                style={{ color: "var(--lp-muted)" }}
              >
                {l.label}
              </Link>
            </li>
          ))}
        </ul>

        {/* Desktop CTA */}
        <Link
          href="/login"
          className="hidden items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-semibold transition-colors md:flex"
          style={{
            backgroundColor: "var(--lp-teal)",
            color: "#fff",
          }}
          onMouseEnter={(e) =>
            (e.currentTarget.style.backgroundColor = "var(--lp-teal-h)")
          }
          onMouseLeave={(e) =>
            (e.currentTarget.style.backgroundColor = "var(--lp-teal)")
          }
        >
          Đăng nhập
        </Link>

        {/* Mobile hamburger */}
        <button
          className="rounded-lg p-2 md:hidden"
          style={{ color: "var(--lp-navy)" }}
          onClick={() => setOpen((o) => !o)}
          aria-label="Toggle menu"
        >
          {open ? <X className="size-6" /> : <Menu className="size-6" />}
        </button>
      </div>

      {/* Mobile menu */}
      {open && (
        <div
          className="border-t px-4 py-4 md:hidden"
          style={{
            backgroundColor: "var(--lp-canvas)",
            borderColor: "var(--lp-border)",
          }}
        >
          <ul className="flex flex-col gap-4">
            {NAV_LINKS.map((l) => (
              <li key={l.href}>
                <Link
                  href={l.href}
                  className="block text-sm font-medium"
                  style={{ color: "var(--lp-muted)" }}
                  onClick={() => setOpen(false)}
                >
                  {l.label}
                </Link>
              </li>
            ))}
            <li>
              <Link
                href="/login"
                className="block rounded-lg px-4 py-2 text-center text-sm font-semibold"
                style={{
                  backgroundColor: "var(--lp-teal)",
                  color: "#fff",
                }}
                onClick={() => setOpen(false)}
              >
                Đăng nhập
              </Link>
            </li>
          </ul>
        </div>
      )}
    </nav>
  );
}
