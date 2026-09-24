using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// P11 review (Feature 4, 29/08/2026): tách ra từ việc CaseDiagnosisService/AiMetricsService
/// gọi thẳng AppDbContext.AiPredictions — chỉ 2 method thực sự cần, không CRUD thừa.
/// </summary>
public interface IAiPredictionRepository
{
    Task<IReadOnlyList<AiPrediction>> ListByModelVersionAsync(
        Guid modelVersionId, CancellationToken ct = default);

    Task AddRangeAsync(IReadOnlyList<AiPrediction> predictions, CancellationToken ct = default);

    /// <summary>Mọi dự đoán của một ảnh (kể cả bản ghi "0 hộp" Confidence = 0) — CÓ tracking (xác nhận lại ảnh).</summary>
    Task<IReadOnlyList<AiPrediction>> ListByImageForUpdateAsync(Guid imageId, CancellationToken ct = default);

    /// <summary>Đánh dấu xoá, CHƯA lưu.</summary>
    void RemoveRange(IEnumerable<AiPrediction> predictions);
}
