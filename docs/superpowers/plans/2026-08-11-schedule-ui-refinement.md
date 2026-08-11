# Schedule Slot Management UI Refinement Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine the schedule slot management UI at `/schedule` - remove edit feature, increase component sizes for better visibility, and replace browser `confirm()` dialogs with custom modal UI.

**Architecture:** Single-file component refactor in `schedule-slot-management-view.tsx`. Remove edit-related state and callbacks, introduce a `ConfirmModal` component for close/reopen confirmations, and scale up all font sizes and spacing.

**Tech Stack:** React, Tailwind CSS, Lucide React icons, react-hot-toast

---

## Global Constraints

- **No external UI library changes** - use existing Tailwind-only approach
- **Preserve all existing API hooks** - only remove UI elements, not API calls
- **Maintain Vietnamese text** for all labels and messages
- **Keep slot creation feature unchanged**

---

## Task Structure

### Task 1: Remove Edit Feature

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

**Changes:**
- Remove `useUpdateScheduleSlot` from imports (line 14)
- Remove `UpdateScheduleSlotRequest` from type imports (line 21)
- Remove `editingSlot` state (line 50)
- Remove `updateMutation` (line 65)
- Remove `onEdit` from WeekView props (line 147)
- Remove `editingSlot && EditSlotModal` block (lines 213-229)
- Remove `EditSlotModal` component (lines 462-497)
- Remove `onEdit` from DayColumn props (lines 250, 269, 284, 294)
- Remove `onEdit` from SlotCard props (lines 250, 269, 284, 294)
- Remove edit button from SlotCard (lines 382-388)

---

### Task 2: Add ConfirmModal Component

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

Add this component after `ModalActions`:

