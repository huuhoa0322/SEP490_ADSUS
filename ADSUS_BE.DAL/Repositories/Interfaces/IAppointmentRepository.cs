using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IAppointmentRepository
{
    Task<IReadOnlyList<Appointment>> ListBySlotAsync(Guid slotId, CancellationToken ct = default);
    Task<(IReadOnlyList<Appointment> Items, int Total)> ListBySlotPagedAsync(
        Guid slotId,
        int skip,
        int take,
        CancellationToken ct = default);
}