using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IScheduleSlotRepository
{
    Task<ScheduleSlot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleSlot>> SearchAsync(
        Guid? doctorId,
        DateOnly? slotDate,
        SlotStatus? status,
        int skip,
        int take,
        CancellationToken ct = default);
    Task<int> CountAsync(
        Guid? doctorId,
        DateOnly? slotDate,
        SlotStatus? status,
        CancellationToken ct = default);

    /// <summary>BR-01 + DB enforce ex_schedule_slots_no_overlap.</summary>
    Task<bool> HasOverlappingAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? excludeSlotId,
        CancellationToken ct = default);

    Task AddAsync(ScheduleSlot slot, CancellationToken ct = default);
}