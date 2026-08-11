# Task 1 Brief: Remove Edit Feature

## Overview
Remove the edit slot functionality from `schedule-slot-management-view.tsx`. This includes:
- Removing the edit-related imports, state, mutation, and components
- Removing edit button from SlotCard
- Removing edit-related props from WeekView, DayColumn, SlotCard

## Target File
`adsus-fe/src/features/appointment-scheduling/components/schedule-slot-management-view.tsx`

## Specific Changes Required

### 1. Remove imports (around lines 14, 21)
- Remove `useUpdateScheduleSlot` from the hooks import
- Remove `UpdateScheduleSlotRequest` from the types import

### 2. Remove state (around line 50)
- Remove `editingSlot` state variable

### 3. Remove mutation (around line 65)
- Remove `updateMutation` declaration

### 4. Remove WeekView props
- Remove `onEdit` prop passing to WeekView

### 5. Remove EditSlotModal block (around lines 213-229)
- Remove the entire `{editingSlot && <EditSlotModal .../>}` block

### 6. Remove EditSlotModal component (around lines 462-497)
- Delete the entire `EditSlotModal` function

### 7. Remove onEdit from component props
- WeekView: remove `onEdit` from props interface and JSX
- DayColumn: remove `onEdit` from props interface and JSX
- SlotCard: remove `onEdit` from props interface and JSX

### 8. Remove edit button from SlotCard (around lines 382-388)
- The "Sửa" button currently shown for OPEN slots should be removed
- Keep only the close button (✕) for OPEN slots

## Global Constraints
- No external UI library changes - use existing Tailwind-only approach
- Preserve all existing API hooks - only remove UI elements, not API calls
- Maintain Vietnamese text for all labels and messages
- Keep slot creation feature unchanged

## Notes
- The update mutation hook `useUpdateScheduleSlot` is still used elsewhere in the codebase - do NOT remove from `use-schedule-slot.ts`
- Only remove the UI that calls this mutation
