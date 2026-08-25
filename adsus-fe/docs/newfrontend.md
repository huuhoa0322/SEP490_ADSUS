# newfrontend.md — Dashboard Redesign Design System

> **Scope:** Admin Dashboard (`/dashboard`) redesign only. This doc captures the new
> token set and component conventions so teammates can review and apply them to other
> pages if the approach is accepted.
>
> **Baseline:** Reference image from `preclinic.dreamstechnologies.com/html/index.html`.
> See `DESIGN.md` (root) for the full extracted palette and spacing scale.

---

## 1. Color Tokens

All tokens are declared in `src/app/globals.css` under `--cat-*`. The `--cat-` prefix
signals they belong to the new design system and are **opt-in** — existing `--primary`,
`--accent`, `--status-*` tokens are unchanged so other pages are unaffected.

| Token | Hex | Role |
|-------|-----|------|
| `--cat-navy` | `#2E37A4` | Primary accent, stat tile icon bg 1, grouped-bar series 1 |
| `--cat-violet` | `#7C5CD9` | AI/tech data, optional accent |
| `--cat-magenta` | `#DD2590` | Stat tile icon bg 3, appointments series |
| `--cat-teal` | `#0E9384` | Success state, positive metric, grouped-bar series 3 |
| `--cat-blue` | `#2F80ED` | Stat tile icon bg 4, secondary accent |
| `--cat-green` | `#27AE60` | Positive delta, "confirmed" pill |
| `--cat-amber` | `#E2B93B` | Warning state, "pending" pill |
| `--cat-rose` | `#EF1E1E` | Critical state, "cancelled" pill, destructive |
| `--cat-text` | `oklch(0.145 0 0)` | Matches `--foreground` |
| `--cat-muted` | `oklch(0.45 0 0)` | Matches `--muted-foreground` |

### Usage rule

- Stat tile icons: navy / teal / magenta / blue cycle
- Donut / bar chart segments: categorical assignment, each segment gets one `--cat-*`
- Status pills: green = good, amber = warning, rose = critical
- Backgrounds / borders: **never** use `--cat-*` directly for backgrounds; use
  `--secondary` or `oklch()` values

---

## 2. Spacing & Border Radius

| Token | Value | Usage |
|-------|-------|-------|
| `--radius` | `0.625rem` | Card containers |
| Card radius | `rounded-2xl` | All ChartCard, StatTile, panel containers |
| Pill radius | `rounded-full` | Date preset buttons |
| Icon bg radius | `rounded-xl` | Stat tile icon containers |
| Badge radius | `rounded-full` | Trend / status badges |

Spacing scale unchanged — use the standard Tailwind `space-*` scale (4px base).

---

## 3. Typography

| Level | Class | Size | Usage |
|-------|-------|------|-------|
| Page title | `font-heading text-[28px] font-bold` | 28px | `h1` in dashboard header |
| Section title | `font-heading text-[15px] font-bold` | 15px | ChartCard h2 |
| Stat value | `font-heading text-[34px] font-bold tabular-nums` | 34px | StatTile, RateMeter |
| Body / label | `text-sm` | 14px | Default |
| Caption / meta | `text-xs` | 12px | Descriptions, timestamps |

**Font stack:** Inter (body) + Fraunces (headings). `font-heading` maps to Fraunces via
`globals.css --font-heading: var(--font-exo)`. Do not introduce new font families.

---

## 4. Component Specs

### StatTile v2

```
┌──────────────────────────────┐
│▌  [label: uppercase xs]  [icon-bg]
│   [value 34px bold]
│   [hint: sm muted]          │
│   [trend badge pill]        │
└──────────────────────────────┘
```

- Container: `rounded-2xl border border-[var(--border)] bg-background p-6`
- Left accent bar: `absolute left-0 top-0 h-full w-1 rounded-l-2xl`, color = `--cat-*` prop
- Icon bg: `size-10 rounded-xl flex items-center justify-center text-white`,
  bg = `--cat-*` prop
