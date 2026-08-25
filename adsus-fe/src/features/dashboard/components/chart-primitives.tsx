"use client";

import type { ReactNode } from "react";

/**
 * Các mảnh dựng biểu đồ cho SCR-08.
 *
 * Cố ý KHÔNG kéo thêm thư viện biểu đồ nào. Màn này chỉ cần so sánh độ lớn và tỉ lệ, dựng
 * bằng div và SVG là đủ — thêm một thư viện vài trăm KB cho mấy thanh ngang là không đáng,
 * mà lại thêm một thứ nữa cả nhóm phải học.
 */

/** Palette for dashboard redesign — extracted from reference image. */
export const CAT = {
  navy: "var(--cat-navy)",
  violet: "var(--cat-violet)",
  magenta: "var(--cat-magenta)",
  teal: "var(--cat-teal)",
  blue: "var(--cat-blue)",
  green: "var(--cat-green)",
  amber: "var(--cat-amber)",
  rose: "var(--cat-rose)",
  text: "var(--cat-text)",
  muted: "var(--cat-muted)",
} as const;

export type CatKey = keyof typeof CAT;

/** V2 stat tile — gradient icon-bg + value + optional trend badge. */
export function StatTile({
  label,
  value,
  hint,
  icon,
  cat,
  trend,
}: {
  label: string;
  value: string | number;
  hint?: string;
  icon?: ReactNode;
  cat?: CatKey;
  trend?: string;
}) {
  const bgVar = cat ? `var(--cat-${cat})` : "var(--cat-blue)";
  return (
    <div className="group relative overflow-hidden rounded-2xl border border-[var(--border)] bg-background p-6 transition-shadow hover:shadow-md">
      {/* Left accent bar */}
      <div
        className="absolute left-0 top-0 h-full w-1 rounded-l-2xl transition-all group-hover:w-1.5"
        style={{ backgroundColor: bgVar }}
      />
      <div className="flex items-start justify-between gap-3 pl-2">
        <div className="flex flex-col gap-3">
          <span className="font-heading text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {label}
          </span>
          <p className="font-heading text-[34px] font-bold leading-none tracking-[-0.02em] text-foreground tabular-nums">
            {value}
          </p>
          {hint && (
            <p className="text-sm text-muted-foreground">{hint}</p>
          )}
        </div>
        {icon && (
          <div
            className="flex size-10 shrink-0 items-center justify-center rounded-xl text-white"
            style={{ backgroundColor: bgVar }}
          >
            {icon}
          </div>
        )}
      </div>
      {trend && (
        <div className="mt-3 flex items-center gap-1 pl-2">
          <span
            className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium text-white"
            style={{ backgroundColor: bgVar + "22", color: bgVar }}
          >
            {trend}
          </span>
        </div>
      )}
    </div>
  );
}

export function ChartCard({
  title,
  description,
  children,
  action,
  className,
}: {
  title: string;
  description?: string;
  children: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <section className={`overflow-hidden rounded-2xl border border-[var(--border)] bg-background ${className ?? ""}`}>
      <div className="flex items-center justify-between border-b border-[var(--border)] px-6 py-4">
        <div>
          <h2 className="font-heading text-[15px] font-bold tracking-[-0.01em] text-foreground">
            {title}
          </h2>
          {description && (
            <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">{description}</p>
          )}
        </div>
        {action && <div>{action}</div>}
      </div>
      <div className="p-6">{children}</div>
    </section>
  );
}

/**
 * Danh sách thanh ngang so sánh độ lớn — redesign phiên bản.
 * Mỗi thanh một màu trong categorical palette.
 */
export function BarList({
  items,
  colors,
  emptyLabel = "Chưa có dữ liệu",
}: {
  items: { label: string; value: number }[];
  colors?: CatKey[];
  emptyLabel?: string;
}) {
  const max = Math.max(...items.map((i) => i.value), 0);

  if (max === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">{emptyLabel}</p>;
  }

  return (
    <ul className="flex flex-col gap-3">
      {items.map((item, idx) => {
        const color = colors ? `var(--cat-${colors[idx % colors.length]})` : CAT.teal;
        return (
          <li key={item.label} className="flex items-center gap-3">
            <span className="w-28 shrink-0 text-sm font-medium text-foreground">{item.label}</span>
            <span
              className="h-2 min-w-0.5 flex-1 overflow-hidden rounded-full bg-[var(--secondary)]"
              title={`${item.label}: ${item.value}`}
            >
              <span
                className="block h-full rounded-full"
                style={{ width: `${(item.value / max) * 100}%`, backgroundColor: color }}
              />
            </span>
            <span className="w-14 shrink-0 text-right text-sm font-semibold tabular-nums text-foreground">
              {item.value}
            </span>
          </li>
        );
      })}
    </ul>
  );
}

