using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>Repository cho danh mục đơn vị tính thuốc (viên, vỉ, hộp, chai...).</summary>
public interface IMedicineUnitRepository
{
    /// <summary>Toàn bộ đơn vị tính, xếp theo tên. Chỉ đọc.</summary>
    Task<IReadOnlyList<MedicineUnit>> ListAsync(CancellationToken ct = default);
}
