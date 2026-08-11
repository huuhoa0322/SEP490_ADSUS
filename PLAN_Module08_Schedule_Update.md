# Plan: Module 8 - Cập nhật Lịch khám

## Context

Module 8 (Appointment Scheduling) hiện tại có các vấn đề cần sửa theo yêu cầu mới:
- Lịch tự sinh chỉ T2-T6, cần **bao gồm T7-CN**
- Đóng ca **không thể mở lại** (Closed là terminal), cần **cho mở lại**
- Điều dưỡng có quyền quản lý lịch, cần **chỉ bác sĩ** được quản lý
- Hiện tại có 2 nút "Khôi phục tuần này" / "Khôi phục tháng" - cần **bỏ**
- Hiện tại hiện theo tuần/tháng, cần **chỉ hiện 3 tuần** với filter theo tuần + nút next

---

## Thay đổi Backend

### 1. BLL: `ScheduleSlotService.cs`

**Thay đổi 1: Cập nhật `EnsureDefaultSlotsAsync` — bao gồm T7-CN**

```csharp
// HIỆN TẠI (T2-T6):
for (var d = 0; d < 5; d++)

// MỚI (T2-CN, 7 ngày):
for (var d = 0; d < 7; d++)
```

**Thay đổi 2: Thêm method `ReopenSlotAsync`**

```csharp
public async Task<ScheduleSlotResponse> ReopenSlotAsync(Guid slotId, CancellationToken ct = default)
{
    var slot = await _repo.GetByIdForUpdateAsync(slotId, ct);
    if (slot is null)
        throw new InvalidOperationException($"Slot '{slotId}' not found.");
    
    if (slot.Status == SlotStatus.Open)
        throw new InvalidOperationException("Slot is already open.");
    
    slot.Status = SlotStatus.Open;
    slot.UpdatedAt = DateTime.UtcNow;
    await _repo.UpdateAsync(slot, ct);
    return MapToResponse(slot);
}
```

**Thay đổi 3: Cập nhật `ListSlotsAsync` — giới hạn mặc định 3 tuần**

```csharp
// HIỆN TẠI:
var to = toDate ?? from.AddDays(30);  // 30 ngày

// MỚI:
var to = toDate ?? from.AddDays(21);  // 3 tuần = 21 ngày
```

### 2. BLL: Interface `IScheduleSlotService.cs`

**Thêm method mới:**

```csharp
Task<ScheduleSlotResponse> ReopenSlotAsync(Guid slotId, CancellationToken ct = default);
```

### 3. Controller: `ScheduleSlotsController.cs`

**Thay đổi 1: Chỉ cho phép Doctor (bỏ Nurse)**

```csharp
// HIỆN TẠI:
[Authorize(Roles = "DOCTOR,NURSE")]

// MỚI:
[Authorize(Roles = "DOCTOR")]
```

**Thay đổi 2: Thêm endpoint `PUT /{id}/reopen`**

```csharp
[HttpPut("{id:guid}/reopen")]
[ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> Reopen(Guid id, CancellationToken ct = default)
{
    try
    {
        var existing = await _slots.GetSlotAsync(id, ct);
        if (existing is null)
            return NotFound(ApiResponse<object>.Fail(404, $"Slot '{id}' not found."));
        if (existing.DoctorId != CurrentDoctorId)
            return StatusCode(403, ApiResponse<object>.Fail(403, "Not your slot."));

        var slot = await _slots.ReopenSlotAsync(id, ct);
        return Ok(ApiResponse<ScheduleSlotResponse>.Ok(slot));
    }
    catch (InvalidOperationException ex)
    {
        if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(ApiResponse<object>.Fail(404, ex.Message));
        return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
    }
}
```

---

## Thay đổi Frontend

### 1. FE: `use-schedule-slot.ts` (hooks)

**Thêm hook mới:**

```typescript
export function useReopenScheduleSlot() {
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await apiClient.put<ApiResponse<ScheduleSlotResponse>>(
        `/schedule-slots/${id}/reopen`
      );
      return data.data;
    },
  });
}
```

### 2. FE: `schedule-slot-management-view.tsx`

**Thay đổi 1: Bỏ ViewToggle (Tuần/Tháng), chỉ dùng Week view**

```typescript
// BỎ hoàn toàn
const [viewMode, setViewMode] = useState<ViewMode>("week");

// Thay bằng fixed: luôn hiện 3 tuần
```

**Thay đổi 2: Cập nhật state - chỉ có `weekAnchor` thay vì `anchor`**

```typescript
const [weekAnchor, setWeekAnchor] = useState<Date>(() => mondayOfWeek(today));
```

