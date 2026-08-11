# Schedule Slot Management UI Refinement Plan v2

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Further refine the schedule slot management UI:
1. Change slot button layout: patient name as primary action, X button for close
2. Make all slots uniformly sized and aligned
3. Add date to weekday header labels (e.g., "T2 (10/08)")

**Architecture:** Single-file component refactor in `schedule-slot-management-view.tsx`.

**Tech Stack:** React, Tailwind CSS, Lucide React icons (X already imported)

---

## Global Constraints

- **No external UI library changes** - use existing Tailwind-only approach
- **Maintain Vietnamese text** for all labels
- **Preserve all existing functionality** - create, close, reopen slots

---

## Task Structure

### Task 1: Update SlotCard Button Layout

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

**Changes to `SlotCard` component (lines 329-388):**

Current layout for BOOKED slots shows patient name above and "Đã đặt" badge below.

**New layout:**
1. **For BOOKED slots:** Display patient name as primary text, keep "Đã đặt" badge, add small ✕ button to close
2. **For OPEN slots:** Display "Trống" or show time only, add patient name if no booking, add ✕ button to close
3. **For CLOSED slots:** Keep current "Mở lại" button

**New SlotCard structure:**
```tsx
function SlotCard({
  slot,
  onClose,
  onReopen,
}: {
  slot: ScheduleSlotResponse;
  onClose: () => void;
  onReopen: () => void;
}) {
  const patientName = slot.bookedAppointments?.[0]?.patientFullName;
  const hasBooking = !!patientName;

  return (
    <div className={`flex flex-col rounded border p-2 text-xs ${slot.status === "CLOSED" ? "border-slate-300 bg-slate-50" : "border-slate-200 bg-white"}`}>
      {/* Header: Time range + Status badge */}
      <div className="flex items-start justify-between gap-0.5">
        <span className="font-mono text-xs font-medium text-slate-600">
          {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
        </span>
        <span className={`shrink-0 rounded-full px-1.5 py-0.5 text-[10px] ${STATUS_STYLES[slot.status]}`}>
          {STATUS_LABELS[slot.status]}
        </span>
      </div>

      {/* Patient name - primary action for BOOKED */}
      <div className="mt-1 flex items-center justify-between gap-1">
        <span className={`flex-1 truncate text-xs ${hasBooking ? "text-blue-700 font-medium" : "text-slate-400"}`}>
          {hasBooking ? patientName : "—"}
        </span>
        
        {/* Close button - small X icon */}
        {slot.status !== "CLOSED" && (
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 rounded p-0.5 text-slate-400 hover:bg-red-50 hover:text-red-500"
            title="Đóng ca"
          >
            <X className="h-3 w-3" />
          </button>
        )}
      </div>

      {/* Status indicator / Reopen button */}
      {slot.status === "CLOSED" && (
        <button
          type="button"
          onClick={onReopen}
          className="mt-1 w-full rounded border border-green-200 bg-green-50 px-2 py-1 text-xs text-green-600 hover:bg-green-100"
        >
          Mở lại
        </button>
      )}
    </div>
  );
}
```

**Key changes:**
- `X` icon is already imported (line 3)
- Patient name becomes the primary content for BOOKED slots
- Small ✕ button for closing (not a full button with text)
- Consistent flex layout for all slot states
- Fixed height using `flex flex-col`

---

### Task 2: Update Weekday Header with Date

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

**Changes to `DayColumn` component (lines 270-327):**

Current header shows: "T2" + "10" (number)

**New header format:**
- T2 column: "T2 (10/08)"
- T3 column: "T3 (11/08)"
- etc.

