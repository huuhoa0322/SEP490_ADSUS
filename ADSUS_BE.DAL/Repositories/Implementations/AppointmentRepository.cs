using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IAppointmentRepository (Module 8 — UC-13, UC-14).
/// Read-only queries dùng AsNoTracking.
/// </summary>
public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _db;

    public AppointmentRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Appointment>> ListByPatientAsync(
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Where(a => a.PatientProfileId == patientProfileId)
            .OrderByDescending(a => a.Slot.SlotDate)
            .ThenByDescending(a => a.Slot.StartTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Appointment>> ListByDoctorAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.Slot.DoctorId == doctorId
                && a.Slot.SlotDate >= fromDate
                && a.Slot.SlotDate <= toDate)
            .OrderBy(a => a.Slot.SlotDate)
                .ThenBy(a => a.Slot.StartTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Appointment>> ListBookedStartingBetweenAsync(
        DateTime fromLocal,
        DateTime toLocal,
        CancellationToken ct = default)
    {
        var fromDate = DateOnly.FromDateTime(fromLocal);
        var fromTime = TimeOnly.FromDateTime(fromLocal);
        var toDate = DateOnly.FromDateTime(toLocal);
        var toTime = TimeOnly.FromDateTime(toLocal);

        // So sánh (SlotDate, StartTime) theo thứ tự từ điển — cửa sổ có thể vắt qua nửa đêm.
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
            .Where(a => a.Status == AppointmentStatus.Booked
                && (a.Slot.SlotDate > fromDate
                    || (a.Slot.SlotDate == fromDate && a.Slot.StartTime >= fromTime))
                && (a.Slot.SlotDate < toDate
                    || (a.Slot.SlotDate == toDate && a.Slot.StartTime <= toTime)))
            .OrderBy(a => a.Slot.SlotDate)
                .ThenBy(a => a.Slot.StartTime)
            .ToListAsync(ct);
    }

    public async Task<Appointment?> GetByIdAsync(Guid appointmentId, CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(a => a.BookedByUser)
            .Include(a => a.PatientRelationship)
            .Include(a => a.Case)
                .ThenInclude(c => c.CaseSymptoms)
                    .ThenInclude(cs => cs.Category)
            .Include(a => a.Case)
                .ThenInclude(c => c.CaseSymptoms)
                    .ThenInclude(cs => cs.Symptom)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);
    }

    public async Task<Appointment> CreateAsync(Appointment appointment, CancellationToken ct = default)
    {
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);
        return appointment;
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken ct = default)
    {
        _db.Appointments.Update(appointment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Appointment>> ListForPatientOrBookerAsync(
        Guid patientProfileId,
        Guid? bookedByUserId,
        AppointmentStatus? statusFilter,
        CancellationToken ct = default)
    {
        var query = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(a => a.BookedByUser)
            .Include(a => a.PatientRelationship)
            .Where(a => a.PatientProfileId == patientProfileId
                || (bookedByUserId != null && a.BookedByUserId == bookedByUserId));

        if (statusFilter.HasValue)
        {
            query = query.Where(a => a.Status == statusFilter.Value);
        }

        return await query
            .OrderByDescending(a => a.Slot.SlotDate)
            .ThenByDescending(a => a.Slot.StartTime)
            .ToListAsync(ct);
    }

    public Task<int> CountUserCancellationsSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken ct = default) =>
        _db.Appointments
            .CountAsync(a => (a.BookedByUserId == userId || (a.BookedByUserId == null && a.PatientProfile != null && a.PatientProfile.UserId == userId))
                && a.Status == AppointmentStatus.Cancelled
                && a.UpdatedAt >= sinceUtc
                && (a.CancelledReason == null || (!a.CancelledReason.StartsWith("Đổi lịch:") && !a.CancelledReason.StartsWith("Bác sĩ nghỉ phép"))), ct);

    public async Task<CheckinQueuePage> GetCheckinQueuePageAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string? search,
        AppointmentStatus? statusFilter,
        bool excludeCancelled,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseQuery = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.Slot.SlotDate >= fromDate && a.Slot.SlotDate <= toDate);

        // Tìm kiếm ở mức EF Core query: FullName, Phone, DoctorName, Reason
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            baseQuery = baseQuery.Where(a =>
                (a.PatientProfile != null && a.PatientProfile.User != null && a.PatientProfile.User.FullName.ToLower().Contains(term)) ||
                (a.PatientProfile != null && a.PatientProfile.FullName != null && a.PatientProfile.FullName.ToLower().Contains(term)) ||
                (a.PatientProfile != null && a.PatientProfile.User != null && a.PatientProfile.User.Phone != null && a.PatientProfile.User.Phone.Contains(term)) ||
                (a.PatientProfile != null && a.PatientProfile.Phone != null && a.PatientProfile.Phone.Contains(term)) ||
                (a.Slot != null && a.Slot.Doctor != null && a.Slot.Doctor.FullName.ToLower().Contains(term)) ||
                (a.Reason != null && a.Reason.ToLower().Contains(term)));
        }

        // Đếm số lượng theo trạng thái trên toàn bộ mốc thời gian đã chọn (không phụ thuộc phân trang)
        var bookedCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.Booked, ct);
        var checkedInCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.Completed, ct);
        var cancelledCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.Cancelled, ct);
        var noShowCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.NoShow, ct);

        var query = baseQuery;
        if (statusFilter.HasValue)
        {
            query = query.Where(a => a.Status == statusFilter.Value);
        }
        else if (excludeCancelled)
        {
            query = query.Where(a => a.Status != AppointmentStatus.Cancelled);
        }

        var totalCount = await query.CountAsync(ct);

        // Sắp xếp: Đang chờ check-in (Booked = 0) lên đầu -> Đã check-in (Completed = 1) ở giữa -> Vắng mặt (NoShow = 2) -> Đã hủy (Cancelled = 3) ở cuối.
        // Trong cùng mỗi nhóm: sắp xếp tăng dần theo thời gian (SlotDate, StartTime).
        var items = await query
            .OrderBy(a => a.Status == AppointmentStatus.Booked ? 0
                        : (a.Status == AppointmentStatus.Completed ? 1
                        : (a.Status == AppointmentStatus.NoShow ? 2 : 3)))
            .ThenBy(a => a.Slot.SlotDate)
            .ThenBy(a => a.Slot.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new CheckinQueuePage(items, totalCount, bookedCount, checkedInCount, cancelledCount, noShowCount);
    }

    public Task<Appointment?> GetNextForDoctorOnDateAsync(Guid doctorId, DateOnly date, CancellationToken ct = default) =>
        _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
            .Include(a => a.PatientProfile).ThenInclude(p => p.User)
            .Include(a => a.Case)
            .Where(a => a.Slot.DoctorId == doctorId
                && a.Slot.SlotDate == date
                && (a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Booked)
                && (a.Case == null || (a.Case.Status != CaseStatus.End && a.Case.Status != CaseStatus.Confirmed)))
            .OrderBy(a => a.Slot.StartTime)
            .FirstOrDefaultAsync(ct);

    public Task<Appointment?> GetByIdForUpdateAsync(Guid appointmentId, CancellationToken ct = default) =>
        _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

    public Task<Appointment?> GetByIdWithDetailsForUpdateAsync(Guid appointmentId, CancellationToken ct = default) =>
        _db.Appointments
            .Include(a => a.Slot).ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile).ThenInclude(p => p.User)
            .Include(a => a.BookedByUser)
            .Include(a => a.PatientRelationship)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

    public Task<Appointment?> GetLatestBookedByCaseForUpdateAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.CaseId == caseId && a.Status == AppointmentStatus.Booked, ct);

    public Task<Appointment?> GetLatestByCaseAsync(Guid caseId, CancellationToken ct = default) =>
        _db.Appointments
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.CaseId == caseId, ct);

    public Task<Appointment?> GetForRescheduleAsync(Guid appointmentId, CancellationToken ct = default) =>
        _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(a => a.Case)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

    public Task<Appointment?> GetWithBookerAndRelationshipAsync(Guid appointmentId, CancellationToken ct = default) =>
        _db.Appointments
            .AsNoTracking()
            .Include(a => a.BookedByUser)
            .Include(a => a.PatientRelationship)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

    public Task<Appointment?> GetFirstOnDateAsync(
        Guid patientProfileId,
        DateOnly slotDate,
        IReadOnlyCollection<AppointmentStatus> statuses,
        Guid? excludeAppointmentId,
        CancellationToken ct = default) =>
        _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot).ThenInclude(s => s.Doctor)
            .FirstOrDefaultAsync(a =>
                a.PatientProfileId == patientProfileId
                && (excludeAppointmentId == null || a.AppointmentId != excludeAppointmentId)
                && a.Slot.SlotDate == slotDate
                && statuses.Contains(a.Status),
                ct);

    public Task<int> CountBookedByProfileAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.Appointments.CountAsync(a => a.PatientProfileId == patientProfileId
            && a.Status == AppointmentStatus.Booked, ct);

    public Task<int> CountSelfBookedByProfileAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.Appointments.CountAsync(a => a.PatientProfileId == patientProfileId
            && a.BookedByUserId == null
            && a.RelationshipId == null
            && a.Status == AppointmentStatus.Booked, ct);

    public Task<int> CountBookedForOthersByUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.Appointments.CountAsync(a => a.BookedByUserId == userId
            && a.RelationshipId != null
            && a.Status == AppointmentStatus.Booked, ct);

    public Task<int> CountProfileCancellationsSinceAsync(Guid patientProfileId, DateTime sinceUtc, CancellationToken ct = default) =>
        _db.Appointments.CountAsync(a => a.PatientProfileId == patientProfileId
            && a.Status == AppointmentStatus.Cancelled
            && a.UpdatedAt >= sinceUtc
            && (a.CancelledReason == null || (!a.CancelledReason.StartsWith("Đổi lịch:") && !a.CancelledReason.StartsWith("Bác sĩ nghỉ phép"))), ct);

    public Task<bool> ExistsActiveInSlotAsync(Guid patientProfileId, Guid slotId, CancellationToken ct = default) =>
        _db.Appointments.AnyAsync(a =>
            a.PatientProfileId == patientProfileId &&
            a.SlotId == slotId &&
            a.Status != AppointmentStatus.Cancelled &&
            a.Status != AppointmentStatus.NoShow, ct);

    public async Task AddAsync(Appointment appointment, CancellationToken ct = default) =>
        await _db.Appointments.AddAsync(appointment, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task CancelByCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var appointments = await _db.Appointments
            .Where(a => a.CaseId == caseId && a.Status == AppointmentStatus.Booked)
            .ToListAsync(ct);

        foreach (var appointment in appointments)
        {
            appointment.Status = AppointmentStatus.Cancelled;
        }

        if (appointments.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }
}
