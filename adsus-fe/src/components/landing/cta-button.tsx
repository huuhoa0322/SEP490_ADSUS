"use client";

import Link from "next/link";

interface CTAButtonProps {
  href: string;
  children: React.ReactNode;
  size?: "md" | "lg";
  showIcon?: boolean;
}

export function CTAButton({ href, children, size = "md", showIcon = false }: CTAButtonProps) {
  const sizeClass = size === "lg" ? "px-8 py-4 text-base" : "px-7 py-3.5 text-sm";

  return (
    <Link
      href={href}
      className={`inline-flex items-center gap-2 rounded-xl font-semibold text-white shadow-sm transition-colors ${sizeClass}`}
      style={{
        backgroundColor: "var(--lp-teal)",
        boxShadow: "0 4px 14px rgba(18,140,130,0.25)",
      }}
      onMouseEnter={(e) =>
        (e.currentTarget.style.backgroundColor = "var(--lp-teal-h)")
      }
      onMouseLeave={(e) =>
        (e.currentTarget.style.backgroundColor = "var(--lp-teal)")
      }
    >
      {showIcon && (
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          className="size-5"
        >
          <rect width="18" height="18" x="3" y="4" rx="2" ry="2" />
          <line x1="16" x2="16" y1="2" y2="6" />
          <line x1="8" x2="8" y1="2" y2="6" />
          <line x1="3" x2="21" y1="10" y2="10" />
        </svg>
      )}
      {children}
    </Link>
  );
}