```tsx
function ConfirmModal({
  title,
  message,
  confirmLabel = "Xác nhận",
  cancelLabel = "Hủy",
  variant = "danger", // "danger" | "warning" | "info"
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "danger" | "warning" | "info";
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const variantStyles = {
    danger: {
      icon: "text-red-500",
      iconBg: "bg-red-100",
      confirmBtn: "bg-red-600 hover:bg-red-700",
    },
    warning: {
      icon: "text-amber-500",
      iconBg: "bg-amber-100",
      confirmBtn: "bg-amber-600 hover:bg-amber-700",
    },
    info: {
      icon: "text-blue-500",
      iconBg: "bg-blue-100",
      confirmBtn: "bg-blue-600 hover:bg-blue-700",
    },
  };
  const styles = variantStyles[variant];

  return (
    <ModalShell title="" onClose={onCancel}>
      <div className="flex items-start gap-4">
        <div className={`shrink-0 rounded-full p-3 ${styles.iconBg}`}>
          {variant === "danger" && (
            <svg className={`h-6 w-6 ${styles.icon}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          )}
          {variant === "warning" && (
            <svg className={`h-6 w-6 ${styles.icon}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          )}
          {variant === "info" && (
            <svg className={`h-6 w-6 ${styles.icon}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          )}
        </div>
        <div className="flex-1">
          <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
          <p className="mt-2 text-sm text-slate-600">{message}</p>
        </div>
      </div>
      <div className="mt-6 flex justify-end gap-3">
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          {cancelLabel}
        </button>
        <button
          type="button"
          onClick={onConfirm}
          className={`rounded-md px-4 py-2 text-sm font-medium text-white ${styles.confirmBtn}`}
        >
          {confirmLabel}
        </button>
      </div>
    </ModalShell>
  );
}
```

---

### Task 3: Add Confirmation State and Replace confirm() Calls

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

**Changes:**

1. Add new state after existing states (after line 50):
```tsx
const [confirmAction, setConfirmAction] = useState<{
  type: "close" | "forceClose" | "reopen";
  slot: ScheduleSlotResponse;
} | null>(null);
```

2. Replace `confirm()` in `onClose` handler (lines 148-181) - remove browser `confirm()` and instead set `confirmAction` state:
```tsx
onClose={async (s, force) => {
  if (!force) {
    setConfirmAction({ type: "close", slot: s });
    return;
  }
  setConfirmAction({ type: "forceClose", slot: s });
}}
```

3. Replace `confirm()` in `onReopen` handler (lines 182-191):
```tsx
onReopen={(s) => setConfirmAction({ type: "reopen", slot: s })}
```

4. Add confirmation modal rendering after existing modals (after line 229):
```tsx
{confirmAction && (
  <ConfirmModal
    title={
      confirmAction.type === "close"
        ? "Xác nhận đóng ca"
        : confirmAction.type === "forceClose"
        ? "Xác nhận đóng ca (có booking)"
        : "Xác nhận mở lại ca"
    }
    message={
      confirmAction.type === "close"
        ? confirmAction.slot.activeAppointmentsCount > 0
          ? `Ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)} ngày ${confirmAction.slot.slotDate} có ${confirmAction.slot.activeAppointmentsCount} lịch hẹn đang đặt. Bạn có chắc muốn đóng?`
          : `Bạn có chắc muốn đóng ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)} ngày ${confirmAction.slot.slotDate}?`
        : confirmAction.type === "forceClose"
        ? `Khung giờ này có ${confirmAction.slot.activeAppointmentsCount} lịch hẹn đang BOOKED. Các booking hiện tại vẫn giữ nguyên, nhưng bệnh nhân không đặt thêm được.`
        : `Mở lại ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)} ngày ${confirmAction.slot.slotDate}?`
    }
    variant={confirmAction.type === "reopen" ? "info" : confirmAction.type === "forceClose" ? "warning" : "danger"}
    confirmLabel={
      confirmAction.type === "close" ? "Đóng ca"
      : confirmAction.type === "forceClose" ? "Đóng ca"
      : "Mở lại"
    }
    onConfirm={async () => {
      try {
        if (confirmAction.type === "close") {
          await closeMutation.mutateAsync({ id: confirmAction.slot.slotId, force: false });
        } else if (confirmAction.type === "forceClose") {
          await closeMutation.mutateAsync({ id: confirmAction.slot.slotId, force: true });
        } else {
          await reopenMutation.mutateAsync(confirmAction.slot.slotId);
        }
        setConfirmAction(null);
        await listQuery.refetch();
        toast.success(
          confirmAction.type === "reopen"
            ? `Đã mở lại ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)}.`
            : `Đã đóng ca khám ${confirmAction.slot.startTime.slice(0, 5)}–${confirmAction.slot.endTime.slice(0, 5)}.`
        );
      } catch (err) {
        setConfirmAction(null);
        toast.error(getApiErrorMessage(err, "Thao tác thất bại."));
      }
    }}
    onCancel={() => setConfirmAction(null)}
  />
)}
```

---

### Task 4: Scale Up UI Components

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

**Changes - increase all font sizes and spacing:**

1. **WeekView container** (line 255):
```tsx
// Change: gap-2 → gap-3
<div className="grid grid-cols-7 gap-3">
```

2. **DayColumn** (line 303):
```tsx
// Change min-h-[200px] → min-h-[280px]
// Change p-1 → p-2
<div className={`min-h-[280px] rounded border p-2 ${isWeekend ? "border-amber-200 bg-amber-50/30" : "border-slate-200 bg-white"}`}>
```

3. **DayColumn weekday label** (line 305):
```tsx
// Change text-[10px] → text-xs
<div className={`text-xs font-semibold ${isWeekend ? "text-amber-600" : "text-slate-500"}`}>
```

4. **DayColumn day number** (line 308):
```tsx
// Change text-sm → text-base
<div className={`text-base ${isPast ? "text-slate-400" : "text-slate-700"}`}>
```

5. **DayColumn empty state** (line 314):
```tsx
// Change text-[9px] → text-xs
// Change p-1 → p-2
<div className={`rounded border border-dashed p-2 text-center text-xs ${isWeekend ? "border-amber-200 text-amber-400" : "border-slate-200 text-slate-400"}`}>
```

6. **DayColumn "Thêm" button** (line 332):
```tsx
// Change text-[9px] → text-xs
// Change p-0.5 → p-1
className="mt-1 w-full rounded border border-dashed border-slate-300 p-1 text-xs text-slate-400 hover:border-blue-400 hover:text-blue-500"
```

7. **SlotCard** (line 353):
```tsx
// Change p-1 → p-2
// Change text-[9px] → text-xs
<div className={`rounded border p-2 text-xs ${slot.status === "CLOSED" ? "border-slate-300 bg-slate-50" : "border-slate-200 bg-white"}`}>
```

8. **SlotCard time range** (line 355):
```tsx
// Change text-[9px] → text-sm
<span className="font-mono text-sm font-medium text-slate-700">
```

9. **SlotCard status badge** (line 358):
```tsx
// Change text-[8px] → text-[10px]
// Change px-1 py-0.5 → px-2 py-1
<span className={`shrink-0 rounded-full px-2 py-1 text-[10px] ${STATUS_STYLES[slot.status]}`}>
```

10. **SlotCard patient names** (line 367):
```tsx
// Change text-[9px] → text-xs
<div key={apt.appointmentId} className="text-blue-700 truncate text-xs" title={apt.reason ?? undefined}>
```

11. **SlotCard action buttons** (lines 376-396):
```tsx
// Change px-1 py-0.5 → px-2 py-1
// Change text-[9px] → text-xs
{slot.status === "BOOKED" && (
  <span className="flex-1 rounded border border-blue-200 bg-blue-50 px-2 py-1 text-center text-xs text-blue-600">
    Đã đặt
  </span>
)}
{slot.status === "OPEN" && (
  <button
    type="button"
    onClick={onClose}
    className="flex-1 rounded border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-600 hover:bg-red-100"
    title="Đóng"
  >
    Đóng ca
  </button>
)}
{slot.status === "CLOSED" && (
  <button
    type="button"
    onClick={onReopen}
    className="flex-1 rounded border border-green-200 bg-green-50 px-2 py-1 text-xs text-green-600 hover:bg-green-100"
  >
    Mở lại
  </button>
)}
```

---

### Task 5: Cleanup Unused Props

**File:** `adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

