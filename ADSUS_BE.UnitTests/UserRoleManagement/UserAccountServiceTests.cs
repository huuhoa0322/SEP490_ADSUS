using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

/// <summary>
/// UC-04 — Admin quản lý tài khoản (FT-07 tạo, FT-08 vô hiệu hoá, FT-09 phân quyền).
///
/// Bám theo phần Verification Criteria của UC-04 trong UCS.
/// </summary>
public class UserAccountServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly UserAccountService _sut;

    private readonly List<User> _saved = new();

    /// <summary>Các dòng nhật ký đã được xếp vào hàng chờ trong bài test.</summary>
    private readonly List<AuditLog> _audited = new();

    /// <summary>Admin đang thao tác. Là actor được ghi vào nhật ký.</summary>
    private readonly Guid _adminId = Guid.NewGuid();

    public UserAccountServiceTests()
    {
        _auditLogs.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
                  .Callback<AuditLog, CancellationToken>((l, _) => _audited.Add(l))
                  .Returns(Task.CompletedTask);

        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedByAnotherUserAsync(
                  It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => _saved.Add(u))
              .Returns(Task.CompletedTask);

        _sut = new UserAccountService(
            _users.Object,
            new AccountAuditTrail(_auditLogs.Object),
            new Mock<ILogger<UserAccountService>>().Object);
    }

    // ---------- FT-07: tạo tài khoản ----------

    [Fact]
    public async Task CreateAsync_ValidDoctorRequest_CreatesActiveAccountAndForcesPasswordChange()
    {
        var (result, account, _) = await _sut.CreateAsync(BuildCreateRequest("DOCTOR"), _adminId);

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal("ACTIVE", account!.Status);
        Assert.Equal("DOCTOR", account.Role);

        // BR-03 — tài khoản mới luôn bị buộc đổi mật khẩu ở lần đăng nhập đầu.
        var user = Assert.Single(_saved);
        Assert.True(user.MustChangePassword);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task CreateAsync_TemporaryPassword_IsHashedReturnedOnceAndNeverInResponse()
    {
        // Sửa 12/08/2026 — thống nhất với UC-03 AF-02/UC-06 AF-01/AF-03: mật khẩu tạm không
        // còn gửi email, mà trả plaintext MỘT LẦN qua phần tử thứ ba của tuple.
        var (_, account, temporaryPassword) = await _sut.CreateAsync(BuildCreateRequest("DOCTOR"), _adminId);

        var user = Assert.Single(_saved);

        // PRD §6.2 — DB chỉ lưu bản băm.
        Assert.StartsWith("$2", user.PasswordHash);

        // Trả về đúng một lần, và khớp với bản băm đã lưu.
        Assert.False(string.IsNullOrEmpty(temporaryPassword));
        Assert.True(BCrypt.Net.BCrypt.Verify(temporaryPassword, user.PasswordHash));

        // account (UserAccountResponse) không có trường nào chứa mật khẩu — DTO không định
        // nghĩa trường đó, nên chỉ cần khẳng định các trường công khai là đủ.
        Assert.NotNull(account);
        Assert.Equal("0988776655", account!.PhoneNumber);
    }

    [Fact]
    public async Task TemporaryPasswordGenerator_Generate_AlwaysMeetsPasswordPolicy()
    {
        // TDS §4.3: 8–72 ký tự, ít nhất 1 chữ hoa, ít nhất 1 chữ số.
        // Sinh nhiều lần vì đây là hàm ngẫu nhiên — chạy một lần không chứng minh được gì.
        for (var i = 0; i < 200; i++)
        {
            var matKhau = TemporaryPasswordGenerator.Generate();

            Assert.InRange(matKhau.Length, 8, 72);
            Assert.Contains(matKhau, char.IsUpper);
            Assert.Contains(matKhau, char.IsDigit);
        }
    }

    [Fact]
    public async Task CreateAsync_NoEmail_StillSucceedsAndReturnsPasswordDirectly()
    {
        // UCS ghi Email là Optional. Từ 12/08/2026, mật khẩu tạm không còn phụ thuộc email —
        // luôn trả về trực tiếp cho Admin đọc, nên việc có/không khai email không còn ảnh
        // hưởng gì tới việc tài khoản đăng nhập được hay không.
        var request = BuildCreateRequest("DOCTOR");
        request.Email = null;

        var (result, account, temporaryPassword) = await _sut.CreateAsync(request, _adminId);

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.NotNull(account);
        Assert.False(string.IsNullOrEmpty(temporaryPassword));
        Assert.Single(_saved);
    }

    [Fact]
    public async Task CreateAsync_PhoneAlreadyExists_IsRejected()
    {
        // AF-03 / BR-02 — số điện thoại là định danh đăng nhập duy nhất.
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var (result, account, temporaryPassword) = await _sut.CreateAsync(BuildCreateRequest("DOCTOR"), _adminId);

        Assert.Equal(AccountOperationResult.PhoneAlreadyUsed, result);
        Assert.Null(account);
        Assert.Null(temporaryPassword);
        Assert.Empty(_saved);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("")]
    [InlineData("SUPERUSER")]
    public async Task CreateAsync_InvalidRole_IsRejected(string vaiTro)
    {
        // UC-04: tài khoản ADMIN được cấp lúc dựng hệ thống, KHÔNG tạo qua màn này.
        var (result, _, _) = await _sut.CreateAsync(BuildCreateRequest(vaiTro), _adminId);

        Assert.Equal(AccountOperationResult.InvalidRole, result);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task CreateAsync_EmailAlreadyUsed_IsRejected()
    {
        _users.Setup(r => r.IsEmailUsedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var (result, account, temporaryPassword) = await _sut.CreateAsync(BuildCreateRequest("DOCTOR"), _adminId);

        Assert.Equal(AccountOperationResult.EmailAlreadyUsed, result);
        Assert.Null(account);
        Assert.Null(temporaryPassword);
        Assert.Empty(_saved);
    }

    // ---------- SCR-06: tìm kiếm và phân trang ----------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task SearchAsync_PageBelowOne_IsClampedToOne(int requestedPage, int expectedPage)
    {
        _users.Setup(r => r.SearchAsync(
                  It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<UserStatus?>(),
                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Array.Empty<User>(), 0));

        var result = await _sut.SearchAsync(null, null, null, requestedPage, 20, _adminId);

        Assert.Equal(expectedPage, result.Page);
        _users.Verify(r => r.SearchAsync(
            null, null, null, expectedPage, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(101, 20)]
    public async Task SearchAsync_PageSizeOutOfRange_IsClampedToDefault(int requestedPageSize, int expectedPageSize)
    {
        // MaxPageSize = 100 — chặn client kéo cả bảng users về một lần.
        _users.Setup(r => r.SearchAsync(
                  It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<UserStatus?>(),
                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Array.Empty<User>(), 0));

        var result = await _sut.SearchAsync(null, null, null, 1, requestedPageSize, _adminId);

        Assert.Equal(expectedPageSize, result.PageSize);
    }

    [Fact]
    public async Task SearchAsync_ValidPageAndPageSize_PassedThroughUnchanged()
    {
        _users.Setup(r => r.SearchAsync(
                  It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<UserStatus?>(),
                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Array.Empty<User>(), 0));

        var result = await _sut.SearchAsync(null, null, null, 3, 50, _adminId);

        Assert.Equal(3, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    // ---------- BR-01: ngày sinh của bệnh nhân ----------

    [Fact]
    public async Task CreateAsync_PatientRole_DateOfBirthIsIgnoredEvenIfSent()
    {
        // BR-01 — ngày sinh là dữ liệu y tế, Admin không được chạm. Ẩn ở giao diện là chưa
        // đủ vì gọi thẳng API vẫn gửi lên được, nên tầng nghiệp vụ phải tự loại bỏ.
        var request = BuildCreateRequest("PATIENT");
        request.DateOfBirth = "1990-05-20";

        var (result, account, _) = await _sut.CreateAsync(request, _adminId);

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Null(Assert.Single(_saved).DateOfBirth);
        Assert.Null(account!.DateOfBirth);
    }

    [Fact]
    public async Task CreateAsync_DoctorRole_DateOfBirthIsSaved()
    {
        // BR-01 chỉ áp cho PATIENT. Bác sĩ và điều dưỡng vẫn khai ngày sinh bình thường.
        var request = BuildCreateRequest("DOCTOR");
        request.DateOfBirth = "1985-03-10";

        var (_, account, _) = await _sut.CreateAsync(request, _adminId);

        Assert.Equal(new DateOnly(1985, 3, 10), Assert.Single(_saved).DateOfBirth);
        Assert.Equal("1985-03-10", account!.DateOfBirth);
    }

    [Fact]
    public async Task GetByIdAsync_PatientAccount_DoesNotReturnDateOfBirth()
    {
        // Kể cả khi trong DB có sẵn ngày sinh (do dữ liệu cũ, hoặc do vai trò vừa bị đổi),
        // giao diện quản trị vẫn không được thấy.
        var user = BuildDbUser(UserRole.Patient);
        user.DateOfBirth = new DateOnly(1990, 5, 20);
        SetupGetById(user);

        var account = await _sut.GetByIdAsync(user.UserId, Guid.NewGuid());

        Assert.Null(account!.DateOfBirth);
    }

    [Fact]
    public async Task GetByIdAsync_AccountNotFound_ReturnsNull()
    {
        SetupGetById(null);

        var account = await _sut.GetByIdAsync(Guid.NewGuid(), _adminId);

        Assert.Null(account);
    }

    [Fact]
    public async Task GetByIdAsync_ActingAdminIsTarget_FlagsIsCurrentUserCorrectly()
    {
        // Để giao diện ẩn nút khoá và vô hiệu hoá trên dòng của chính người đang đăng nhập —
        // backend vốn đã chặn (AF-04), bày ra nút chắc chắn báo lỗi chỉ làm người dùng bối rối.
        var user = BuildDbUser(UserRole.Admin);
        SetupGetById(user);

        var chinhMinh = await _sut.GetByIdAsync(user.UserId, user.UserId);
        var nguoiKhac = await _sut.GetByIdAsync(user.UserId, Guid.NewGuid());

        Assert.True(chinhMinh!.IsCurrentUser);
        Assert.False(nguoiKhac!.IsCurrentUser);
    }

    // ---------- FT-08: vô hiệu hoá ----------

    [Fact]
    public async Task DeactivateAsync_ActiveAccount_TransitionsToDeactivatedWithoutDeletingRow()
    {
        // BR-05 — không bao giờ xoá cứng; dữ liệu liên quan phải còn truy cập được.
        var user = BuildDbUser(UserRole.Patient);
        SetupGetById(user);

        var result = await _sut.DeactivateAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Deactivated, user.Status);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_TargetIsDifferentAdmin_Succeeds()
    {
        // UC-04 AF-04 — nhóm chốt ngày 31/07/2026: Admin được vô hiệu hoá Admin khác.
        // Chỉ cấm thao tác lên chính mình.
        var adminKhac = BuildDbUser(UserRole.Admin);
        SetupGetById(adminKhac);

        var result = await _sut.DeactivateAsync(adminKhac.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Deactivated, adminKhac.Status);
    }

    [Fact]
    public async Task DeactivateAsync_TargetIsSelf_ReturnsCannotTargetSelf()
    {
        var adminId = Guid.NewGuid();

        var result = await _sut.DeactivateAsync(adminId, adminId);

        Assert.Equal(AccountOperationResult.CannotTargetSelf, result);
    }

    [Fact]
    public async Task DeactivateAsync_AccountNotFound_ReturnsNotFound()
    {
        SetupGetById(null);

        var result = await _sut.DeactivateAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(AccountOperationResult.NotFound, result);
    }

    // ---------- AF-02: khôi phục tài khoản ----------

    [Fact]
    public async Task ReactivateAsync_DeactivatedAccount_TransitionsToActive()
    {
        var user = BuildDbUser(UserRole.Doctor);
        user.Status = UserStatus.Deactivated;
        SetupGetById(user);

        var result = await _sut.ReactivateAsync(_adminId, user.UserId, "khôi phục theo yêu cầu");

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Active, user.Status);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateAsync_TargetIsSelf_ReturnsCannotTargetSelf()
    {
        var adminId = Guid.NewGuid();

        var result = await _sut.ReactivateAsync(adminId, adminId, "lý do bất kỳ");

        Assert.Equal(AccountOperationResult.CannotTargetSelf, result);
    }

    [Fact]
    public async Task ReactivateAsync_AccountNotFound_ReturnsNotFound()
    {
        SetupGetById(null);

        var result = await _sut.ReactivateAsync(_adminId, Guid.NewGuid(), "lý do bất kỳ");

        Assert.Equal(AccountOperationResult.NotFound, result);
    }

    [Fact]
    public async Task ReactivateAsync_AccountAlreadyActive_IsRejected()
    {
        // Chỉ khôi phục tài khoản ĐANG bị vô hiệu hoá — gọi lại trên tài khoản đã Active
        // không có tác dụng gì, không nên âm thầm trả Success.
        var user = BuildDbUser(UserRole.Doctor);
        user.Status = UserStatus.Active;
        SetupGetById(user);

        var result = await _sut.ReactivateAsync(_adminId, user.UserId, "lý do bất kỳ");

        Assert.NotEqual(AccountOperationResult.Success, result);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- FT-09: phân quyền ----------

    [Fact]
    public async Task UpdateAsync_AccountNotFound_ReturnsNotFound()
    {
        SetupGetById(null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "DOCTOR",
        }, _adminId);

        Assert.Equal(AccountOperationResult.NotFound, result);
    }

    [Fact]
    public async Task UpdateAsync_InvalidRoleString_IsRejected()
    {
        var user = BuildDbUser(UserRole.Doctor);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "SUPERUSER",
        }, _adminId);

        Assert.Equal(AccountOperationResult.InvalidRole, result);
    }

    [Fact]
    public async Task UpdateAsync_EmailAlreadyUsedByAnotherUser_IsRejected()
    {
        var user = BuildDbUser(UserRole.Doctor);
        SetupGetById(user);
        _users.Setup(r => r.IsEmailUsedByAnotherUserAsync(
                  user.UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "DOCTOR",
            Email = "da-dung@example.com",
        }, _adminId);

        Assert.Equal(AccountOperationResult.EmailAlreadyUsed, result);
    }

    [Fact]
    public async Task UpdateAsync_ChangeRoleToNurse_Succeeds()
    {
        var user = BuildDbUser(UserRole.Doctor);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Vũ Thị Cẩm Tú",
            Role = "NURSE",
        }, _adminId);

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserRole.Nurse, user.Role);
    }

    [Fact]
    public async Task UpdateAsync_NeverChangesPhoneOrStatus()
    {
        // BR-02 — số điện thoại là định danh đăng nhập. Trạng thái đi qua endpoint riêng
        // (Deactivate) — UpdateAsync không được phép chạm vào.
        var user = BuildDbUser(UserRole.Doctor);
        user.Status = UserStatus.Deactivated;
        var soCu = user.Phone;
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Tên Mới",
            Role = "DOCTOR",
        }, _adminId);

        Assert.Equal(soCu, user.Phone);
        Assert.Equal(UserStatus.Deactivated, user.Status);
    }

    [Fact]
    public async Task UpdateAsync_ChangeRoleToPatient_PreservesDateOfBirthButHidesItOnRead()
    {
        // BR-01 nói Admin không được THẤY ngày sinh bệnh nhân — không nói phải XOÁ nó.
        // Hai việc khác nhau, và trước đây code làm nhầm sang việc thứ hai.
        var user = BuildDbUser(UserRole.Doctor);
        user.DateOfBirth = new DateOnly(1985, 3, 10);
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "PATIENT",
            DateOfBirth = "1985-03-10",
        }, _adminId);

        // Dữ liệu còn nguyên trong database...
        Assert.Equal(new DateOnly(1985, 3, 10), user.DateOfBirth);

        // ...nhưng Admin vẫn không đọc được. Đó mới là chỗ BR-01 được thi hành.
        var response = await _sut.GetByIdAsync(user.UserId, _adminId);
        Assert.Null(response!.DateOfBirth);
    }

    [Fact]
    public async Task UpdateAsync_EditingPatientName_NeverWipesDateOfBirthEnteredByNurse()
    {
        // Lỗi thật, xuất hiện khi UC-06 (Điều dưỡng tạo hồ sơ bệnh nhân) bắt đầu ghi ngày
        // sinh: Admin chỉ vào sửa lại cái tên cho đúng chính tả, mà ngày sinh Điều dưỡng vừa
        // nhập bị xoá sạch. Admin không nhìn thấy ô đó nên không hề biết mình vừa xoá gì, và
        // cũng không ai khôi phục lại được.
        var user = BuildDbUser(UserRole.Patient);
        user.DateOfBirth = new DateOnly(1992, 7, 15);
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Thị Hoa",
            Role = "PATIENT",
            // Form của Admin ẩn hẳn ô ngày sinh nên luôn gửi lên null.
            DateOfBirth = null,
        }, _adminId);

        Assert.Equal(new DateOnly(1992, 7, 15), user.DateOfBirth);
        Assert.Equal("Nguyễn Thị Hoa", user.FullName);
    }

    [Theory]
    [InlineData("DOCTOR")]
    [InlineData("NURSE")]
    [InlineData("PATIENT")]
    public async Task UpdateAsync_CannotDemoteAdminRole(string vaiTroMoi)
    {
        // Lỗ nguy hiểm nhất của màn này trước khi vá: ô vai trò trên form chỉ có Bác sĩ,
        // Điều dưỡng, Bệnh nhân. Mở một tài khoản Admin ra sửa thì ô đó rơi về giá trị đầu
        // danh sách, chỉ cần bấm Lưu để đổi cái tên là mất luôn quyền quản trị — không cảnh
        // báo, không hoàn tác được. Mất Admin cuối cùng là không còn ai tạo lại được nữa.
        var user = BuildDbUser(UserRole.Admin);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Quản trị viên",
            Role = vaiTroMoi,
        }, _adminId);

        Assert.Equal(AccountOperationResult.CannotChangeAdminRole, result);
        Assert.Equal(UserRole.Admin, user.Role);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_CannotPromoteAccountToAdmin()
    {
        // Chiều ngược lại. UC-04 ghi "Admin accounts are not created on this screen" —
        // không cho tạo thì cũng không được đi cửa sau bằng cách sửa vai trò.
        var user = BuildDbUser(UserRole.Doctor);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "ADMIN",
        }, _adminId);

        Assert.Equal(AccountOperationResult.CannotChangeAdminRole, result);
        Assert.Equal(UserRole.Doctor, user.Role);
    }

    [Fact]
    public async Task UpdateAsync_AdminAccount_NameAndEmailStillEditable()
    {
        // Khoá vai trò nhưng không được khoá luôn cả form: sửa tên hay email của tài khoản
        // Admin vẫn phải chạy, miễn là vai trò giữ nguyên ADMIN.
        var user = BuildDbUser(UserRole.Admin);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Quý Hiếu",
            Role = "ADMIN",
            Email = "admin@example.com",
        }, _adminId);

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal("Nguyễn Quý Hiếu", user.FullName);
        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    // ---------- Nhật ký thao tác ----------

    [Fact]
    public async Task CreateAsync_RecordsAuditLogWithCorrectActor()
    {
        await _sut.CreateAsync(BuildCreateRequest("DOCTOR"), _adminId);

        var log = Assert.Single(_audited);
        Assert.Equal("CREATE_ACCOUNT", log.Action);

        // Người thực hiện lấy từ token, KHÔNG phải người bị tác động — ghi nhầm chỗ này thì
        // nhật ký nói ngược hẳn ai đã làm gì.
        Assert.Equal(_adminId, log.ActorId);
        Assert.Contains("BS. Trần Văn B", log.Detail);
        Assert.Contains("0988776655", log.Detail);
    }

    [Fact]
    public async Task DeactivateAsync_RecordsAuditLogWithPriorStatus()
    {
        // Thao tác một chiều, không hoàn tác được (BR-05) — nhật ký là thứ duy nhất còn lại
        // để biết tài khoản đó trước khi bị vô hiệu hoá đang ở trạng thái nào.
        var user = BuildDbUser(UserRole.Doctor);
        user.Status = UserStatus.Active;
        SetupGetById(user);

        await _sut.DeactivateAsync(user.UserId, _adminId);

        var log = Assert.Single(_audited);
        Assert.Equal("DEACTIVATE_ACCOUNT", log.Action);
        Assert.Contains("ACTIVE", log.Detail);
    }

    [Fact]
    public async Task UpdateAsync_AuditLogRecordsRoleBeforeAndAfter()
    {
        var user = BuildDbUser(UserRole.Doctor);
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Vũ Thị Cẩm Tú",
            Role = "NURSE",
        }, _adminId);

        var log = Assert.Single(_audited);
        Assert.Equal("UPDATE_ACCOUNT", log.Action);
        Assert.Contains("DOCTOR", log.Detail);
        Assert.Contains("NURSE", log.Detail);
    }

    [Fact]
    public async Task RejectedOperations_AreNeverWrittenToAuditLog()
    {
        // Nhật ký chỉ ghi việc ĐÃ XẢY RA. Ghi cả những lần bị từ chối thì đọc lại sẽ tưởng
        // tài khoản đã bị thay đổi thật, trong khi thực tế không có gì xảy ra.
        var user = BuildDbUser(UserRole.Admin);
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Quản trị viên",
            Role = "DOCTOR",
        }, _adminId);

        await _sut.DeactivateAsync(_adminId, _adminId);

        Assert.Empty(_audited);
    }

    [Fact]
    public async Task ReactivateAsync_RecordsAuditLogWithReadableVietnameseReason()
    {
        // Bug thật phát hiện qua P11 review Feature 2 (28/08/2026): chuỗi ghi nhật ký ở đây
        // từng bị hỏng encoding (mojibake), nên mọi lần khôi phục tài khoản ghi chữ không đọc
        // được vào audit_log — một bảng dữ liệu tuân thủ.
        var user = BuildDbUser(UserRole.Doctor);
        user.Status = UserStatus.Deactivated;
        SetupGetById(user);

        await _sut.ReactivateAsync(_adminId, user.UserId, "Reactivated by admin via UI");

        var log = Assert.Single(_audited);
        Assert.Equal(AccountAuditTrail.ReactivateAccount, log.Action);
        Assert.Contains("khôi phục tài khoản, lý do: Reactivated by admin via UI", log.Detail);
    }

    [Fact]
    public async Task CreateAsync_AuditLogNeverContainsDateOfBirth()
    {
        // BR-01 — Admin không được xem ngày sinh của bệnh nhân. Chặn ở API rồi mà để rò qua
        // nhật ký thì cũng như không chặn.
        var request = BuildCreateRequest("DOCTOR");
        request.DateOfBirth = "1985-03-10";

        await _sut.CreateAsync(request, _adminId);

        var log = Assert.Single(_audited);
        Assert.DoesNotContain("1985", log.Detail);
    }

    // ---------- helpers ----------

    private static CreateUserAccountRequest BuildCreateRequest(string vaiTro) => new()
    {
        PhoneNumber = "0988776655",
        FullName = "BS. Trần Văn B",
        Role = vaiTro,
        Email = "bs.b@example.com",
    };

    private static User BuildDbUser(UserRole vaiTro) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0912345678",
        FullName = "Nguyễn Văn A",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa123456@"),
        Role = vaiTro,
        Status = UserStatus.Active,
    };

    private void SetupGetById(User? user)
    {
        // GetByIdAsync (service) đọc qua GetByIdReadOnlyAsync; Update/SetLocked/Deactivate
        // sửa-rồi-lưu qua GetForUpdateAsync — set cả hai để test không cần biết SUT gọi
        // method nào (P11 review Module 2, 12/08/2026).
        _users.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _users.Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
    }
}
