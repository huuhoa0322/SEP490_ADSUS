using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho PrescriptionItem (mỗi dòng thuốc trong 1 đơn). ScheduleSlots
/// của job không persist ở đây — chỉ là tham số runtime cho IntakeLogGenerationService.
/// </summary>
public interface IPrescriptionItemRepository
{
    /// <summary>Lấy 1 item theo ID (kèm medicine + parent prescription + intake logs).</summary>
    Task<PrescriptionItem?> GetByIdAsync(Guid prescriptionItemId, CancellationToken ct = default);

    /// <summary>Lấy tất cả items thuộc 1 đơn thuốc.</summary>
    Task<IReadOnlyList<PrescriptionItem>> ListByPrescriptionAsync(Guid prescriptionId, CancellationToken ct = default);

    /// <summary>Add 1 item vào change tracker.</summary>
    Task AddAsync(PrescriptionItem item, CancellationToken ct = default);

    /// <summary>Add nhiều items cùng lúc (dùng cho CreatePrescriptionCommandHandler).</summary>
    Task AddRangeAsync(IEnumerable<PrescriptionItem> items, CancellationToken ct = default);
}
