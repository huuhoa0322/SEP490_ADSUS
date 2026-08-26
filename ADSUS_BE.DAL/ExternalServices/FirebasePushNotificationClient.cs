using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// Firebase Cloud Messaging implementation của IPushNotificationClient.
/// Sử dụng Firebase Admin SDK để gửi push notification đến thiết bị.
///
/// Sử dụng IUserFcmTokenRepository để query FCM tokens từ database.
/// </summary>
public sealed class FirebasePushNotificationClient : IPushNotificationClient, IDisposable
{
    private readonly ILogger<FirebasePushNotificationClient> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _serviceAccountPath;

    public FirebasePushNotificationClient(
        IConfiguration configuration,
        ILogger<FirebasePushNotificationClient> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        _serviceAccountPath = configuration["Firebase:ServiceAccountPath"]
            ?? throw new InvalidOperationException("Firebase:ServiceAccountPath not configured in appsettings or User Secrets.");

        InitializeFirebaseApp();
    }

    private void InitializeFirebaseApp()
    {
        if (FirebaseApp.DefaultInstance != null)
        {
            _logger.LogInformation("[FCM] Firebase App already initialized");
            return;
        }

        try
        {
#pragma warning disable CS0618 // GoogleCredential.FromFile is deprecated but still works
            var credential = GoogleCredential.FromFile(_serviceAccountPath);
#pragma warning restore CS0618
            FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
            });

            _logger.LogInformation("[FCM] Firebase App initialized successfully with service account");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FCM] Failed to initialize Firebase App. Path={Path}", _serviceAccountPath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> SendToUserAsync(
        Guid userId,
        PushMessage message,
        CancellationToken ct = default)
    {
        // Query FCM tokens từ database
        var tokens = await GetUserFcmTokensAsync(userId, ct);

        if (tokens.Count == 0)
        {
            _logger.LogWarning(
                "[FCM] No FCM tokens found for user {UserId}. Cannot send notification.",
                userId);
            return 0;
        }

        _logger.LogInformation(
            "[FCM] Found {TokenCount} FCM token(s) for user {UserId}",
            tokens.Count, userId);

        var successCount = 0;

        foreach (var token in tokens)
        {
            try
            {
                await SendToSingleDeviceAsync(token, message, ct);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[FCM] Failed to send to token {TokenId}. Will not remove token.",
                    token);
                // Không xóa token khi gửi fail (có thể do network tạm thời)
            }
        }

        _logger.LogInformation(
            "[FCM] SendToUser completed. UserId={UserId}, TotalTokens={Total}, Success={Success}",
            userId, tokens.Count, successCount);

        return successCount;
    }

    private async Task<IReadOnlyList<string>> GetUserFcmTokensAsync(Guid userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.UserFcmTokens
            .Where(t => t.UserId == userId)
            .Select(t => t.Token)
            .ToListAsync(ct);
    }

    private async Task SendToSingleDeviceAsync(string fcmToken, PushMessage message, CancellationToken ct)
    {
#pragma warning disable CS0618 // Message.Token is deprecated but still required for device targeting
        var firebaseMessage = new Message
        {
            Notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = message.Title,
                Body = message.Body,
            },
            Token = fcmToken,
            Data = message.Data ?? new Dictionary<string, string>(),
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification
                {
                    ChannelId = "default",
                    Icon = "ic_notification",
                    Color = "#4CAF50",
                }
            },
            Apns = new ApnsConfig
            {
                Aps = new Aps
                {
                    Alert = new ApsAlert
                    {
                        Title = message.Title,
                        Body = message.Body,
                    },
                    ContentAvailable = true,
                }
            }
        };
#pragma warning restore CS0618

        var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(firebaseMessage, ct);

        _logger.LogInformation(
            "[FCM] Message sent successfully. MessageId={MessageId}, TokenLength={TokenLength}",
            messageId, fcmToken.Length);
    }

    public void Dispose()
    {
        // FirebaseApp.DefaultInstance không cần dispose thủ công
        // Nó được quản lý bởi FirebaseAdmin SDK
    }
}
