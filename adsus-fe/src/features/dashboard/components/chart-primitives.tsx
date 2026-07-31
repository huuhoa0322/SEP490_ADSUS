"use client";

import type { ReactNode } from "react";

/**
 * Các mảnh dựng biểu đồ cho SCR-08.
 *
 * Cố ý KHÔNG kéo thêm thư viện biểu đồ nào. Màn này chỉ cần so sánh độ lớn và tỉ lệ, dựng
 * bằng div và SVG là đủ — thêm một thư viện vài trăm KB cho mấy thanh ngang là không đáng,
 * mà lại thêm một thứ nữa cả nhóm phải học.
 */

/** Ô số liệu — dùng khi con số tự nó đã là câu trả lời, không cần vẽ gì. */
export function StatTile({
  label,
  value,
  hint,
  icon,
}: {
  label: string;
  value: string | number;
  hint?: string;
  icon?: ReactNode;
}) {
  return (
    <div className="rounded-3xl border border-border bg-background p-6">
      <div className="flex items-start justify-between gap-3">
        <span className="font-heading text-xs font-600 uppercase tracking-wider text-muted-foreground">
          {label}
        </span>
        {icon}
      </div>
      <p className="mt-3 font-heading text-[34px] font-bold leading-none tracking-[-0.02em] text-foreground tabular-nums">
        {value}
      </p>
      {hint && <p className="mt-2 text-sm text-muted-foreground">{hint}</p>}
    </div>
  );
}

export function ChartCard({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-border bg-background p-6">
      <h2 className="font-heading text-[17px] font-bold tracking-[-0.01em] text-foreground">
        {title}
      </h2>
      {description && (
        <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">{description}</p>
      )}
      <div className="mt-6">{children}</div>
    </section>
  );
}

/**
 * Danh sách thanh ngang so sánh độ lớn.
 *
 * MỘT màu cho tất cả: đây là một đại lượng duy nhất (số lượng) trải qua nhiều nhóm, không
 * phải nhiều chuỗi dữ liệu khác nhau. Tô mỗi nhóm một màu chỉ làm người đọc tưởng màu mang
 * ý nghĩa gì đó.
 *
 * Mỗi thanh luôn hiện sẵn con số bên cạnh, nên không cần chú giải, và cũng là cách bù cho
 * việc màu teal có độ tương phản thấp so với nền.
 */
export function BarList({
  items,
  emptyLabel = "Chưa có dữ liệu",
}: {
  items: { label: string; value: number }[];
  emptyLabel?: string;
}) {
  const max = Math.max(...items.map((i) => i.value), 0);

  if (max === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">{emptyLabel}</p>;
  }

  return (
    <ul className="flex flex-col gap-3.5">
      {items.map((item) => (
        <li key={item.label} className="flex items-center gap-3">
          <span className="w-28 shrink-0 text-sm text-muted-foreground">{item.label}</span>
          <span
            className="h-2.5 min-w-0.5 flex-1 overflow-hidden rounded-full bg-secondary"
            title={`${item.label}: ${item.value}`}
          >
            <span
              className="block h-full rounded-full bg-[var(--status-good)]"
              style={{ width: `${(item.value / max) * 100}%` }}
            />
          </span>
          <span className="w-12 shrink-0 text-right text-sm font-600 tabular-nums text-foreground">
            {item.value}
          </span>
        </li>
      ))}
    </ul>
  );
}

export type StatusTone = "good" | "warning" | "critical" | "neutral";

const TONE_VAR: Record<StatusTone, string> = {
  good: "var(--status-good)",
  warning: "var(--status-warning)",
  critical: "var(--status-critical)",
  neutral: "var(--muted-foreground)",
};

/**
 * Phân rã theo trạng thái: một thanh xếp chồng cộng thêm phần chú giải bên dưới.
 *
 * Màu KHÔNG BAO GIỜ là thứ duy nhất mang thông tin — mỗi phần đều có nhãn chữ và con số đi
 * kèm. Giữa các phần chừa một khe nhỏ để đường ranh không bị nhoè khi hai màu cạnh nhau.
 */
export function StatusBreakdown({
  segments,
  emptyLabel = "Chưa có dữ liệu trong khoảng thời gian này",
}: {
  segments: { label: string; value: number; tone: StatusTone }[];
  emptyLabel?: string;
}) {
  const total = segments.reduce((sum, s) => sum + s.value, 0);

  // AF-01 — khoảng thời gian không có dữ liệu là chuyện bình thường, nói rõ ra thay vì hiện
  // một dãy số 0 khiến người dùng tưởng màn hình hỏng.
  if (total === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">{emptyLabel}</p>;
  }

  return (
    <div>
      <div className="flex h-3 w-full gap-0.5 overflow-hidden rounded-full bg-secondary">
        {total > 0 &&
          segments
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

      <ul className="mt-5 flex flex-col gap-2.5">
        {segments.map((s) => (
          <li key={s.label} className="flex items-center gap-2.5 text-sm">
            <span
              aria-hidden
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: TONE_VAR[s.tone] }}
            />
            <span className="flex-1 text-muted-foreground">{s.label}</span>
            <span className="font-600 tabular-nums text-foreground">{s.value}</span>
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
      <div className="mt-4 h-2.5 w-full overflow-hidden rounded-full bg-secondary">
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
