using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IUltrasoundImageRepository
{
    Task<IReadOnlyList<UltrasoundImage>> ListByCaseAsync(Guid caseId, CancellationToken ct = default);

    Task AddRangeAsync(IReadOnlyList<UltrasoundImage> images, CancellationToken ct = default);

    /// <summary>Một ảnh theo Id — CÓ tracking (cập nhật ảnh khi xác nhận lại).</summary>
    Task<UltrasoundImage?> GetForUpdateAsync(Guid imageId, CancellationToken ct = default);

    /// <summary>Ca đã có ít nhất một ảnh siêu âm chưa.</summary>
    Task<bool> ExistsForCaseAsync(Guid caseId, CancellationToken ct = default);
}