Remove unused `onEdit` prop from:
- WeekView component props interface (lines 237-252) - remove `onEdit`
- DayColumn component props interface (lines 279-296) - remove `onEdit`
- SlotCard component props interface (lines 341-350) - remove `onEdit`
- WeekView JSX call (line 269) - remove `onEdit` prop
- DayColumn JSX call (line 284) - remove `onEdit` prop
- SlotCard JSX call (line 322) - remove `onEdit` prop

---

### Task 6: Test and Verify

**Manual testing checklist:**

1. Navigate to `/schedule`
2. Verify week view displays with larger fonts and spacing
3. Click "Đóng ca" on an OPEN slot - should show custom modal (not browser confirm)
4. Click "Mở lại" on a CLOSED slot - should show custom modal
5. Click "Thêm khung giờ" - modal should still work
6. Verify no edit button appears on any slot
7. Verify patient names still display for booked slots

---

## Self-Review Checklist

- [ ] Edit feature completely removed (no onEdit references)
- [ ] ConfirmModal component added with danger/warning/info variants
- [ ] All browser `confirm()` calls replaced with ConfirmModal
- [ ] All font sizes increased (text-[9px] → text-xs, text-[10px] → text-[10px] stays, etc.)
- [ ] Spacing increased (gap-2 → gap-3, min-h-[200px] → min-h-[280px], p-1 → p-2)
- [ ] Button labels changed from "Sửa" / "✕" to "Đóng ca" / "Mở lại"
- [ ] Confirmation messages are clear and in Vietnamese
- [ ] No TypeScript errors (unused imports/variables removed)
- [ ] Component still functional (create, close, reopen work)

---

## Execution Options

**Plan complete and saved to `docs/superpowers/plans/2026-08-11-schedule-ui-refinement.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
