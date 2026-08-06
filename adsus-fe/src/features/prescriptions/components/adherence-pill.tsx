"use client";

import { cn } from "@/lib/utils";

/**
 * AdherencePill — hiển thị tỉ lệ tuân thủ thuốc (Module 7, SCR-18).
 *
 * Quy tắc nghiệp vụ (CLAUDE.md §11.3.4):
 *   - adherence ≥ 80%  → variant "good"   (status-good, success tint)
 *   - adherence < 80%  → variant "warn"   (status-warning, amber tint)
 *   - KHÔNG bao giờ variant "destructive" — màu đỏ chỉ dành cho safety card /
 *     validation error / "không được". Adherence thấp là "cần hỗ trợ", không
 *     phải "lỗi" cần trừng phạt.
 *
 * Thuộc tính `data-adid` để test dễ grep — không phải prop ẩn của DOM.
 */

export type AdherenceVariant = "good" | "warn" | "unknown";

export interface AdherencePillProps {
  /** Giá trị 0..100. Null/undefined/NaN → variant "unknown". */
  percent: number | null | undefined;
  /** Label tuỳ biến, vd: "tuần này", "tháng này". */
  label?: string;
  /** Bypass cho test/storybook. */
  variant?: AdherenceVariant;
  /** ClassName tuỳ biến. */
  className?: string;
}

function deriveVariant(percent: number | null | undefined): AdherenceVariant {
  if (percent == null || Number.isNaN(percent)) return "unknown";
  return percent >= 80 ? "good" : "warn";
}

function formatPercent(percent: number): string {
  // Math.round tránh phần thập phân dài (vd: 79.6 → 80%, 79.4 → 79%).
  return `${Math.round(percent)}%`;
}

const variantClasses: Record<AdherenceVariant, string> = {
  // Medizco palette (§8.1 master thực tế):
  //   --status-good      = #1cba9f (teal)
  //   --status-warning   = #e0912f (amber)
  //   --muted            = #5b6b85 (slate)
  good: "bg-[#e4f5f3] text-[#128c82] border-[#1cba9f]/30",
  warn: "bg-[#fdf3e3] text-[#a86515] border-[#e0912f]/30",
  unknown: "bg-[#f7f9fb] text-[#5b6b85] border-[#dde5ef]",
};

export function AdherencePill({
  percent,
  label,
  variant: variantOverride,
  className,
}: AdherencePillProps) {
  const variant = variantOverride ?? deriveVariant(percent);
  const display =
    percent == null || Number.isNaN(percent)
      ? "—"
      : formatPercent(percent as number);

  return (
    <span
      data-adid={variant}
      className={cn(
        "inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5",
        "text-xs font-medium",
        variantClasses[variant],
        className,
      )}
    >
      {display}
      {label ? (
        <span className="ml-1 text-[11px] font-normal opacity-80">{label}</span>
      ) : null}
    </span>
  );
}