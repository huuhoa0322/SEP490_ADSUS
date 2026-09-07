"use client";

import { useState } from "react";

import type { DailyPoint } from "../types/dashboard.types";

/** Đại lượng nào của một ngày sẽ được vẽ. */
type Measure = "newAccounts" | "cases" | "appointments";

const VIEW_WIDTH = 480;
const VIEW_HEIGHT = 120;
const PADDING_TOP = 10;
const PADDING_BOTTOM = 18;

/**
 * Biểu đồ xu hướng theo ngày (UC-05 bước 3).
 *
 * MỖI ĐẠI LƯỢNG MỘT BIỂU ĐỒ RIÊNG, không gộp ba đường vào một khung.
 * Số tài khoản mới, số ca khám và số lượt hẹn có thang đo khác hẳn nhau; vẽ chung thì hoặc
 * phải dùng hai trục tung — kiểu biểu đồ gây hiểu nhầm nhiều nhất — hoặc đường nhỏ bị ép bẹp
 * xuống sát đáy và không đọc được gì.
 *
 * Một chuỗi thì không cần chú giải: tiêu đề đã nói nó là gì.
 */
export function TrendChart({
  points,
  measure,
  label,
}: {
  points: DailyPoint[];
  measure: Measure;
  label: string;
}) {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null);

  const values = points.map((p) => p[measure]);
  const max = Math.max(...values, 0);
  const total = values.reduce((sum, v) => sum + v, 0);

  if (points.length === 0 || total === 0) {
    return (
      <figure className="rounded-2xl border border-[var(--border)] bg-background p-6">
        <figcaption className="font-heading text-[15px] font-semibold text-foreground">
          {label}
        </figcaption>
        <p className="flex h-32 items-center justify-center text-sm text-muted-foreground">
          Chưa có dữ liệu trong khoảng thời gian này
        </p>
      </figure>
    );
  }

  const plotHeight = VIEW_HEIGHT - PADDING_TOP - PADDING_BOTTOM;

  /** Toạ độ X của điểm thứ i. Một điểm duy nhất thì đặt giữa khung cho khỏi dính mép. */
  const xAt = (i: number) =>
    points.length === 1 ? VIEW_WIDTH / 2 : (i / (points.length - 1)) * VIEW_WIDTH;

  const yAt = (v: number) =>
    PADDING_TOP + plotHeight - (max === 0 ? 0 : (v / max) * plotHeight);

  const linePath = points
    .map((p, i) => `${i === 0 ? "M" : "L"} ${xAt(i).toFixed(1)} ${yAt(p[measure]).toFixed(1)}`)
    .join(" ");

  const areaPath =
    `${linePath} L ${xAt(points.length - 1).toFixed(1)} ${PADDING_TOP + plotHeight} ` +
    `L ${xAt(0).toFixed(1)} ${PADDING_TOP + plotHeight} Z`;

  const gradientId = `trend-fill-${measure}`;
  const hovered = hoverIndex === null ? null : points[hoverIndex];

  function handleMove(event: React.MouseEvent<SVGSVGElement>) {
    const box = event.currentTarget.getBoundingClientRect();
    const ratio = (event.clientX - box.left) / box.width;
    const index = Math.round(ratio * (points.length - 1));
    setHoverIndex(Math.min(Math.max(index, 0), points.length - 1));
  }

  return (
    <figure className="rounded-2xl border border-[var(--border)] bg-background p-6">
      <div className="flex items-baseline justify-between gap-3">
        <figcaption className="font-heading text-[15px] font-semibold text-foreground">
          {label}
        </figcaption>
        <span className="text-sm tabular-nums text-muted-foreground">
          {total} · cao nhất {max}/ngày
        </span>
      </div>

      <div className="relative mt-4">
        <svg
          viewBox={`0 0 ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
          className="h-32 w-full overflow-visible"
          role="img"
          aria-label={`${label}: tổng ${total} trong kỳ, cao nhất ${max} một ngày`}
          onMouseMove={handleMove}
          onMouseLeave={() => setHoverIndex(null)}
        >
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--cat-teal)" stopOpacity="0.22" />
              <stop offset="100%" stopColor="var(--cat-teal)" stopOpacity="0" />
            </linearGradient>
          </defs>

          <line
            x1="0"
            y1={PADDING_TOP + plotHeight}
            x2={VIEW_WIDTH}
            y2={PADDING_TOP + plotHeight}
            stroke="var(--border)"
            strokeWidth="1"
          />

          <path d={areaPath} fill={`url(#${gradientId})`} />
          <path
            d={linePath}
            fill="none"
            stroke="var(--cat-teal)"
            strokeWidth="2"
            strokeLinejoin="round"
            strokeLinecap="round"
          />

          {hoverIndex !== null && hovered && (
            <g>
              <line
                x1={xAt(hoverIndex)}
                y1={PADDING_TOP}
                x2={xAt(hoverIndex)}
                y2={PADDING_TOP + plotHeight}
                stroke="var(--border)"
                strokeWidth="1"
              />
              <circle
                cx={xAt(hoverIndex)}
                cy={yAt(hovered[measure])}
                r="5"
                fill="var(--background)"
                stroke="var(--cat-teal)"
                strokeWidth="2"
              />
            </g>
          )}
        </svg>

        {hovered && (
          <div
            className="pointer-events-none absolute -top-1 rounded-xl border border-[var(--border)] bg-background px-3 py-1.5 text-xs shadow-lg"
            style={{
              left: `${(hoverIndex! / Math.max(points.length - 1, 1)) * 100}%`,
              transform: "translateX(-50%)",
            }}
          >
            <span className="tabular-nums text-muted-foreground">{hovered.date}</span>
            <span className="ml-2 font-semibold tabular-nums text-foreground">
              {hovered[measure]}
            </span>
          </div>
        )}
      </div>

      <div className="mt-1.5 flex justify-between text-xs tabular-nums text-muted-foreground">
        <span>{points[0].date}</span>
        <span>{points[points.length - 1].date}</span>
      </div>
    </figure>
  );
}

// ─── Grouped Bar Chart (Appointment Statistics) ──────────────────────────────

export type Series = { label: string; color: string; key: keyof DailyPoint };

export const APPOINTMENT_SERIES: Series[] = [
  { label: "Tài khoản mới", color: "var(--cat-navy)", key: "newAccounts" },
  { label: "Ca khám", color: "var(--cat-rose)", key: "cases" },
  { label: "Lượt hẹn", color: "var(--cat-teal)", key: "appointments" },
];

export interface GroupedBarProps {
  points: DailyPoint[];
  series: Series[];
  title: string;
}

export function GroupedBarChart({ points, series, title }: GroupedBarProps) {
  const [hoverCol, setHoverCol] = useState<number | null>(null);

  if (points.length === 0) {
    return (
      <figure className="rounded-2xl border border-[var(--border)] bg-background p-6">
        <figcaption className="font-heading text-[15px] font-semibold text-foreground">
          {title}
        </figcaption>
        <p className="flex h-48 items-center justify-center text-sm text-muted-foreground">
          Chưa có dữ liệu
        </p>
      </figure>
    );
  }

  const BAR_GROUP_W = 48;
  const BAR_W = 12;
  const GAP = (BAR_GROUP_W - series.length * BAR_W) / (series.length + 1);
  const CHART_H = 140;
  const LABEL_H = 32;
  const PAD_LEFT = 8;
  const PAD_RIGHT = 8;

  const totals = points.map((p) =>
    series.reduce((s, ser) => s + ((p[ser.key] as number) ?? 0), 0)
  );
  const max = Math.max(...totals, 1);
  const chartW = points.length * BAR_GROUP_W + PAD_LEFT + PAD_RIGHT;

  const colX = (i: number) => PAD_LEFT + i * BAR_GROUP_W;

  const barX = (colIdx: number, serIdx: number) =>
    colX(colIdx) + GAP + serIdx * (BAR_W + GAP / 2);

  const barH = (val: number) => Math.max(2, (val / max) * (CHART_H - 10));

  const barY = (val: number) => CHART_H - barH(val);

  return (
    <figure>
      <figcaption className="mb-4 flex items-center justify-between">
        <span className="font-heading text-[15px] font-semibold text-foreground">{title}</span>
        <div className="flex items-center gap-4">
          {series.map((s) => (
            <span key={s.label} className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span className="size-2 rounded-full" style={{ backgroundColor: s.color }} />
              {s.label}
            </span>
          ))}
        </div>
      </figcaption>

      <div className="overflow-x-auto">
        <svg
          viewBox={`0 0 ${chartW} ${CHART_H + LABEL_H}`}
          className="w-full min-w-0"
          style={{ maxHeight: CHART_H + LABEL_H }}
          role="img"
          aria-label={title}
        >
          {/* Baseline */}
          <line
            x1="0"
            y1={CHART_H}
            x2={chartW}
            y2={CHART_H}
            stroke="var(--border)"
            strokeWidth="1"
          />

          {points.map((p, ci) => (
            <g key={ci}>
              {series.map((s, si) => {
                const val = (p[s.key] as number) ?? 0;
                const isHovered = hoverCol === ci;
                return (
                  <rect
                    key={si}
                    x={barX(ci, si)}
                    y={barY(val)}
                    width={BAR_W}
                    height={barH(val)}
                    rx="3"
                    fill={s.color}
                    opacity={hoverCol === null || isHovered ? 1 : 0.3}
                    onMouseEnter={() => setHoverCol(ci)}
                    onMouseLeave={() => setHoverCol(null)}
                  >
                    <title>{`${s.label}: ${val}`}</title>
                  </rect>
                );
              })}
            </g>
          ))}
        </svg>
      </div>

      {/* X-axis labels */}
      <div className="mt-2 flex" style={{ paddingLeft: `${PAD_LEFT}px` }}>
        {points.map((p, ci) => (
          <div
            key={ci}
            className="flex-shrink-0 text-center"
            style={{ width: `${BAR_GROUP_W}px` }}
          >
            <span className="text-xs tabular-nums text-muted-foreground">
              {p.date.slice(5)}
            </span>
          </div>
        ))}
      </div>
    </figure>
  );
}
