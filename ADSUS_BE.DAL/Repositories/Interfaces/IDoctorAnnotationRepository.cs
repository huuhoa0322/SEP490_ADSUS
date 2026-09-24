using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// P11 review (Feature 4, 29/08/2026): tách ra từ việc CaseDiagnosisService/AiMetricsService
/// gọi thẳng AppDbContext.DoctorAnnotations — chỉ 2 method thực sự cần, không CRUD thừa.
/// </summary>
public interface IDoctorAnnotationRepository
{
    Task<IReadOnlyList<DoctorAnnotation>> ListByImageIdsAsync(
        IReadOnlyList<Guid> imageIds, CancellationToken ct = default);

    Task AddRangeAsync(IReadOnlyList<DoctorAnnotation> annotations, CancellationToken ct = default);

    /// <summary>Mọi chú thích của một ảnh — CÓ tracking (xác nhận lại ảnh).</summary>
    Task<IReadOnlyList<DoctorAnnotation>> ListByImageForUpdateAsync(Guid imageId, CancellationToken ct = default);

    /// <summary>Đánh dấu xoá, CHƯA lưu.</summary>
    void RemoveRange(IEnumerable<DoctorAnnotation> annotations);
}
