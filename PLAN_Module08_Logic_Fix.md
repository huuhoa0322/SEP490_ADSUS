# Plan: Module 8 - Sửa Logic Auto-sinh Lịch & Reopen

## Context

Module 8 đang có 3 vấn đề sau khi user test:

### Vấn đề 1: Auto-sinh lịch KHÔNG hoạt động
- Test thấy BE không tự sinh ca cho 3 tuần tới khi bác sĩ mở trang
- Logic hiện tại trong `ListSlotsAsync` có bug nghiêm trọng

### Vấn đề 2: Qua ngày mới không sinh thêm ca
- Yêu cầu: ngày 10 → sinh đến ngày 31; ngày 11 → sinh thêm ca cho ngày 1 tháng sau
- Logic hiện tại chỉ check `hasFutureSlots` 1 lần → sai

### Vấn đề 3: Reopen chưa hoạt động
- Test thấy không mở lại được slot
- Code backend đã có sẵn, cần kiểm tra flow

---

## Phân tích Bug Chi Tiết

### Bug A: `ListSlotsAsync` không thực sự sinh slot

**Code hiện tại (BUG):**
```csharp
public async Task<(IReadOnlyList<ScheduleSlotResponse> Items, int TotalCount)> ListSlotsAsync(
    DateOnly? fromDate = null, DateOnly? toDate = null, Guid? doctorId = null, ...)
{
    var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var to = toDate ?? from.AddDays(21);
    
    // Auto-sinh
    var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var targetEndDate = today.AddDays(21);
    var existingSlots = await _repo.ListByRangeAsync(today, targetEndDate, doctorId, null, ct);
    var hasFutureSlots = existingSlots.Any(s => s.SlotDate >= tomorrow);
    
    if (!hasFutureSlots)
    {
        await EnsureUpcomingSlotsAsync(doctorId ?? Guid.Empty, today, targetEndDate, ct);
        // � BUG: doctorId có thể là Guid.Empty → throw exception
    }
    
    var slots = await _repo.ListByRangeAsync(from, to, doctorId, statusFilter, ct);
    return ...
}
```

**Bug phát hiện:**
1. `doctorId` từ Controller LUÔN là `CurrentDoctorId` (không null), nên `?? Guid.Empty` không gây throw
2. NHƯNG `hasFutureSlots = true` sau lần sinh đầu tiên → không sinh thêm cho những ngày tiếp theo

**Fix:**
- Đổi logic: luôn kiểm tra **từng ngày** xem đã có slot chưa, nếu thiếu ngày nào thì sinh ngày đó
- Dùng vòng lặp for qua 21 ngày tới, mỗi ngày check `HasOverlapAsync` → nếu không có slot OPEN nào → tự sinh 2 ca mặc định

### Bug B: Ngày 11 không sinh thêm ca cho ngày 1 tháng sau

Yêu cầu rõ ràng: 
- Ngày 10 sinh đến ngày 31 (21 ngày)
- Sang ngày 11, sinh thêm cho ngày 1 tháng sau

**Code hiện tại:**
```csharp
var targetEndDate = today.AddDays(21); // fixed = today + 21
```

**Vấn đề:** Khi sang ngày 11, `today = 11`, `targetEndDate = 11 + 21 = 32` (= ngày 1 tháng sau). Nhưng check `hasFutureSlots` chỉ check có slot future không → đã có → skip.

**Fix:** Mỗi lần load, duyệt qua từng ngày từ `tomorrow` đến `today + 21`, với mỗi ngày check xem có slot OPEN trong 2 ca mặc định không → nếu thiếu thì sinh.

### Bug C: Reopen không hoạt động

**Code backend đã đầy đủ:**
- Service: `ReopenSlotAsync` ✓
- Controller: `PUT /{id}/reopen` ✓
- Hook: `useReopenScheduleSlot` ✓
- API: `reopenScheduleSlot` ✓
- UI: nút "Mở lại" cho status CLOSED ✓

**Nghi vấn:** UI có nhưng có thể không hiển thị vì:
1. Backend trả `status` là số (enum 0/1) thay vì string → `normalizeStatus` chuyển đúng nhưng có thể có edge case
2. Slot chưa thực sự được set CLOSED (logic CloseSlotAsync đang chạy đúng)

**Cần kiểm tra:** Xác nhận user đã test đóng slot trước khi test reopen, và status thực sự là CLOSED.

**Fix (nếu cần):** Đảm bảo logic close set status đúng (đã đúng trong code), và test lại flow end-to-end.

---

## Thay đổi Cần Làm

### 1. Backend: Sửa logic auto-sinh lịch trong `ListSlotsAsync`

**File:** `ADSUS_BE.BLL/AppointmentScheduling/Services/ScheduleSlotService.cs`