```tsx
function DayColumn({
  dateIso,
  weekdayLabel,
  slots,
  isPast,
  onAddClick,
  onClose,
  onReopen,
}: {
  dateIso: string;
  weekdayLabel: string;
  slots: ScheduleSlotResponse[];
  isPast: boolean;
  onAddClick: (dateIso: string) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
  onReopen: (s: ScheduleSlotResponse) => void | Promise<void>;
}) {
  const isWeekend = weekdayLabel === "T7" || weekdayLabel === "CN";
  
  // Parse date for display
  const date = new Date(dateIso);
  const dayOfMonth = date.getDate().toString().padStart(2, "0");
  const month = (date.getMonth() + 1).toString().padStart(2, "0");

  return (
    <div className={`flex min-h-[280px] flex-col rounded border p-2 ${isWeekend ? "border-amber-200 bg-amber-50/30" : "border-slate-200 bg-white"}`}>
      {/* Header with weekday + date */}
      <div className="mb-1 text-center">
        <div className={`text-xs font-semibold ${isWeekend ? "text-amber-600" : "text-slate-500"}`}>
          {weekdayLabel} ({dayOfMonth}/{month})
        </div>
      </div>
      
      {/* Slots container - fixed height, scrollable if needed */}
      <div className="flex-1 space-y-1">
        {slots.length === 0 && (
          <div className={`rounded border border-dashed p-2 text-center text-xs ${isWeekend ? "border-amber-200 text-amber-400" : "border-slate-200 text-slate-400"}`}>
            {isPast ? "Qua" : "—"}
          </div>
        )}
        {slots.map((s) => (
          <SlotCard
            key={s.slotId}
            slot={s}
            onClose={() => void onClose(s, false)}
            onReopen={() => void onReopen(s)}
          />
        ))}
      </div>
      
      {/* Add button - always at bottom */}
      {!isPast && (
        <button
          type="button"
          onClick={() => onAddClick(dateIso)}
          className="mt-2 w-full rounded border border-dashed border-slate-300 p-1 text-xs text-slate-400 hover:border-blue-400 hover:text-blue-500"
        >
          + Thêm
        </button>
      )}
    </div>
  );
}
```

**Key changes:**
- Header now shows: "T2 (10/08)" format
- DayColumn uses `flex flex-col min-h-[280px]` for consistent height
- Slots container uses `flex-1` to fill available space
- "Thêm" button always at bottom with `mt-2`

---

### Task 3: Update WeekView for Consistent Heights

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

**Changes to `WeekView` component (lines 231-267):**

Add `min-h-[600px]` to ensure all columns have consistent height and align properly.

```tsx
function WeekView({
  weekStart,
  todayIso,
  slotsByDay,
  onAddClick,
  onClose,
  onReopen,
}: {
  weekStart: Date;
  todayIso: string;
  slotsByDay: Map<string, ScheduleSlotResponse[]>;
  onAddClick: (dateIso: string) => void;
  onClose: (s: ScheduleSlotResponse, force: boolean) => void | Promise<void>;
  onReopen: (s: ScheduleSlotResponse) => void | Promise<void>;
}) {
  return (
    <div className="grid min-h-[600px] grid-cols-7 gap-3">
      {Array.from({ length: 7 }).map((_, i) => {
        const date = addDays(weekStart, i);
        const dateIso = isoDate(date);
        const daySlots = slotsByDay.get(dateIso) ?? [];
        const isPast = dateIso < todayIso;
        return (
          <DayColumn
            key={dateIso}
            dateIso={dateIso}
            weekdayLabel={WEEKDAY_LABELS_VI[i % 7]}
            slots={daySlots}
            isPast={isPast}
            onAddClick={onAddClick}
            onClose={onClose}
            onReopen={onReopen}
          />
        );
      })}
    </div>
  );
}
```

**Key changes:**
- Added `min-h-[600px]` to grid container
- Ensures all columns stretch to same minimum height
- Columns will grow as slots fill up

---

## Verification Checklist

- [ ] All slots display with uniform height and alignment
- [ ] Patient name shows prominently for BOOKED slots
- [ ] X button (✕) visible for closing slots
- [ ] Weekday headers show date: "T2 (10/08)"
- [ ] Create, close, reopen all work correctly
- [ ] Responsive on smaller screens (scroll if needed)

---

## Execution Options

**Plan complete and saved to `docs/superpowers/plans/2026-08-11-schedule-ui-refinement-v2.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks

**2. Inline Execution** - Execute tasks in this session with checkpoints

**Which approach?**
