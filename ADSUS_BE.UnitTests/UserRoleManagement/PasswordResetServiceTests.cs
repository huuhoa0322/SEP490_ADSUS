using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

/// <summary>
/// UC-03 FT-06 — cấp lại mật khẩu.
///
/// Luật khó nhất ở đây là AF-01: sai số điện thoại, sai email, hay tài khoản đã bị khoá đều
/// phải im lặng y hệt nhau. Không kiểm được bằng cách nhìn thông báo trả về (vì chúng giống
/// nhau), nên phải kiểm bằng HÀNH VI: có ghi database không, có gửi mail không.
/// </summary>
public class PasswordResetServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly PasswordResetService _sut;

    /// <summary>Các dòng nhật ký đã được xếp vào hàng chờ trong bài test.</summary>
    private readonly List<AuditLog> _audited = new();

    public PasswordResetServiceTests()
    {
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        _auditLogs.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
                  .Callback<AuditLog, CancellationToken>((l, _) => _audited.Add(l))
                  .Returns(Task.CompletedTask);

        // RequestSelfServiceResetAsync (28/08/2026) chạy phần gửi thư + đổi mật khẩu ở 1 scope
        // DI riêng (IServiceScopeFactory), để không đụng vào AppDbContext của scope request đã
        // bị dispose. Dựng 1 ServiceProvider thật (không mock tay IServiceProvider) đăng ký
        // đúng các mock/instance đang dùng ở test này, để CreateScope() trả về đúng chúng.
        var services = new ServiceCollection();
        services.AddSingleton(_users.Object);
        services.AddSingleton(_email.Object);
        services.AddSingleton(new AccountAuditTrail(_auditLogs.Object));
        var provider = services.BuildServiceProvider();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        _sut = new PasswordResetService(
            _users.Object,
            _email.Object,
            new AccountAuditTrail(_auditLogs.Object),
            scopeFactory.Object,
            new Mock<ILogger<PasswordResetService>>().Object,
            // Chạy việc "nền" NGAY tại chỗ thay vì Task.Run — nếu không, mọi assertion đọc
            // trạng thái ngay sau `await _sut.RequestSelfServiceResetAsync(...)` bên dưới sẽ
            // thành race condition (có lúc qua có lúc trượt), vì việc thật sẽ chạy trên 1
            // thread khác, không đồng bộ với awaiter của bài test.
            dispatchBackground: work => work());
    }

    // ---------- Đường tự phục vụ ----------

    [Fact]
    public async Task RequestSelfServiceResetAsync_MatchingPhoneAndEmail_ChangesPasswordAndSendsEmail()
    {
        var user = BuildUser();
        var hashCu = user.PasswordHash;
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        Assert.NotEqual(hashCu, user.PasswordHash);
        // BR-04 — cấp lại xong là phải đổi ở lần đăng nhập kế tiếp (UC-25).
        Assert.True(user.MustChangePassword);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            "a@example.com", user.FullName, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_PhoneNotFound_DoesNothing()
    {
        // AF-01 — không tìm thấy tài khoản thì im lặng, không ghi gì, không gửi gì.
        SetupGetByPhone(null);

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        VerifyNoSideEffects();
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_EmailDoesNotMatch_DoesNothing()
    {
        // BR-01 — phải khớp CẢ hai. Chỉ cần biết số điện thoại mà đặt lại được mật khẩu của
        // người khác thì đó là lỗ hổng chiếm tài khoản.
        var user = BuildUser();
        SetupGetByPhone(user);

        var request = BuildRequest();
        request.Email = "nguoikhac@example.com";

        await _sut.RequestSelfServiceResetAsync(request);

        VerifyNoSideEffects();
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_EmailDiffersOnlyByCase_StillMatches()
    {
        // Người dùng gõ email in hoa là chuyện thường. DB cũng có unique index trên
        // lower(email) nên so sánh phải bỏ qua hoa thường.
        var user = BuildUser();
        SetupGetByPhone(user);

        var request = BuildRequest();
        request.Email = "A@ExAmPlE.CoM";

        await _sut.RequestSelfServiceResetAsync(request);

        Assert.True(user.MustChangePassword);
    }

    [Theory]
    [InlineData(UserStatus.Deactivated)]
    public async Task RequestSelfServiceResetAsync_AccountNotActive_DoesNothing(UserStatus trangThai)
    {
        // AF-01 — tài khoản bị vô hiệu hoá cũng không được cấp lại mật khẩu.
        var user = BuildUser();
        user.Status = trangThai;
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        VerifyNoSideEffects();
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_AccountHasNoEmail_DoesNothing()
    {
        var user = BuildUser();
        user.Email = null;
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        VerifyNoSideEffects();
    }

    // ---------- Đường Admin cấp lại hộ (AF-02) ----------

    [Theory]
    [InlineData("a@example.com")]
    [InlineData(null)]
    public async Task AdminResetAsync_Success_AlwaysReturnsPlaintextAndNeverEmails(string? email)
    {
        // Quyết định ghi đè 06/08/2026, mở rộng lần 2 — không còn phân biệt có/không có email
        // nữa: cả hai trường hợp đều đổi mật khẩu thật và trả plaintext đúng một lần để người
        // thao tác đọc trực tiếp cho chủ tài khoản, KHÔNG BAO GIỜ gửi email ở đường này nữa.
        var user = BuildUser();
        user.Email = email;
        var hashCu = user.PasswordHash;
        SetupGetById(user);

        var result = await _sut.AdminResetAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result.Result);
        Assert.NotNull(result.TemporaryPassword);
        Assert.NotEqual(hashCu, user.PasswordHash);
        Assert.True(user.MustChangePassword);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AdminResetAsync_AccountDeactivated_IsRejected()
    {
        var user = BuildUser();
        user.Status = UserStatus.Deactivated;
        SetupGetById(user);

        var result = await _sut.AdminResetAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.AccountIsDeactivated, result.Result);
    }

    [Fact]
    public async Task AdminResetAsync_AccountNotFound_ReturnsNotFound()
    {
        SetupGetById(null);

        var result = await _sut.AdminResetAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(AccountOperationResult.NotFound, result.Result);
    }

    [Fact]
    public async Task AdminResetAsync_TargetIsSelf_IsRejected()
    {
        // Admin đổi mật khẩu của chính mình đã có UC-25, không cần đi vòng qua đây.
        var adminId = Guid.NewGuid();

        var result = await _sut.AdminResetAsync(adminId, adminId);

        Assert.Equal(AccountOperationResult.CannotTargetSelf, result.Result);
    }

    // ---------- Gửi thư hỏng thì KHÔNG được đổi mật khẩu (chỉ còn áp dụng cho đường tự phục vụ —
    // AdminResetAsync không còn gửi email nữa kể từ 06/08/2026 mở rộng lần 2, xem Theory phía trên) ----------

    [Fact]
    public async Task RequestSelfServiceResetAsync_EmailSendFails_KeepsOldPassword()
    {
        var user = BuildUser();
        var hashCu = user.PasswordHash;
        SetupGetByPhone(user);
        SetupEmailSendFails();

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        Assert.Equal(hashCu, user.PasswordHash);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Nhật ký thao tác ----------

    [Fact]
    public async Task AdminResetAsync_RecordsAuditLogWithAdminAsActor()
    {
        var user = BuildUser();
        var adminId = Guid.NewGuid();
        SetupGetById(user);

        await _sut.AdminResetAsync(user.UserId, adminId);

        var log = Assert.Single(_audited);
        Assert.Equal("ADMIN_RESET_PASSWORD", log.Action);
        Assert.Equal(adminId, log.ActorId);
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_RecordsAuditLogWithAccountHolderAsActor()
    {
        var user = BuildUser();
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        var log = Assert.Single(_audited);
        Assert.Equal("SELF_RESET_PASSWORD", log.Action);
        Assert.Equal(user.UserId, log.ActorId);
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_NonMatchingInfo_DoesNotWriteAuditLog()
    {
        // AF-01 — không khớp thì im lặng hoàn toàn. Ghi nhật ký ở đây là biến bảng nhật ký
        // thành chỗ dò xem số điện thoại nào có tài khoản thật.
        SetupGetByPhone(null);

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        Assert.Empty(_audited);
    }

    [Fact]
    public async Task RequestSelfServiceResetAsync_EmailSendFails_DoesNotWriteAuditLog()
    {
        // Sửa 12/08/2026 — test này trước đây gọi nhầm AdminResetAsync, đường đó không còn
        // gửi email từ 06/08/2026 (mở rộng lần 2) nên kịch bản "gửi thư hỏng" không thể xảy
        // ra ở đó nữa (xem chú thích ở RequestSelfServiceResetAsync_EmailSendFails_KeepsOldPassword phía
        // trên) — chỉ còn áp dụng cho đường tự phục vụ. Mật khẩu không đổi thì cũng không có
        // việc gì đã xảy ra để mà ghi.
        var user = BuildUser();
        SetupGetByPhone(user);
        SetupEmailSendFails();

        await _sut.RequestSelfServiceResetAsync(BuildRequest());

        Assert.Empty(_audited);
    }

    // ---------- helpers ----------

    private void SetupEmailSendFails() =>
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

    private static ForgotPasswordRequest BuildRequest() => new()
    {
        PhoneNumber = "0912345678",
        Email = "a@example.com",
    };

    private static User BuildUser() => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0912345678",
        FullName = "Nguyễn Văn A",
        Email = "a@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa123456@"),
        Role = UserRole.Patient,
        Status = UserStatus.Active,
        MustChangePassword = false,
    };

    private void SetupGetByPhone(User? user)
    {
        _users.Setup(r => r.GetByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        // Phần "nền" của RequestSelfServiceResetAsync (28/08/2026) đọc lại đúng user này bằng
        // GetForUpdateAsync (scope DI riêng, xem PasswordResetService) rồi mới sửa-rồi-lưu —
        // trả về CÙNG một instance để các assertion đọc thẳng trên biến `user` ở từng test vẫn
        // đúng, không phải dựng lại một bản sao.
        if (user is not null)
        {
            _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(user);
        }
    }

    // Chỉ dùng cho AdminResetAsync (sửa-rồi-lưu) — GetForUpdateAsync (P11 review Module 2, 12/08/2026).
    private void SetupGetById(User? user) =>
        _users.Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

    /// <summary>Không ghi database, không gửi mail — đúng nghĩa "im lặng" của AF-01.</summary>
    private void VerifyNoSideEffects()
    {
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
