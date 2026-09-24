using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho quy cách đóng gói thuốc (hộp/vỉ/viên... kèm hệ số quy đổi và giá bán).
/// Mỗi thuốc có đúng một quy cách "đơn vị cơ sở" (IsBaseUnit, hệ số 1).
/// </summary>
public interface IMedicinePackagingRepository
{
    /// <summary>Tên đơn vị cơ sở của từng thuốc (MedicineId → tên đơn vị). Chỉ đọc.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetBaseUnitNamesAsync(
        IReadOnlyCollection<Guid> medicineIds,
        CancellationToken ct = default);

    /// <summary>Tên đơn vị cơ sở của một thuốc, null nếu chưa cấu hình. Chỉ đọc.</summary>
    Task<string?> GetBaseUnitNameAsync(Guid medicineId, CancellationToken ct = default);

    /// <summary>Mọi quy cách của một thuốc kèm đơn vị tính — đơn vị cơ sở trước, rồi hệ số tăng dần. Chỉ đọc.</summary>
    Task<IReadOnlyList<MedicinePackaging>> ListByMedicineAsync(Guid medicineId, CancellationToken ct = default);

    /// <summary>Một quy cách kèm đơn vị tính. Chỉ đọc.</summary>
    Task<MedicinePackaging?> GetWithUnitAsync(Guid packagingId, CancellationToken ct = default);

    /// <summary>Đọc để sửa/xoá — CÓ tracking.</summary>
    Task<MedicinePackaging?> GetForUpdateAsync(Guid packagingId, CancellationToken ct = default);

    /// <summary>Thuốc đã có quy cách (khác <paramref name="excludePackagingId"/>) dùng đơn vị tính này chưa.</summary>
    Task<bool> ExistsForUnitAsync(
        Guid medicineId,
        Guid medicineUnitId,
        Guid? excludePackagingId,
        CancellationToken ct = default);

    /// <summary>Quy cách đã xuất hiện trong giao dịch kho nào chưa (BR-019: đã dùng thì không được xoá).</summary>
    Task<bool> IsUsedInInventoryAsync(Guid packagingId, CancellationToken ct = default);

    /// <summary>Thêm vào context, CHƯA lưu.</summary>
    Task AddAsync(MedicinePackaging packaging, CancellationToken ct = default);

    /// <summary>Đánh dấu xoá, CHƯA lưu.</summary>
    void Remove(MedicinePackaging packaging);

    Task SaveChangesAsync(CancellationToken ct = default);
}
