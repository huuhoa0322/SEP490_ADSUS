using System.Collections.Concurrent;

namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// Fake implementation cho IPushNotificationClient — chỉ dùng trong dev/test/CI.
/// Lưu message vào in-memory ConcurrentBag để test verify "đã push đến user X chưa".
///
/// KHÔNG dùng ở production — wire <see cref="FirebasePushNotificationClient"/> thay thế
/// (sẽ viết ở sprint sau khi có FCM service account JSON).
///
/// Trả về số device = 1 (giả lập 1 user có 1 device active). Production implementation sẽ
/// query user_notification_tokens để đếm số device thật + gửi lần lượt.
/// </summary>
public sealed class FakePushNotificationClient : IPushNotificationClient
{
    private readonly ConcurrentQueue<(Guid UserId, PushMessage Message)> _sent = new();

    /// <summary>Test helper — expose danh sách message đã gửi (KHÔNG dùng ở production).</summary>
    public IReadOnlyList<(Guid UserId, PushMessage Message)> SentMessages =>
        _sent.ToArray();

    public Task<int> SendToUserAsync(
        Guid userId,
        PushMessage message,
        CancellationToken ct = default)
    {
        // FakePush không có async work thật, nhưng giữ signature async để production
        // implementation (FirebasePush) có thể swap vào mà không đổi handler.
        // ct không được check vì không có gì để cancel — đây là stub, không phải HTTP call.
        _sent.Enqueue((userId, message));
        return Task.FromResult(1);
    }
}
