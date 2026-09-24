using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho dịch vụ đã gắn vào ca khám (CaseClinicService — bảng nối Case ↔ ClinicService,
/// giữ giá tại thời điểm gắn). Mỗi dịch vụ gắn tối đa một lần vào một ca.
/// </summary>
public interface ICaseClinicServiceRepository
{
    /// <summary>Các dịch vụ của một ca kèm ClinicService, gắn trước lên trước. Chỉ đọc.</summary>
    Task<IReadOnlyList<CaseClinicService>> ListByCaseAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Dịch vụ <paramref name="clinicServiceId"/> đã gắn vào ca chưa, kèm ClinicService. Chỉ đọc.</summary>
    Task<CaseClinicService?> GetByCaseAndServiceAsync(Guid caseId, Guid clinicServiceId, CancellationToken ct = default);

    /// <summary>Một dịch vụ đã gắn kèm ClinicService — CÓ tracking (gỡ khỏi ca).</summary>
    Task<CaseClinicService?> GetForUpdateAsync(Guid caseClinicServiceId, CancellationToken ct = default);

    /// <summary>Thêm vào context, CHƯA lưu.</summary>
    Task AddAsync(CaseClinicService caseClinicService, CancellationToken ct = default);

    /// <summary>Đánh dấu xoá, CHƯA lưu.</summary>
    void Remove(CaseClinicService caseClinicService);
}
