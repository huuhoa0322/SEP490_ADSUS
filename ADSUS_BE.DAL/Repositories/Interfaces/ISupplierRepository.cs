using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho nhà cung cấp thuốc (Module PrescriptionAdherence — quản lý kho).
/// Không xoá cứng: ngừng hợp tác = IsActive false.
/// </summary>
public interface ISupplierRepository
{
    /// <summary>
    /// Danh sách phân trang, mới tạo trước. <paramref name="search"/> khớp một phần tên (không
    /// phân biệt hoa thường), số điện thoại hoặc mã số thuế. Chỉ đọc.
    /// </summary>
    Task<(IReadOnlyList<Supplier> Items, int TotalCount)> SearchPagedAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Đọc để hiển thị. Chỉ đọc.</summary>
    Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken ct = default);

    /// <summary>Đọc để sửa — CÓ tracking.</summary>
    Task<Supplier?> GetForUpdateAsync(Guid supplierId, CancellationToken ct = default);

    /// <summary>
    /// Nhà cung cấp đầu tiên (khác <paramref name="excludeSupplierId"/>) trùng tên hoặc email
    /// (không phân biệt hoa thường), trùng số điện thoại, hoặc trùng mã số thuế khi
    /// <paramref name="taxCode"/> khác null. Giá trị truyền vào phải đã Trim. Chỉ đọc.
    /// </summary>
    Task<Supplier?> FindDuplicateAsync(
        string name,
        string phoneNumber,
        string email,
        string? taxCode,
        Guid? excludeSupplierId,
        CancellationToken ct = default);

    /// <summary>Thêm vào context, CHƯA lưu.</summary>
    Task AddAsync(Supplier supplier, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
