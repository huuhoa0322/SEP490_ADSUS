"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState, useRef, useEffect } from "react";
import { Menu, X, User, LogOut, CalendarCheck } from "lucide-react";

import { useAuthStore, useHasHydrated } from "@/store/auth-store";

const NAV_LINKS = [
  { href: "/", label: "Trang chủ" },
  { href: "/#dich-vu", label: "Dịch vụ" },
  { href: "/blog", label: "Bài viết" },
  { href: "/dat-lich", label: "Đặt lịch" },
];

export function LandingNavbar() {
  const [open, setOpen] = useState(false);
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const hasHydrated = useHasHydrated();
  const accessToken = useAuthStore((s) => s.accessToken);
  const user = useAuthStore((s) => s.user);
  const signOut = useAuthStore((s) => s.signOut);

  const isPatient = hasHydrated && accessToken && user?.role === "PATIENT";
  const pathname = usePathname();
  // Guest bấm "Đăng nhập" từ /dat-lich → sau login phải quay lại /dat-lich.
  const loginHref =
    pathname === "/dat-lich" ? "/login?redirect=/dat-lich" : "/login";

  // Close dropdown when clicking outside
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    if (dropdownOpen) {
      document.addEventListener("mousedown", handleClick);
    }
    return () => document.removeEventListener("mousedown", handleClick);
  }, [dropdownOpen]);

  // Close mobile menu on route change
  useEffect(() => {
    setOpen(false);
  }, []);

  function handleSignOut() {
    void signOut();
    setDropdownOpen(false);
  }

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

        {/* Desktop auth area */}
        <div className="hidden items-center gap-3 md:flex">
          {isPatient ? (
            /* Patient logged in — show account dropdown */
            <div className="relative" ref={dropdownRef}>
              <button
                onClick={() => setDropdownOpen((o) => !o)}
                className="flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-colors"
                style={{
                  backgroundColor: "var(--lp-teal-tint)",
                  color: "var(--lp-teal)",
                }}
                aria-haspopup="true"
                aria-expanded={dropdownOpen}
              >
                <User className="size-4" />
                <span>Tài khoản</span>
              </button>

              {dropdownOpen && (
                <div
                  className="absolute right-0 top-full mt-2 w-48 rounded-xl border shadow-sm"
                  style={{
                    backgroundColor: "var(--lp-surface)",
                    borderColor: "var(--lp-border)",
                  }}
                >
                  <div
                    className="border-b px-4 py-2.5"
                    style={{ borderColor: "var(--lp-border)" }}
                  >
                    <p className="text-xs font-medium" style={{ color: "var(--lp-muted)" }}>
                      {user.fullName}
                    </p>
                    <p className="text-xs" style={{ color: "var(--lp-muted)" }}>
                      Bệnh nhân
                    </p>
                  </div>
                  <div className="py-1">
                    <Link
                      href="/dat-lich"
                      className="flex items-center gap-2.5 px-4 py-2.5 text-sm transition-colors hover:bg-[var(--lp-canvas)]"
                      style={{ color: "var(--lp-text)" }}
                      onClick={() => setDropdownOpen(false)}
                    >
                      <CalendarCheck className="size-4" style={{ color: "var(--lp-teal)" }} />
                      Lịch hẹn của tôi
                    </Link>
                    <button
                      onClick={handleSignOut}
                      className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-sm transition-colors hover:bg-[var(--lp-canvas)]"
                      style={{ color: "var(--lp-text)" }}
                    >
                      <LogOut className="size-4 text-destructive" />
                      Đăng xuất
                    </button>
                  </div>
                </div>
              )}
            </div>
          ) : (
            /* Guest — show sign-in button */
            <Link
              href={loginHref}
              className="flex items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-semibold transition-colors"
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
          )}
        </div>

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

            {isPatient ? (
              <>
                <li className="border-t pt-4" style={{ borderColor: "var(--lp-border)" }}>
                  <p className="mb-1 text-xs" style={{ color: "var(--lp-muted)" }}>
                    {user.fullName}
                  </p>
                  <Link
                    href="/dat-lich"
                    className="flex items-center gap-2 text-sm font-medium"
                    style={{ color: "var(--lp-teal)" }}
                    onClick={() => setOpen(false)}
                  >
                    <CalendarCheck className="size-4" />
                    Lịch hẹn của tôi
                  </Link>
                </li>
                <li>
                  <button
                    onClick={() => {
                      void signOut();
                      setOpen(false);
                    }}
                    className="flex items-center gap-2 text-sm font-medium text-destructive"
                  >
                    <LogOut className="size-4" />
                    Đăng xuất
                  </button>
                </li>
              </>
            ) : (
              <li>
                <Link
                  href={loginHref}
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
            )}
          </ul>
        </div>
      )}
    </nav>
  );
}
