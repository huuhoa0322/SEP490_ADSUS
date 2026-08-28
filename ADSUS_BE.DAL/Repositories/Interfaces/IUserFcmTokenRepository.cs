using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IUserFcmTokenRepository
{
    Task UpsertAsync(Guid userId, string token, string deviceType, CancellationToken ct = default);
    Task DeleteByTokenAsync(string token, CancellationToken ct = default);
    Task DeleteAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
