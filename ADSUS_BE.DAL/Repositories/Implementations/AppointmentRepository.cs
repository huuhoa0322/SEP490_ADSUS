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

    public async Task<Appointment?> GetByIdAsync(Guid appointmentId, CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
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
}
