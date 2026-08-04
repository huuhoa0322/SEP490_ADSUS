using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface ICaseRepository
{
    /// <summary>Bản đầy đủ cho màn hình chi tiết (#23) và cho PDF (#27).</summary>
    Task<Case?> GetDetailAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Bản nhẹ, chỉ để kiểm tra tồn tại và trạng thái.</summary>
    Task<Case?> GetByIdAsync(Guid caseId, CancellationToken ct = default);

    Task<(IReadOnlyList<Case> Items, int TotalCount)> SearchByPatientAsync(
        Guid patientProfileId,
        CaseStatus? status,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Ghi ca bệnh và toàn bộ ảnh trong MỘT lần SaveChanges — không bao giờ tồn tại ca
    /// không có ảnh nào (UC-07 BR-02).
    /// </summary>
    Task<Case> CreateWithImagesAsync(
        Case newCase,
        IReadOnlyList<UltrasoundImage> images,
        CancellationToken ct = default);
}
