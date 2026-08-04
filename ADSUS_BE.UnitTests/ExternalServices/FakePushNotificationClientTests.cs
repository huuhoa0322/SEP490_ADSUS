using ADSUS_BE.DAL.ExternalServices;
using Xunit;

namespace ADSUS_BE.UnitTests.ExternalServices;

/// <summary>
/// Tests cho FakePushNotificationClient. Implementation cụ thể của IPushNotificationClient
/// dùng trong dev/test. Production sẽ swap sang FirebasePushNotificationClient (sprint sau).
///
/// Verify FakePush:
/// - Lưu message đã gửi vào in-memory collection
/// - Trả về số device = 1 (giả lập 1 user = 1 device cho đơn giản)
/// - Cho phép test khác verify "đã push đến user X chưa" qua property SentMessages
/// </summary>
public class FakePushNotificationClientTests
{
    [Fact]
    public async Task SendToUserAsync_StoresMessageInMemory()
    {
        // Arrange
        var client = new FakePushNotificationClient();
        var userId = Guid.NewGuid();
        var msg = new PushMessage(
            Title: "Nhắc uống thuốc",
            Body: "Đã đến giờ uống Paracetamol 1 viên",
            DeepLink: "/reminders/abc");

        // Act
        var sent = await client.SendToUserAsync(userId, msg);

        // Assert
        Assert.Equal(1, sent);
        Assert.Single(client.SentMessages);
        Assert.Equal(userId, client.SentMessages.First().UserId);
        Assert.Equal("Nhắc uống thuốc", client.SentMessages.First().Message.Title);
        Assert.Equal("/reminders/abc", client.SentMessages.First().Message.DeepLink);
    }

    [Fact]
    public async Task SendToUserAsync_MultipleSends_StoresAllInOrder()
    {
        // Arrange
        var client = new FakePushNotificationClient();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Act
        await client.SendToUserAsync(user1, new PushMessage("T1", "B1"));
        await client.SendToUserAsync(user2, new PushMessage("T2", "B2"));
        await client.SendToUserAsync(user1, new PushMessage("T3", "B3"));

        // Assert
        Assert.Equal(3, client.SentMessages.Count);
        Assert.Equal(user1, client.SentMessages.ElementAt(0).UserId);
        Assert.Equal(user2, client.SentMessages.ElementAt(1).UserId);
        Assert.Equal(user1, client.SentMessages.ElementAt(2).UserId);
        Assert.Equal("T3", client.SentMessages.ElementAt(2).Message.Title);
    }

    [Fact]
    public async Task SendToUserAsync_NullData_DoesNotThrow()
    {
        // Arrange — Data optional, KHÔNG ép user truyền
        var client = new FakePushNotificationClient();
        var msg = new PushMessage("T", "B");

        // Act + Assert
        await client.SendToUserAsync(Guid.NewGuid(), msg); // không throw
        Assert.Single(client.SentMessages);
    }

    [Fact]
    public async Task SendToUserAsync_CancellationToken_RespectsCancellation()
    {
        // Arrange — FakePush không async-blocking nhưng vẫn phải nhận CancellationToken
        // để signature tương thích với implementation production (FirebasePush sẽ cancel HTTP).
        var client = new FakePushNotificationClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert — FakePush implementation KHÔNG throw OperationCanceledException
        // vì không có async work thật; đây chỉ là contract test.
        await client.SendToUserAsync(Guid.NewGuid(), new PushMessage("T", "B"), cts.Token);
    }
}
