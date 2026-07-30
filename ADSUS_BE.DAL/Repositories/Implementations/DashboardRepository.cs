using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// UC-05 FT-10 — đếm số liệu cho màn thống kê.
///
/// Mọi truy vấn đều là đếm thuần. Không truy vấn nào lấy về tên, số điện thoại hay bản ghi
/// bệnh nhân (BR-01) — dữ liệu cá nhân không rời khỏi database, nên không có đường nào lọt
/// lên màn hình Admin dù có lỗi lập trình ở tầng trên.
/// </summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public DashboardRepository(AppDbContext db) => _db = db;

    public async Task<AccountCounts> GetAccountCountsAsync(
        CancellationToken cancellationToken = default)
    {
        // Gom về MỘT truy vấn nhóm theo (vai trò, trạng thái) rồi cộng ở bộ nhớ, thay vì
        // bắn 8 lệnh COUNT riêng. Bảng users nhỏ nên chênh lệch không lớn, nhưng 8 vòng đi
        // về tới Supabase ở Singapore thì độ trễ mạng cộng dồn thấy rõ.
        var groups = await _db.Users
            .AsNoTracking()
            .GroupBy(u => new { u.Role, u.Status })
            .Select(g => new { g.Key.Role, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int ByRole(UserRole role) => groups.Where(g => g.Role == role).Sum(g => g.Count);
        int ByStatus(UserStatus status) => groups.Where(g => g.Status == status).Sum(g => g.Count);

        return new AccountCounts(
            Total: groups.Sum(g => g.Count),
            AdminCount: ByRole(UserRole.Admin),
            DoctorCount: ByRole(UserRole.Doctor),
            NurseCount: ByRole(UserRole.Nurse),
            PatientCount: ByRole(UserRole.Patient),
            ActiveCount: ByStatus(UserStatus.Active),
            LockedCount: ByStatus(UserStatus.Locked),
            DeactivatedCount: ByStatus(UserStatus.Deactivated));
    }

    public async Task<ActivityCounts> GetActivityCountsAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        // Lọc theo CreatedAt của từng bảng. Cases dùng VisitDate vì đó mới là ngày khám thật;
        // CreatedAt chỉ là lúc bấm lưu, có thể lệch ngày nếu bác sĩ nhập bù hôm sau.
        var fromDate = DateOnly.FromDateTime(fromInclusive);
        var toDate = DateOnly.FromDateTime(toExclusive);

        var newAccounts = await _db.Users.AsNoTracking()
            .CountAsync(u => u.CreatedAt >= fromInclusive && u.CreatedAt < toExclusive, cancellationToken);

        var caseCount = await _db.Cases.AsNoTracking()
            .CountAsync(c => c.VisitDate >= fromDate && c.VisitDate < toDate, cancellationToken);

        var aiGroups = await _db.AiResults.AsNoTracking()
            .Where(r => r.CreatedAt >= fromInclusive && r.CreatedAt < toExclusive)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Ai(AiResultStatus status) => aiGroups.FirstOrDefault(g => g.Status == status)?.Count ?? 0;

        // Lọc lịch hẹn theo NGÀY KHÁM (SlotDate), không theo ngày đặt.
        //
        // Phải cùng mốc với khung giờ bên dưới, nếu không "số lượt đặt trung bình mỗi khung
        // giờ" là đem hai đại lượng khác mốc chia cho nhau. Thực tế gặp phải: khung giờ nằm
        // ở tương lai nên rơi hết ra ngoài khoảng 30 ngày vừa qua, ra 10 lượt đặt trên 0
        // khung giờ.
        //
        // Đọc theo ngày khám cũng đúng nghĩa hơn với Admin: "kỳ này phòng khám có bao nhiêu
        // lượt hẹn" chứ không phải "bao nhiêu lượt được bấm đặt".
        var appointmentGroups = await _db.Appointments.AsNoTracking()
            .Where(a => a.Slot.SlotDate >= fromDate && a.Slot.SlotDate < toDate)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Appointments(AppointmentStatus status) =>
            appointmentGroups.FirstOrDefault(g => g.Status == status)?.Count ?? 0;

        var slotCount = await _db.ScheduleSlots.AsNoTracking()
            .CountAsync(s => s.SlotDate >= fromDate && s.SlotDate < toDate, cancellationToken);

        // Tuân thủ uống thuốc: đếm theo giờ ĐƯỢC HẸN, không phải giờ xác nhận. Liều hẹn hôm
        // qua mà sáng nay mới bấm xác nhận vẫn phải tính vào hôm qua, nếu không tỉ lệ tuân
        // thủ của mọi ngày đều sai.
        //
        // Trạng thái chỉ có Pending → Taken (Glossary UCS), nên "đã uống" tương đương
        // ConfirmedAt khác null — không cần tới enum intake_status.
        var doseCount = await _db.MedicationIntakeLogs.AsNoTracking()
            .CountAsync(l => l.ScheduledTime >= fromInclusive && l.ScheduledTime < toExclusive,
                cancellationToken);

        var takenCount = await _db.MedicationIntakeLogs.AsNoTracking()
            .CountAsync(l => l.ScheduledTime >= fromInclusive
                             && l.ScheduledTime < toExclusive
                             && l.ConfirmedAt != null,
                cancellationToken);

        return new ActivityCounts(
            NewAccounts: newAccounts,
            CaseCount: caseCount,
            AiRunCount: aiGroups.Sum(g => g.Count),
            AiConfirmedCount: Ai(AiResultStatus.Confirmed),
            AiRejectedCount: Ai(AiResultStatus.Rejected),
            AiPendingCount: Ai(AiResultStatus.PendingReview),
            AppointmentBookedCount: Appointments(AppointmentStatus.Booked),
            AppointmentCancelledCount: Appointments(AppointmentStatus.Cancelled),
            ScheduleSlotCount: slotCount,
            MedicationDoseCount: doseCount,
            MedicationTakenCount: takenCount);
    }
}
