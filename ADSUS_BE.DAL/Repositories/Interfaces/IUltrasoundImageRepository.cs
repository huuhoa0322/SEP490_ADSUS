using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IUltrasoundImageRepository
{
    Task<IReadOnlyList<UltrasoundImage>> ListByCaseAsync(Guid caseId, CancellationToken ct = default);

    Task AddRangeAsync(IReadOnlyList<UltrasoundImage> images, CancellationToken ct = default);
}