**Thay đổi 3: Tính `fromDate`/`toDate` — luôn 3 tuần tính từ weekAnchor**

```typescript
const { fromDate, toDate, rangeLabel } = useMemo(() => {
  const start = mondayOfWeek(weekAnchor);
  const end = addDays(start, 20);  // 3 tuần - 1 ngày = 20 ngày
  return {
    fromDate: isoDate(start),
    toDate: isoDate(end),
    rangeLabel: `3 tuần từ ${isoDate(start)}`,
  };
}, [weekAnchor]);
```

**Thay đổi 4: Bỏ 2 nút "Khôi phục tuần này" / "Khôi phục tháng"**

```typescript
// BỎ hoàn toàn ensureDefaultMutation.mutate và ensureMonth
```

**Thay đổi 5: Bỏ MonthView, chỉ dùng WeekView nhưng render 3 tuần**

```typescript
// Thay WeekView để render 3 tuần thay vì 1 tuần
function WeekView({ weekStart, ... }) {
  return (
    <div className="grid grid-cols-7 gap-2">
      {Array.from({ length: 21 }).map((_, i) => {  // 21 ngày = 3 tuần
        const date = addDays(weekStart, i);
        // ...
      })}
    </div>
  );
}
```

**Thay đổi 6: Thêm nút Reopen cho slot CLOSED**

```typescript
// Trong SlotCard và SlotChip:
{slot.status === "CLOSED" && (
  <button
    type="button"
    onClick={onReopen}
    className="rounded border border-green-300 bg-green-50 px-2 py-0.5 text-green-700 hover:bg-green-100"
  >
    Mở lại
  </button>
)}
```

**Thay đổi 7: Cập nhật onReopen handler**

```typescript
const reopenMutation = useReopenScheduleSlot();

const onReopen = async (s: ScheduleSlotResponse) => {
  if (!confirm(`Mở lại khung giờ ${s.startTime.slice(0,5)}–${s.endTime.slice(0,5)} ngày ${s.slotDate}?`)) return;
  try {
    await reopenMutation.mutateAsync(s.slotId);
  } catch (err) {
    toast.error(getApiErrorMessage(err, "Không mở lại được khung giờ."));
  }
};
```

**Thay đổi 8: Bỏ toggle Week/Month, chỉ còn nút Prev/Next để chuyển tuần**

```typescript
const goPrev = () => setWeekAnchor(addDays(weekAnchor, -7));  // lùi 1 tuần
const goNext = () => setWeekAnchor(addDays(weekAnchor, 7));   // tiến 1 tuần
```

**Thay đổi 9: Cập nhật UI header**

```typescript
// Header mới:
<header className="flex items-center justify-between">
  <div>
    <h1 className="text-2xl font-semibold">Quản lý lịch khám</h1>
    <p className="text-sm text-slate-500">
      Hệ thống tự sinh ca mặc định T2-CN (8h-12h, 13h-17h).
    </p>
  </div>
  <button
    type="button"
    onClick={() => {
      setDefaultDate(todayIso);
      setShowCreate(true);
    }}
    className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
  >
    <Plus className="h-4 w-4" /> Thêm khung giờ
  </button>
</header>
```

---

## Tóm tắt thay đổi

| # | File | Thay đổi |
|---|------|-----------|
| 1 | `ScheduleSlotService.cs` | +7 ngày (T2-CN), thêm `ReopenSlotAsync`, giới hạn 21 ngày |
| 2 | `IScheduleSlotService.cs` | + `ReopenSlotAsync` |
| 3 | `ScheduleSlotsController.cs` | `[Authorize(Roles = "DOCTOR")]`, + `PUT /{id}/reopen` |
| 4 | `use-schedule-slot.ts` | + `useReopenScheduleSlot` |
| 5 | `schedule-slot-management-view.tsx` | Bỏ Month, 3 tuần, bỏ khôi phục, + reopen |

---

## Verification

1. **Build Backend**: `dotnet build ADSUS_BE/ADSUS_BE.slnx`
2. **Build Frontend**: `npm run build` trong `adsus-fe/`
3. **Test Manual**:
   - Đăng nhập Nurse → không thấy trang Schedule (403)
   - Đăng nhập Doctor → thấy 3 tuần (21 ngày T2-CN)
   - Đóng 1 slot → thấy nút "Mở lại"
   - Bấm "Mở lại" → slot chuyển sang OPEN
   - Bấm Next → hiện 3 tuần tiếp theo
   - Không còn nút "Khôi phục tuần/tháng"
