using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
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
    private readonly PasswordResetService _sut;

    public PasswordResetServiceTests()
    {
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        _sut = new PasswordResetService(_users.Object, _email.Object);
    }

    // ---------- Đường tự phục vụ ----------

    [Fact]
    public async Task TuCapLai_DungSoDienThoaiVaEmail_DoiMatKhauVaGuiMail()
    {
        var user = TaoUser();
        var hashCu = user.PasswordHash;
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(YeuCau());

        Assert.NotEqual(hashCu, user.PasswordHash);
        // BR-04 — cấp lại xong là phải đổi ở lần đăng nhập kế tiếp (UC-25).
        Assert.True(user.MustChangePassword);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            "a@example.com", user.FullName, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TuCapLai_SoDienThoaiKhongTonTai_KHONG_LAM_GI()
    {
        // AF-01 — không tìm thấy tài khoản thì im lặng, không ghi gì, không gửi gì.
        SetupGetByPhone(null);

        await _sut.RequestSelfServiceResetAsync(YeuCau());

        VerifyKhongCoTacDongNao();
    }

    [Fact]
    public async Task TuCapLai_EmailKhongKhop_KHONG_LAM_GI()
    {
        // BR-01 — phải khớp CẢ hai. Chỉ cần biết số điện thoại mà đặt lại được mật khẩu của
        // người khác thì đó là lỗ hổng chiếm tài khoản.
        var user = TaoUser();
        SetupGetByPhone(user);

        var request = YeuCau();
        request.Email = "nguoikhac@example.com";

        await _sut.RequestSelfServiceResetAsync(request);

        VerifyKhongCoTacDongNao();
    }

    [Fact]
    public async Task TuCapLai_EmailKhacHoaThuong_VAN_KHOP()
    {
        // Người dùng gõ email in hoa là chuyện thường. DB cũng có unique index trên
        // lower(email) nên so sánh phải bỏ qua hoa thường.
        var user = TaoUser();
        SetupGetByPhone(user);

        var request = YeuCau();
        request.Email = "A@ExAmPlE.CoM";

        await _sut.RequestSelfServiceResetAsync(request);

        Assert.True(user.MustChangePassword);
    }

    [Theory]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Deactivated)]
    public async Task TuCapLai_TaiKhoanKhongConHieuLuc_KHONG_LAM_GI(UserStatus trangThai)
    {
        // AF-01 — tài khoản bị khoá hay vô hiệu hoá cũng không được cấp lại mật khẩu.
        var user = TaoUser();
        user.Status = trangThai;
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(YeuCau());

        VerifyKhongCoTacDongNao();
    }

    [Fact]
    public async Task TuCapLai_TaiKhoanChuaKhaiEmail_KHONG_LAM_GI()
    {
        var user = TaoUser();
        user.Email = null;
        SetupGetByPhone(user);

        await _sut.RequestSelfServiceResetAsync(YeuCau());

        VerifyKhongCoTacDongNao();
    }

    // ---------- Đường Admin cấp lại hộ (AF-02) ----------

    [Fact]
    public async Task AdminCapLai_ThanhCong_DoiMatKhauVaGuiMail()
    {
        var user = TaoUser();
        var hashCu = user.PasswordHash;
        SetupGetById(user);

        var result = await _sut.AdminResetAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result.Result);
        Assert.Null(result.TemporaryPassword);
        Assert.NotEqual(hashCu, user.PasswordHash);
        Assert.True(user.MustChangePassword);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            user.Email!, user.FullName, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminCapLai_TaiKhoanKhongCoEmail_VanCapLaiVaTraPlaintext()
    {
        // Quyết định ghi đè 06/08/2026 — không có email không còn bị chặn nữa. Mật khẩu vẫn
        // đổi thật, và giá trị plaintext được trả về đúng một lần để người thao tác đọc trực
        // tiếp cho chủ tài khoản (không còn chỗ nào để gửi thư).
        var user = TaoUser();
        user.Email = null;
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
    public async Task AdminCapLai_TaiKhoanDaVoHieuHoa_BiTuChoi()
    {
        var user = TaoUser();
        user.Status = UserStatus.Deactivated;
        SetupGetById(user);

        var result = await _sut.AdminResetAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.AccountIsDeactivated, result.Result);
    }

    [Fact]
    public async Task AdminCapLai_TaiKhoanKhongTonTai_TraVeNotFound()
    {
        SetupGetById(null);

        var result = await _sut.AdminResetAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(AccountOperationResult.NotFound, result.Result);
    }

    [Fact]
    public async Task AdminCapLai_ChoChinhMinh_BiTuChoi()
    {
        // Admin đổi mật khẩu của chính mình đã có UC-25, không cần đi vòng qua đây.
        var adminId = Guid.NewGuid();

        var result = await _sut.AdminResetAsync(adminId, adminId);

        Assert.Equal(AccountOperationResult.CannotTargetSelf, result.Result);
    }

    // ---------- Gửi thư hỏng thì KHÔNG được đổi mật khẩu ----------

    [Fact]
    public async Task AdminCapLai_GuiMailThatBai_GIU_NGUYEN_MAT_KHAU_CU()
    {
        // Thứ tự quan trọng: gửi thư trước, lưu sau.
        //
        // Làm ngược lại thì máy chủ mail trục trặc là mật khẩu cũ đã bị thay trong khi mật
        // khẩu mới không tới tay ai — chủ tài khoản bị nhốt ở ngoài đúng lúc đang cần vào,
        // mà chính người bấm nút cũng không biết là đã hỏng.
        var user = TaoUser();
        var hashCu = user.PasswordHash;
        SetupGetById(user);
        SetupGuiMailThatBai();

        var result = await _sut.AdminResetAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.EmailNotSent, result.Result);
        Assert.Equal(hashCu, user.PasswordHash);
        Assert.False(user.MustChangePassword);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TuCapLai_GuiMailThatBai_GIU_NGUYEN_MAT_KHAU_CU()
    {
        var user = TaoUser();
        var hashCu = user.PasswordHash;
        SetupGetByPhone(user);
        SetupGuiMailThatBai();

        await _sut.RequestSelfServiceResetAsync(YeuCau());

        Assert.Equal(hashCu, user.PasswordHash);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- helpers ----------

    private void SetupGuiMailThatBai() =>
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

    private static ForgotPasswordRequest YeuCau() => new()
    {
        PhoneNumber = "0912345678",
        Email = "a@example.com",
    };

    private static User TaoUser() => new()
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

    private void SetupGetByPhone(User? user) =>
        _users.Setup(r => r.GetByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

    private void SetupGetById(User? user) =>
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

    /// <summary>Không ghi database, không gửi mail — đúng nghĩa "im lặng" của AF-01.</summary>
    private void VerifyKhongCoTacDongNao()
    {
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
