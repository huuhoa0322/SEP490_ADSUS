using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class PatientAccountServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private readonly Mock<IPasswordResetService> _passwordReset = new();
    private readonly PatientAccountService _sut;

    private readonly Guid _actingNurseId = Guid.NewGuid();

    public PatientAccountServiceTests()
    {
        _sut = new PatientAccountService(
            _users.Object,
            _email.Object,
            _audit.Object,
            _passwordReset.Object,
            Mock.Of<ILogger<PatientAccountService>>());
    }

    private static CreatePatientAccountRequest ValidCreateRequest() => new(
        PhoneNumber: "0981234567",
        FullName: "Lê Thị Hoa",
        DateOfBirth: new DateOnly(1984, 3, 12),
        Email: "hoa@example.com");

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesActivePatientAccountWithDateOfBirth()
    {
        // Arrange — điểm khác biệt cốt lõi so với UC-04: Admin tạo tài khoản PATIENT thì ngày
        // sinh bị vứt bỏ (BR-01), còn Điều dưỡng thì PHẢI giữ — dữ liệu đó hiển thị suốt
        // UC-06/07/08 dưới dạng chỉ đọc.
        var request = ValidCreateRequest();
        _users.Setup(r => r.PhoneExistsAsync(request.PhoneNumber, It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedAsync(request.Email!, It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        User? saved = null;
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        // Act
        var response = await _sut.CreateAsync(request, _actingNurseId);

        // Assert
        Assert.Equal(UserRole.Patient, saved!.Role);
        Assert.Equal(UserStatus.Active, saved.Status);
        Assert.True(saved.MustChangePassword);
        Assert.Equal(new DateOnly(1984, 3, 12), saved.DateOfBirth);
        Assert.Equal(saved.UserId, response.UserId);
        Assert.Equal(new DateOnly(1984, 3, 12), response.DateOfBirth);
    }

    [Fact]
    public async Task CreateAsync_PhoneAlreadyUsed_ThrowsConflictException()
    {
        // Arrange — UC-04 BR-02 không đổi: số điện thoại là định danh đăng nhập duy nhất.
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.CreateAsync(ValidCreateRequest(), _actingNurseId));
    }

    [Fact]
    public async Task CreateAsync_EmailAlreadyUsed_ThrowsConflictException()
    {
        // Arrange
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.CreateAsync(ValidCreateRequest(), _actingNurseId));
    }

    [Fact]
    public async Task CreateAsync_WritesAuditLogWithActingNurseAsActor()
    {
        // Arrange — BR-06 / GB-09 (đã mở rộng ở PRD v1.28): hành động tài khoản của Điều
        // dưỡng phải truy vết được y như của Admin.
        var request = ValidCreateRequest();
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        AuditLog? logged = null;
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
              .Callback<AuditLog, CancellationToken>((l, _) => logged = l)
              .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(request, _actingNurseId);

        // Assert
        Assert.NotNull(logged);
        Assert.Equal(_actingNurseId, logged!.ActorId);
        Assert.Equal("NURSE_CREATE_PATIENT_ACCOUNT", logged.Action);
    }

    [Fact]
    public async Task CreateAsync_EmailNotProvided_StillCreatesAccount()
    {
        // Arrange — UC-04 ghi Email là Optional. Tài khoản vẫn tạo được, chỉ là mật khẩu tạm
        // không đi đâu được; Điều dưỡng bổ sung email rồi bấm Cấp lại mật khẩu sau.
        var request = ValidCreateRequest() with { Email = null };
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        User? saved = null;
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        // Act
        var response = await _sut.CreateAsync(request, _actingNurseId);

        // Assert
        Assert.Null(saved!.Email);
        Assert.Equal(saved.UserId, response.UserId);
        _email.Verify(e => e.SendTemporaryPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
