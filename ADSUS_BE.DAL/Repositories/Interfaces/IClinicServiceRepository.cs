using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho danh mục dịch vụ phòng khám (Khám thường, Khám siêu âm...). Không xoá cứng:
/// ngừng cung cấp = IsActive false.
/// </summary>
public interface IClinicServiceRepository
{
    /// <summary>Danh mục dịch vụ, lọc theo trạng thái nếu có, xếp theo mã. Chỉ đọc.</summary>
    Task<IReadOnlyList<ClinicService>> ListAsync(bool? isActive, CancellationToken ct = default);

    /// <summary>Một dịch vụ theo Id. Chỉ đọc.</summary>
    Task<ClinicService?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Dịch vụ đang hoạt động có mã cho trước (khớp chính xác). Chỉ đọc.</summary>
    Task<ClinicService?> FindActiveByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Đọc để sửa — CÓ tracking.</summary>
    Task<ClinicService?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đã có dịch vụ mang mã này chưa (không phân biệt hoa thường). <paramref name="upperCode"/> đã Trim + ToUpper.</summary>
    Task<bool> CodeExistsAsync(string upperCode, CancellationToken ct = default);

    /// <summary>Thêm vào context, CHƯA lưu.</summary>
    Task AddAsync(ClinicService service, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
