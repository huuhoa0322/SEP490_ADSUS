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
      <figure className="rounded-3xl border border-border bg-background p-6">
        <figcaption className="font-heading text-[15px] font-600 text-foreground">
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

  // Chia cho max, không phải cho tổng. Trục tung luôn bắt đầu từ 0 — cắt đáy để "đường dốc
  // hơn cho dễ thấy" là phóng đại biến động, đọc ra sai hẳn mức độ thay đổi.
  const yAt = (v: number) => PADDING_TOP + plotHeight - (max === 0 ? 0 : v / max) * plotHeight;

  const linePath = points
    .map((p, i) => `${i === 0 ? "M" : "L"} ${xAt(i).toFixed(1)} ${yAt(p[measure]).toFixed(1)}`)
    .join(" ");

  const areaPath =
    `${linePath} L ${xAt(points.length - 1).toFixed(1)} ${PADDING_TOP + plotHeight} ` +
    `L ${xAt(0).toFixed(1)} ${PADDING_TOP + plotHeight} Z`;

  const gradientId = `trend-fill-${measure}`;
  const hovered = hoverIndex === null ? null : points[hoverIndex];

  /** Đổi vị trí chuột thành chỉ số điểm gần nhất. */
  function handleMove(event: React.MouseEvent<SVGSVGElement>) {
    const box = event.currentTarget.getBoundingClientRect();
    const ratio = (event.clientX - box.left) / box.width;
    const index = Math.round(ratio * (points.length - 1));
    setHoverIndex(Math.min(Math.max(index, 0), points.length - 1));
  }

  return (
    <figure className="rounded-3xl border border-border bg-background p-6">
      <div className="flex items-baseline justify-between gap-3">
        <figcaption className="font-heading text-[15px] font-600 text-foreground">
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
              <stop offset="0%" stopColor="var(--status-good)" stopOpacity="0.22" />
              <stop offset="100%" stopColor="var(--status-good)" stopOpacity="0" />
            </linearGradient>
          </defs>

          {/* Đường đáy, để mắt biết mốc 0 nằm đâu. Nhạt hơn hẳn dữ liệu. */}
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
            stroke="var(--status-good)"
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
              {/* Vòng nền cùng màu mặt giấy, để điểm không lẫn vào đường bên dưới. */}
              <circle
                cx={xAt(hoverIndex)}
                cy={yAt(hovered[measure])}
                r="5"
                fill="var(--background)"
                stroke="var(--status-good)"
                strokeWidth="2"
              />
            </g>
          )}
        </svg>

        {hovered && (
          <div
            className="pointer-events-none absolute -top-1 rounded-xl border border-border bg-background px-3 py-1.5 text-xs shadow-lg"
            style={{
              left: `${(hoverIndex! / Math.max(points.length - 1, 1)) * 100}%`,
              transform: "translateX(-50%)",
            }}
          >
            <span className="tabular-nums text-muted-foreground">{hovered.date}</span>
            <span className="ml-2 font-600 tabular-nums text-foreground">
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
