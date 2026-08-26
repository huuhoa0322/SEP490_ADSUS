namespace ADSUS_BE.BLL.Common.Interfaces;

public interface IFcmTokenService
{
    Task RegisterTokenAsync(Guid userId, string fcmToken, string deviceType = "android", CancellationToken ct = default);
    Task UnregisterTokenAsync(Guid userId, string fcmToken, CancellationToken ct = default);
    Task UnregisterAllTokensAsync(Guid userId, CancellationToken ct = default);
}
