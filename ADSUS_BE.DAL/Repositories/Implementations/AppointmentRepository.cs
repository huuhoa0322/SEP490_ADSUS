using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _db;

    public AppointmentRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Appointment>> ListBySlotAsync(
        Guid slotId, CancellationToken ct = default)
    {
        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.SlotId == slotId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Appointment> Items, int Total)> ListBySlotPagedAsync(
        Guid slotId, int skip, int take, CancellationToken ct = default)
    {
        var baseQuery = _db.Appointments.AsNoTracking()
            .Where(a => a.SlotId == slotId);
        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .OrderBy(a => a.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }
}