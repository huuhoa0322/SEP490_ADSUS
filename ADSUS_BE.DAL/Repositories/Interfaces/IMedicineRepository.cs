using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho Medicine catalog (danh mục thuốc dùng chung). Bác sĩ tra cứu /
/// tự thêm khi kê đơn. KHÔNG Remove (GB-03) — medicine cũ giữ cho audit.
/// </summary>
public interface IMedicineRepository
{
    /// <summary>Lấy 1 medicine theo ID.</summary>
    Task<Medicine?> GetByIdAsync(Guid medicineId, CancellationToken ct = default);

    /// <summary>Tìm theo tên chính xác (case-insensitive). Trả null nếu chưa có.</summary>
    Task<Medicine?> FindByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Add 1 medicine vào catalog.</summary>
    Task AddAsync(Medicine medicine, CancellationToken ct = default);

    /// <summary>Lấy toàn bộ danh mục thuốc (dùng cho bác sĩ khi kê đơn).</summary>
    Task<IReadOnlyList<Medicine>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Tìm kiếm danh mục thuốc (dùng cho tính năng autocomplete).</summary>
    Task<IReadOnlyList<Medicine>> SearchByNameAsync(string keyword, int limit = 20, CancellationToken ct = default);
}