**Code mới:**
```csharp
public async Task<(IReadOnlyList<ScheduleSlotResponse> Items, int TotalCount)> ListSlotsAsync(
    DateOnly? fromDate = null, DateOnly? toDate = null, Guid? doctorId = null, 
    SlotStatus? statusFilter = null, int page = 1, int pageSize = 20,
    CancellationToken ct = default)
{
    if (!doctorId.HasValue || doctorId.Value == Guid.Empty)
        throw new InvalidOperationException("doctorId is required.");

    var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
    var to = toDate ?? from.AddDays(21);

    if (to < from)
        throw new InvalidOperationException("toDate must not be before fromDate.");

    // Tự động sinh slot cho 21 ngày tới từ hôm nay
    await EnsureUpcomingSlotsAsync(doctorId.Value, ct);

    var slots = await _repo.ListByRangeAsync(from, to, doctorId, statusFilter, ct);
    var items = slots.Select(MapToResponse).ToList();
    return (items, items.Count);
}

public async Task EnsureUpcomingSlotsAsync(
    Guid doctorId, CancellationToken ct = default)
{
    if (doctorId == Guid.Empty)
        throw new InvalidOperationException("doctorId is required.");

    var doctor = await _userRepo.GetByIdAsync(doctorId, ct);
    if (doctor is null || doctor.Role != UserRole.Doctor)
        throw new InvalidOperationException($"User '{doctorId}' is not a valid Doctor.");

    var now = DateTime.UtcNow;
    var today = DateOnly.FromDateTime(now);
    var targetEndDate = today.AddDays(20); // hôm nay + 20 = 21 ngày (index 0..20)

    var newSlots = new List<ScheduleSlot>();

    // Duyệt qua từng ngày từ hôm nay đến hôm nay+20
    for (var day = today; day <= targetEndDate; day = day.AddDays(1))
    {
        // Với mỗi range mặc định (8h-12h, 13h-17h)
        foreach (var (start, end) in DefaultRanges)
        {
            // Skip ca trong quá khứ
            var startDateTime = day.ToDateTime(start, DateTimeKind.Utc);
            if (startDateTime <= now) continue;

            // Check overlap với slot OPEN hiện có
            var hasOverlap = await _repo.HasOverlapAsync(
                doctorId, day, start, end,
                excludeSlotId: null, ct);
            if (hasOverlap) continue; // Đã có slot trong range này

            newSlots.Add(new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = doctorId,
                SlotDate = day,
                StartTime = start,
                EndTime = end,
                Status = SlotStatus.Open,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }

    foreach (var s in newSlots)
    {
        try
        {
            await _repo.AddAsync(s, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx
            && pgEx.SqlState == "23505")
        {
            // Unique constraint violated → đã có slot (idempotent)
        }
    }
}
```

**Logic mới:**
- Mỗi lần `ListSlotsAsync` được gọi → `EnsureUpcomingSlotsAsync`
- Duyệt từng ngày từ `today` đến `today + 20` (21 ngày)
- Với mỗi ngày, với mỗi ca mặc định → check `HasOverlapAsync` (so với slot OPEN hiện có)
- Nếu không có → sinh slot mới

**Giải thích yêu cầu "ngày 10 sinh đến 31, ngày 11 sinh thêm ngày 1":**
- Ngày 10 (hôm nay): today = 10, loop 10..30 → sinh các ca còn thiếu
- Ngày 11 (sau khi qua ngày): today = 11, loop 11..31 → ngày 11 đã có ca từ hôm qua, ngày 12-31 có rồi, ngày 1 tháng sau (day = today+21) chưa có → sinh thêm

**Đáp ứng yêu cầu:** ✅

### 2. Backend: Cập nhật Interface

**File:** `ADSUS_BE.BLL/AppointmentScheduling/Interfaces/IScheduleSlotService.cs`

Đổi signature:
```csharp
// CŨ:
Task EnsureUpcomingSlotsAsync(
    Guid doctorId,
    DateOnly fromDate,
    DateOnly toDate,
    CancellationToken ct = default);

// MỚI:
Task EnsureUpcomingSlotsAsync(
    Guid doctorId,
    CancellationToken ct = default);
```

### 3. Backend: Sửa `EnsureDefaultSlotsAsync` cho nhất quán

Có thể giữ nguyên hoặc bỏ, vì logic mới đã cover. Sẽ giữ để không phá vỡ API cũ (`POST /ensure-default`).

### 4. Frontend: Đảm bảo Reopen hoạt động

UI đã có nút "Mở lại" cho slot CLOSED. Cần:
- Verify status CLOSED thực sự hiển thị
- Có thể thêm toast notification khi reopen thành công

### 5. Frontend: Bỏ logic cũ liên quan

UI không còn gọi `ensureDefault` nữa (đã bỏ từ trư�c). Tốt.

---

## Tóm Tắt Thay Đổi

| # | File | Thay đổi |
|---|------|-----------|
| 1 | `ScheduleSlotService.cs` | Sửa `ListSlotsAsync` + viết lại `EnsureUpcomingSlotsAsync` |
| 2 | `IScheduleSlotService.cs` | Đổi signature `EnsureUpcomingSlotsAsync` |

---

## Verification

1. **Build Backend**: `dotnet build ADSUS_BE/ADSUS_BE.slnx`
2. **Test Manual**:
   - Ngày 10: Login Doctor → load page → BE tự sinh ca cho ngày 10..30
   - Reload: vẫn ngày 10 → không sinh thêm (đã có hết)
   - Sang ngày 11: Login Doctor → load page → BE sinh thêm ca cho ngày 1 tháng sau
   - Đóng 1 slot → status = CLOSED → thấy nút "Mở lại"
   - Click "Mở lại" → status = OPEN