/**
 * Donut chart đơn giản — 2–4 phần, tổng luôn = 100%.
 * Dùng stroke-dasharray trên circle để vẽ.
 */
export function DonutChart({
  segments,
  size = 120,
  strokeWidth = 18,
}: {
  segments: { label: string; value: number; color: string }[];
  size?: number;
  strokeWidth?: number;
}) {
  const total = segments.reduce((s, seg) => s + seg.value, 0);
  const r = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * r;
  const cx = size / 2;
  const cy = size / 2;

  if (total === 0) return null;

  let accumulated = 0;
  const circles = segments.map((seg, idx) => {
    const fraction = seg.value / total;
    const dashLen = fraction * circumference;
    const gapLen = circumference - dashLen;
    const rotation = (accumulated / total) * 360 - 90;
    accumulated += seg.value;
    return { ...seg, dashLen, gapLen, rotation, fraction };
  });

  return (
    <div className="flex items-center gap-6">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label="donut chart">
        {circles.map((c, idx) => (
          <circle
            key={idx}
            cx={cx}
            cy={cy}
            r={r}
            fill="none"
            stroke={c.color}
            strokeWidth={strokeWidth}
            strokeDasharray={`${c.dashLen} ${c.gapLen}`}
            transform={`rotate(${c.rotation} ${cx} ${cy})`}
          />
        ))}
      </svg>
      <ul className="flex flex-col gap-2">
        {circles.map((c, idx) => (
          <li key={idx} className="flex items-center gap-2 text-sm">
            <span
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: c.color }}
            />
            <span className="font-medium text-foreground">{c.label}</span>
            <span className="ml-auto tabular-nums text-muted-foreground">
              {c.fraction > 0 ? `${(c.fraction * 100).toFixed(0)}%` : "—"}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export type StatusTone = "good" | "warning" | "critical" | "neutral";

const TONE_VAR: Record<StatusTone, string> = {
  good: "var(--cat-teal)",
  warning: "var(--cat-amber)",
  critical: "var(--cat-rose)",
  neutral: "var(--cat-muted)",
};

/**
 * Phân rã theo trạng thái: một thanh xếp chồng cộng thêm phần chú giải bên dưới.
 */
export function StatusBreakdown({
  segments,
  emptyLabel = "Chưa có dữ liệu trong khoảng thời gian này",
}: {
  segments: { label: string; value: number; tone: StatusTone }[];
  emptyLabel?: string;
}) {
  const total = segments.reduce((sum, s) => sum + s.value, 0);

  if (total === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">{emptyLabel}</p>;
  }

  return (
    <div>
      <div className="flex h-2.5 w-full gap-0.5 overflow-hidden rounded-lg bg-[var(--secondary)]">
        {segments
          .filter((s) => s.value > 0)
          .map((s) => (
            <span
              key={s.label}
              title={`${s.label}: ${s.value}`}
              style={{
                width: `${(s.value / total) * 100}%`,
                backgroundColor: TONE_VAR[s.tone],
              }}
            />
          ))}
      </div>

      <ul className="mt-4 flex flex-col gap-2.5">
        {segments.map((s) => (
          <li key={s.label} className="flex items-center gap-2.5 text-sm">
            <span
              aria-hidden
              className="size-2 shrink-0 rounded-full"
              style={{ backgroundColor: TONE_VAR[s.tone] }}
            />
            <span className="flex-1 font-medium text-foreground">{s.label}</span>
            <span className="font-semibold tabular-nums text-foreground">{s.value}</span>
            <span className="w-14 text-right tabular-nums text-muted-foreground">
              {total === 0 ? "—" : `${Math.round((s.value / total) * 100)}%`}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** Một tỉ lệ phần trăm duy nhất — con số là chính, thanh chỉ để nhìn cho nhanh. */
export function RateMeter({
  value,
  caption,
  tone = "good",
}: {
  value: number;
  caption: string;
  tone?: StatusTone;
}) {
  return (
    <div>
      <p className="font-heading text-[40px] font-bold leading-none tracking-[-0.02em] text-foreground tabular-nums">
        {value}
        <span className="ml-1 text-2xl text-muted-foreground">%</span>
      </p>
      <div className="mt-4 h-2.5 w-full overflow-hidden rounded-full bg-[var(--secondary)]">
        <span
          className="block h-full rounded-full"
          style={{
            width: `${Math.min(Math.max(value, 0), 100)}%`,
            backgroundColor: TONE_VAR[tone],
          }}
        />
      </div>
      <p className="mt-3 text-sm text-muted-foreground">{caption}</p>
    </div>
  );
}
