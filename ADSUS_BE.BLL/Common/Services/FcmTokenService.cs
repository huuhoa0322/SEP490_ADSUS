using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.Common.Services;

public sealed class FcmTokenService : IFcmTokenService
{
    private readonly IUserFcmTokenRepository _tokenRepository;
    private readonly ILogger<FcmTokenService> _logger;

    public FcmTokenService(
        IUserFcmTokenRepository tokenRepository,
        ILogger<FcmTokenService> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public async Task RegisterTokenAsync(
        Guid userId,
        string fcmToken,
        string deviceType = "android",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            _logger.LogWarning("[FCM] Empty token provided for user {UserId}", userId);
            return;
        }

        await _tokenRepository.UpsertAsync(userId, fcmToken, deviceType, ct);
        _logger.LogInformation("[FCM] Registered token for user {UserId}, device: {DeviceType}", userId, deviceType);
    }

    public async Task UnregisterTokenAsync(
        Guid userId,
        string fcmToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            _logger.LogWarning("[FCM] Empty token provided for unregister by user {UserId}", userId);
            return;
        }

        await _tokenRepository.DeleteByTokenAsync(fcmToken, ct);
        _logger.LogInformation("[FCM] Unregistered token for user {UserId}", userId);
    }

    public async Task UnregisterAllTokensAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        await _tokenRepository.DeleteAllByUserIdAsync(userId, ct);
        _logger.LogInformation("[FCM] Unregistered all tokens for user {UserId}", userId);
    }
}