- Trend badge: `inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium`,
  bg = `--cat-*22`, color = `--cat-*`
- Props: `label`, `value`, `hint?`, `icon?`, `cat?` (CatKey), `trend?`

### ChartCard v2

```
┌─ header ───────────────────────────────┐
│ [title + description]          [action slot]
├────────────────────────────────────────┤
│                                        │
│           children                     │
│                                        │
└────────────────────────────────────────┘
```

- Header: `flex items-center justify-between border-b border-[var(--border)] px-6 py-4`
- Body: `p-6`
- Props: `title`, `description?`, `children`, `action?`, `className?`

### DonutChart

- SVG circle with `stroke-dasharray` technique
- Legend: dot + label + percentage
- Color per segment: pass in as string (`var(--cat-*)`)
- Props: `segments: { label, value, color }[]`, `size?`, `strokeWidth?`

### BarList v2

- Each bar: one color from categorical palette, cycling through `colors` prop
- Max bar width = flex-1
- Value right-aligned, `tabular-nums`
- Props: `items: { label, value }[]`, `colors?` (CatKey[]), `emptyLabel?`

### StatusBreakdown v2

- Thin segmented bar: `h-2.5 rounded-lg gap-0.5`
- Legend: dot + label (bold) + value + percentage
- Tone mapping: good→teal, warning→amber, critical→rose, neutral→muted

### RateMeter v2

- Large percentage value: `text-[40px] font-bold tabular-nums`
- Progress bar: `h-2.5 rounded-full`
- Optional side stats (taken / missed) with colored icon-box

### GroupedBarChart

- 3 series: Đã đặt (navy) / Đã huỷ (rose) / Ca khám (teal)
- Bars: `width=12, rx=3`, hover opacity=0.3 on non-hovered columns
- X-axis: date labels below (MM-DD format)
- Legend: top-right, colored dots

---

## 5. Layout Grid

```
Page: max-w-screen-2xl, px-6 py-8

Row 1: grid sm:grid-cols-2 lg:grid-cols-4  →  4 StatTiles
Row 2: full-width  →  Appointment Statistics (ChartCard, GroupedBarChart)
Row 3: grid lg:grid-cols-3
         ├ ChartCard: DonutChart (Tài khoản theo vai trò)
         ├ ChartCard: BarList (Phân bổ tài khoản)
         └ ChartCard: StatusBreakdown (Lịch hẹn)
Row 4: grid lg:grid-cols-2
         ├ ChartCard: AI Accuracy
         └ ChartCard: Tuân thủ uống thuốc
Row 5: grid sm:grid-cols-3  →  3 sparkline mini-cards (Tài khoản mới / Ca khám / Lượt hẹn)
Row 6: full-width  →  AuditLogPanel
```

---

## 6. Anti-patterns

| # | Anti-pattern | Fix |
|---|-------------|-----|
| 1 | Using `--cat-*` for backgrounds / large areas | Use `--secondary` or oklch() for backgrounds |
| 2 | One color for all bar list items | Use `colors` prop to assign each item a distinct `--cat-*` |
| 3 | Stat tile without accent bar | Always pair icon bg color with a matching left accent bar |
| 4 | Empty state showing a broken chart | Show centered text: "Chưa có dữ liệu trong khoảng thời gian này" |
| 5 | Status tone without icon/badge label | Color is never the sole information carrier — always pair with text |

---

## 7. Diff from Existing System

| Element | Old | New |
|---------|-----|-----|
| Card radius | `rounded-3xl` | `rounded-2xl` |
| Section padding | `p-6` | header `px-6 py-4`, body `p-6` |
| Stat value | `text-[34px]` | `text-[34px] tabular-nums` |
| Stat icon | `text-muted-foreground size-5` | colored `size-10 rounded-xl` bg box |
| Trend label | plain text in `hint` | pill badge |
| Border | `border-border` | `border-[var(--border)]` |
| Color source | `--status-*` | `--cat-*` for dashboard scope |

---

*Document version: 2026-08-25. Update when new pages adopt this system.*
