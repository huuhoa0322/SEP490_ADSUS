using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// UC-10 cập nhật hồ sơ cá nhân, và UC-02 bật/tắt sinh trắc học.
///
/// Hai luật quan trọng nhất được khẳng định ở đây:
/// BR-02 — số điện thoại KHÔNG BAO GIỜ bị đổi qua đường này.
/// BR-03 — không dữ liệu y tế nào bị chạm tới.
/// </summary>
public class ProfileServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _sut = new ProfileService(_users.Object);
    }

    [Fact]
    public async Task LayHoSo_TraVeDayDuThongTin()
    {
        var user = TaoUser();
        user.DateOfBirth = new DateOnly(1990, 5, 20);
        SetupUser(user);

        var result = await _sut.GetOwnProfileAsync(user.UserId);

        Assert.NotNull(result);
        Assert.Equal("Nguyễn Văn A", result!.FullName);
        Assert.Equal("0912345678", result.PhoneNumber);
        Assert.Equal("1990-05-20", result.DateOfBirth);
        Assert.Equal("PATIENT", result.Role);
    }

    [Fact]
    public async Task LayHoSo_TaiKhoanKhongTonTai_TraVeNull()
    {
        SetupUser(null);

        var result = await _sut.GetOwnProfileAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task LayHoSo_TraVeCoEpDoiMatKhau()
    {
        // UC-25: đăng nhập bằng vân tay không đi qua /auth/login nên không nhận được cờ này
        // từ LoginResponse — nó phải có trong hồ sơ. Thiếu thì Admin cấp lại mật khẩu cho
        // tài khoản đã bật vân tay, người dùng quét vân tay là vào thẳng, bỏ qua màn đổi.
        var user = TaoUser();
        user.MustChangePassword = true;
        SetupUser(user);

        var result = await _sut.GetOwnProfileAsync(user.UserId);

        Assert.True(result!.MustChangePassword);
    }

    [Theory]
    [InlineData(UserStatus.Deactivated)]
    [InlineData(UserStatus.Deactivated)]
    public async Task LayHoSo_TaiKhoanKhongConHieuLuc_TraVeNull(UserStatus trangThai)
    {
        // UC-02 AF-02: quét vân tay đúng nhưng tài khoản đã bị Admin khoá thì vẫn không vào
        // được. Ứng dụng di động dựa vào chính lời gọi GET /users/me này để kiểm tra.
        //
        // Trả null y hệt trường hợp không tìm thấy tài khoản (GB-06) — controller vì thế
        // trả về đúng một câu 401 cho cả hai.
        var user = TaoUser();
        user.Status = trangThai;
        SetupUser(user);

        var result = await _sut.GetOwnProfileAsync(user.UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task CapNhat_ThanhCong_LuuDungBaTruong()
    {
        var user = TaoUser();
        SetupUser(user);
        SetupEmailFree();

        var result = await _sut.UpdateOwnProfileAsync(user.UserId, new UpdateProfileRequest
        {
            FullName = "Tên Mới",
            Email = "moi@example.com",
            DateOfBirth = "1995-03-15",
        });

        Assert.Equal(ProfileOperationResult.Success, result);
        Assert.Equal("Tên Mới", user.FullName);
        Assert.Equal("moi@example.com", user.Email);
        Assert.Equal(new DateOnly(1995, 3, 15), user.DateOfBirth);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CapNhat_KHONG_DOI_SoDienThoai()
    {
        // BR-02: số điện thoại là định danh đăng nhập, chỉ phòng khám đổi được.
        var user = TaoUser();
        var soCu = user.Phone;
        SetupUser(user);
        SetupEmailFree();

        await _sut.UpdateOwnProfileAsync(user.UserId, new UpdateProfileRequest
        {
            FullName = "Tên Mới",
        });

        Assert.Equal(soCu, user.Phone);
    }

    [Fact]
    public async Task CapNhat_KHONG_CHAM_DuLieuKhac()
    {
        // BR-03: chỉ sửa thông tin hành chính. Vai trò, trạng thái, mật khẩu phải nguyên vẹn.
        var user = TaoUser();
        var hashCu = user.PasswordHash;
        SetupUser(user);
        SetupEmailFree();

        await _sut.UpdateOwnProfileAsync(user.UserId, new UpdateProfileRequest
        {
            FullName = "Tên Mới",
        });

        Assert.Equal(UserRole.Patient, user.Role);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(hashCu, user.PasswordHash);
    }

    [Fact]
    public async Task CapNhat_EmailDeTrong_LuuThanhNull()
    {
        var user = TaoUser();
        user.Email = "cu@example.com";
        SetupUser(user);

        var result = await _sut.UpdateOwnProfileAsync(user.UserId, new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            Email = "   ",
        });

        Assert.Equal(ProfileOperationResult.Success, result);
        Assert.Null(user.Email);
    }

    [Fact]
    public async Task CapNhat_EmailDaCoNguoiDung_BiTuChoi()
    {
        var user = TaoUser();
        SetupUser(user);
        _users.Setup(r => r.IsEmailUsedByAnotherUserAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.UpdateOwnProfileAsync(user.UserId, new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            Email = "datontai@example.com",
        });

        Assert.Equal(ProfileOperationResult.EmailAlreadyUsed, result);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UserStatus.Deactivated)]
    [InlineData(UserStatus.Deactivated)]
    public async Task CapNhat_TaiKhoanKhongActive_BiTuChoi(UserStatus status)
    {
        var user = TaoUser();
        user.Status = status;
        SetupUser(user);

        var result = await _sut.UpdateOwnProfileAsync(user.UserId, new UpdateProfileRequest
        {
            FullName = "Tên Mới",
        });

        Assert.Equal(ProfileOperationResult.AccountNotActive, result);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SinhTracHoc_BatTat_LuuDungCo(bool enabled)
    {
        var user = TaoUser();
        user.BiometricEnabled = !enabled;
        SetupUser(user);

        var result = await _sut.SetBiometricEnabledAsync(user.UserId, enabled);

        Assert.Equal(ProfileOperationResult.Success, result);
        Assert.Equal(enabled, user.BiometricEnabled);
    }

    [Theory]
    [InlineData(UserStatus.Deactivated)]
    [InlineData(UserStatus.Deactivated)]
    public async Task SinhTracHoc_TaiKhoanKhongActive_BiTuChoi(UserStatus status)
    {
        // UC-02 AF-02: tài khoản bị khoá thì không bật được sinh trắc học.
        var user = TaoUser();
        user.Status = status;
        SetupUser(user);

        var result = await _sut.SetBiometricEnabledAsync(user.UserId, true);

        Assert.Equal(ProfileOperationResult.AccountNotActive, result);
    }

    // ---- helpers ----

    private void SetupUser(User? user) =>
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

    private void SetupEmailFree() =>
        _users.Setup(r => r.IsEmailUsedByAnotherUserAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

    private static User TaoUser() => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0912345678",
        FullName = "Nguyễn Văn A",
        Email = null,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
        Status = UserStatus.Active,
        Role = UserRole.Patient,
        BiometricEnabled = false,
    };
}
