"use client";

import { useState } from "react";

import type { DailyPoint } from "../types/dashboard.types";

/** Format ngày yyyy-MM-dd thành dd/MM để hiển thị trên trục X biểu đồ. */
function formatChartDate(dateStr: string): string {
  const parts = dateStr.split("-");
  if (parts.length !== 3) return dateStr;
  return `${parts[2]}/${parts[1]}`;
}

/** Format ngày yyyy-MM-dd thành dd/MM/yyyy để hiển thị trên tooltip khi hover. */
function formatFullDate(dateStr: string): string {
  const parts = dateStr.split("-");
  if (parts.length !== 3) return dateStr;
  return `${parts[2]}/${parts[1]}/${parts[0]}`;
}

/** Đại lượng nào của một ngày sẽ được vẽ. */
export type Measure = "newAccounts" | "cases" | "appointments" | "revenue";

function formatValue(val: number, measure: Measure): string {
  if (measure === "revenue") {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(val);
  }
  return String(val);
}

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

  const values = points.map((p) => p[measure] ?? 0);
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
    .map((p, i) => `${i === 0 ? "M" : "L"} ${xAt(i).toFixed(1)} ${yAt(p[measure] ?? 0).toFixed(1)}`)
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
          {formatValue(total, measure)} · cao nhất {formatValue(max, measure)}/ngày
        </span>
      </div>

      <div className="relative mt-4">
        <svg
          viewBox={`0 0 ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
          className="h-32 w-full overflow-visible"
          role="img"
          aria-label={`${label}: tổng ${formatValue(total, measure)} trong kỳ, cao nhất ${formatValue(max, measure)} một ngày`}
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
                cy={yAt(hovered[measure] ?? 0)}
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
              {formatValue(hovered[measure] ?? 0, measure)}
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

  const CHART_H = 160;

  // ViewBox chuẩn 1000 đơn vị, dùng preserveAspectRatio="none" để dãn 100% toàn bộ khung
  const VIEW_WIDTH = 1000;
  const n = points.length;
  const colW = VIEW_WIDTH / n;

  // Tính toán kích thước thanh trong từng cột
  const maxGroupW = Math.min(colW * 0.72, 38);
  const barGap = Math.max(1, Math.min(3, maxGroupW * 0.08));
  const barW = Math.max(2, (maxGroupW - (series.length - 1) * barGap) / series.length);
  const totalBarsW = series.length * barW + (series.length - 1) * barGap;

  // Giá trị cao nhất trên toàn bộ các chuỗi
  const allValues = points.flatMap((p) => series.map((s) => (p[s.key] as number) ?? 0));
  const max = Math.max(...allValues, 1);
  const chartCeiling = Math.ceil(max * 1.15);

  const barH = (val: number) => (val === 0 ? 0 : Math.max(3, (val / chartCeiling) * (CHART_H - 15)));
  const barY = (val: number) => CHART_H - barH(val);

  // Hiển thị nhãn ngày thông minh:
  // - 7 ngày: hiện đủ 7 ngày
  // - 14 ngày: hiện cách 2 ngày
  // - 30 ngày: hiện cách 4 ngày (~8 nhãn)
  // - 60 ngày+: hiện cách 7-10 ngày
  const labelInterval =
    n <= 8
      ? 1
      : n <= 15
      ? 2
      : n <= 35
      ? 4
      : n <= 65
      ? 7
      : Math.ceil(n / 10);

  const hoveredPoint = hoverCol !== null ? points[hoverCol] : null;

  return (
    <figure className="relative">
      <figcaption className="mb-4 flex items-center justify-between">
        <span className="font-heading text-[15px] font-semibold text-foreground">{title}</span>
        <div className="flex items-center gap-4">
          {series.map((s) => (
            <span key={s.label} className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span className="size-2 rounded-full shrink-0" style={{ backgroundColor: s.color }} />
              {s.label}
            </span>
          ))}
        </div>
      </figcaption>

      {/* Floating tooltip hiển thị Ngày và Chi tiết các đại lượng khi hover */}
      {hoveredPoint && hoverCol !== null && (
        <div
          className="pointer-events-none absolute top-1 z-30 flex flex-col gap-1 rounded-xl border border-[var(--border)] bg-background/95 px-3.5 py-2 text-xs shadow-xl backdrop-blur-sm transition-all"
          style={{
            left: `${((hoverCol + 0.5) / n) * 100}%`,
            transform: hoverCol > n * 0.7 ? "translateX(-105%)" : hoverCol < n * 0.3 ? "translateX(5%)" : "translateX(-50%)",
          }}
        >
          <div className="font-semibold text-foreground border-b border-border/50 pb-1 flex items-center justify-between gap-4">
            <span>📅 {formatFullDate(hoveredPoint.date)}</span>
          </div>
          <div className="flex flex-col gap-1 pt-0.5">
            {series.map((s) => {
              const val = (hoveredPoint[s.key] as number) ?? 0;
              return (
                <div key={s.label} className="flex items-center justify-between gap-4">
                  <span className="flex items-center gap-1.5 text-muted-foreground">
                    <span className="size-2 rounded-full shrink-0" style={{ backgroundColor: s.color }} />
                    {s.label}
                  </span>
                  <span className="font-semibold tabular-nums text-foreground">{val}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="w-full">
        <svg
          viewBox={`0 0 ${VIEW_WIDTH} ${CHART_H}`}
          className="w-full h-44 sm:h-52 min-w-0"
          preserveAspectRatio="none"
          role="img"
          aria-label={title}
        >
          {/* Lưới ngang phụ */}
          <line
            x1="0"
            y1={CHART_H * 0.33}
            x2={VIEW_WIDTH}
            y2={CHART_H * 0.33}
            stroke="var(--border)"
            strokeDasharray="4 4"
            strokeWidth="0.75"
            opacity="0.4"
          />
          <line
            x1="0"
            y1={CHART_H * 0.66}
            x2={VIEW_WIDTH}
            y2={CHART_H * 0.66}
            stroke="var(--border)"
            strokeDasharray="4 4"
            strokeWidth="0.75"
            opacity="0.4"
          />

          {/* Đường baseline đáy */}
          <line
            x1="0"
            y1={CHART_H}
            x2={VIEW_WIDTH}
            y2={CHART_H}
            stroke="var(--border)"
            strokeWidth="1"
          />

          {/* Các cột dữ liệu theo ngày */}
          {points.map((p, ci) => {
            const colCenterX = (ci + 0.5) * colW;
            const groupStartX = colCenterX - totalBarsW / 2;
            const isHovered = hoverCol === ci;

            return (
              <g key={ci}>
                {/* Highlight nền cột khi hover */}
                {isHovered && (
                  <rect
                    x={ci * colW + 1}
                    y={2}
                    width={colW - 2}
                    height={CHART_H - 2}
                    fill="currentColor"
                    className="fill-muted/20"
                    rx="4"
                  />
                )}

                {/* Các thanh biểu đồ của từng series */}
                {series.map((s, si) => {
                  const val = (p[s.key] as number) ?? 0;
                  const bx = groupStartX + si * (barW + barGap);
                  return (
                    <rect
                      key={si}
                      x={bx}
                      y={barY(val)}
                      width={barW}
                      height={barH(val)}
                      rx={Math.min(3, barW / 2)}
                      fill={s.color}
                      opacity={hoverCol === null || isHovered ? 1 : 0.3}
                      className="transition-opacity"
                    >
                      <title>{`${formatFullDate(p.date)} — ${s.label}: ${val}`}</title>
                    </rect>
                  );
                })}

                {/* Vùng cảm ứng hover bao phủ toàn bộ cột */}
                <rect
                  x={ci * colW}
                  y={0}
                  width={colW}
                  height={CHART_H}
                  fill="transparent"
                  className="cursor-pointer"
                  onMouseEnter={() => setHoverCol(ci)}
                  onMouseLeave={() => setHoverCol(null)}
                />
              </g>
            );
          })}
        </svg>

        {/* X-axis labels: Dùng HTML theo % để chữ luôn sắc nét và không bao giờ bị đè nhau */}
        <div className="relative mt-2.5 h-6 w-full select-none">
          {points.map((p, ci) => {
            const isHovered = hoverCol === ci;
            const isLast = ci === n - 1;
            const isNearLast = n - 1 - ci < Math.floor(labelInterval / 2);
            const showLabel = (ci % labelInterval === 0 && !isNearLast) || isLast;

            if (!showLabel) return null;

            return (
              <span
                key={ci}
                className={`absolute text-xs tabular-nums transition-colors ${
                  isHovered ? "font-bold text-foreground" : "text-muted-foreground"
                }`}
                style={{
                  left: `${((ci + 0.5) / n) * 100}%`,
                  transform: isLast ? "translateX(-90%)" : ci === 0 ? "translateX(-10%)" : "translateX(-50%)",
                }}
              >
                {formatChartDate(p.date)}
              </span>
            );
          })}
        </div>
      </div>
    </figure>
  );
}
