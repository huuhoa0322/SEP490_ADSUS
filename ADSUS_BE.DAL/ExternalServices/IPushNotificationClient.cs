namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// Abstraction cho push notification (FCM/APNs). Implementation cụ thể wire trong Program.cs
/// — hiện tại là FakePush (in-memory), production sẽ swap sang FirebasePushNotificationClient.
///
/// Lý do cần abstraction (CLAUDE.md §3.1 GB-08):
/// 1. Handler KHÔNG gọi trực tiếp FirebaseAdmin SDK — tight coupling vào vendor
/// 2. Test phải chạy được mà KHÔNG cần FCM service account thật
/// 3. Production swap chỉ đổi 1 dòng DI registration
///
/// Payload PushMessage KHÔNG chứa PII y tế (chỉ metadata) — tránh lộ dữ liệu qua notification.
/// </summary>
public interface IPushNotificationClient
{
    /// <summary>
    /// Gửi 1 notification đến tất cả thiết bị active của user.
    /// </summary>
    /// <param name="userId">User nhận notification (FK tới users).</param>
    /// <param name="message">Payload notification.</param>
    /// <param name="ct">Cancellation token (production HTTP call).</param>
    /// <returns>Số device đã gửi thành công. FakePush trả 1 (giả lập).</returns>
    Task<int> SendToUserAsync(
        Guid userId,
        PushMessage message,
        CancellationToken ct = default);
}

/// <summary>Payload tối thiểu — không chứa thông tin y tế nhạy cảm.</summary>
/// <param name="Title">Tiêu đề hiển thị trên notification bar.</param>
/// <param name="Body">Nội dung ngắn (1-2 dòng).</param>
/// <param name="DeepLink">Route mở app khi user tap (VD: "/reminders/{id}").</param>
/// <param name="Data">Custom key-value metadata (VD: {"intake_id": "abc"}).</param>
public sealed record PushMessage(
    string Title,
    string Body,
    string? DeepLink = null,
    Dictionary<string, string>? Data = null);
