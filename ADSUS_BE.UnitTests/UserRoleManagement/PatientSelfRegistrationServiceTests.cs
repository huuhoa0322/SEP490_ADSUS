using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class PatientSelfRegistrationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFirebasePhoneVerificationService> _firebase = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly PatientSelfRegistrationService _sut;

    public PatientSelfRegistrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options);

        _sut = new PatientSelfRegistrationService(
            new ADSUS_BE.DAL.Repositories.Implementations.UnitOfWork(_db),
            PatientAccountTestServices.PatientProfiles(_db),
            _users.AddsTo(_db).Object,
            _firebase.Object,
            _auth.Object,
            Mock.Of<ILogger<PatientSelfRegistrationService>>());
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private static CompleteRegistrationRequest ValidRequest(string? gender = null) => new(
        FirebaseIdToken: "valid-firebase-id-token",
        FullName: "Nguyễn Thị Lan",
        Password: "Password123",
        ConfirmPassword: "Password123",
        Email: "lan@example.com",
        DateOfBirth: "1995-06-15",
        Gender: gender);

    [Fact]
    public async Task CompleteRegistrationAsync_ValidToken_NoExistingGuest_CreatesUserAndNewPatientProfile()
    {
        // Arrange
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        var expectedLogin = new LoginResponse { AccessToken = "token-abc", UserId = Guid.NewGuid() };
        _auth.Setup(a => a.LoginAsync(
                It.Is<LoginRequest>(r => r.PhoneNumber == "0987654321" && r.Password == "Password123"),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(expectedLogin);

        // Act
        var response = await _sut.CompleteRegistrationAsync(
            ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expectedLogin, response);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == "0987654321");
        Assert.NotNull(user);
        Assert.Equal(UserRole.Patient, user.Role);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.False(user.MustChangePassword);
        Assert.Equal("0987654321", user.Phone);
        Assert.Equal("Nguyễn Thị Lan", user.FullName);
        Assert.Equal("lan@example.com", user.Email);
        Assert.Equal(new DateOnly(1995, 6, 15), user.DateOfBirth);
        Assert.Equal(GenderType.Female, user.Gender); // Fallback to Female when null

        var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId);
        Assert.NotNull(profile);
        Assert.Equal(user.UserId, profile.UserId);
        Assert.Equal(user.UserId, profile.CreatedBy);
        Assert.NotEqual(Guid.Empty, profile.PatientProfileId);
        Assert.Null(profile.FullName);
        Assert.Null(profile.Phone);
        Assert.Null(profile.DateOfBirth);
        Assert.Equal(1, await _db.PatientProfiles.CountAsync());
    }

    [Theory]
    [InlineData("MALE", GenderType.Male)]
    [InlineData("male", GenderType.Male)]
    [InlineData("OTHER", GenderType.Other)]
    [InlineData("other", GenderType.Other)]
    [InlineData("FEMALE", GenderType.Female)]
    [InlineData("female", GenderType.Female)]
    public async Task CompleteRegistrationAsync_ExplicitGender_CreatesUserWithSpecifiedGender(
        string genderInput, GenderType expectedGender)
    {
        // Arrange
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new LoginResponse { AccessToken = "tok", UserId = Guid.NewGuid() });

        // Act
        await _sut.CompleteRegistrationAsync(
            ValidRequest(genderInput), TestContext.Current.CancellationToken);

        // Assert
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == "0987654321");
        Assert.NotNull(user);
        Assert.Equal(expectedGender, user.Gender);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CompleteRegistrationAsync_NullOrEmptyGender_FallsBackToFemale(string? genderInput)
    {
        // Arrange
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new LoginResponse { AccessToken = "tok", UserId = Guid.NewGuid() });

        // Act
        await _sut.CompleteRegistrationAsync(
            ValidRequest(genderInput), TestContext.Current.CancellationToken);

        // Assert
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == "0987654321");
        Assert.NotNull(user);
        Assert.Equal(GenderType.Female, user.Gender);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_WithExistingGuestProfile_LinksProfileAndClearsGuestFields()
    {
        // Arrange
        var guestProfileId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var originalCreatedAt = DateTime.UtcNow.AddDays(-10);

        var guestProfile = new PatientProfile
        {
            PatientProfileId = guestProfileId,
            UserId = null,
            Phone = "0987654321",
            FullName = "Khách Hàng Vãng Lai",
            DateOfBirth = new DateOnly(1988, 12, 1),
            CreatedBy = doctorId,
            CreatedAt = originalCreatedAt,
            UpdatedAt = originalCreatedAt,
        };

        _db.PatientProfiles.Add(guestProfile);
        await _db.SaveChangesAsync();

        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new LoginResponse { AccessToken = "token-123", UserId = Guid.NewGuid() });

        // Act
        await _sut.CompleteRegistrationAsync(
            ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, await _db.PatientProfiles.CountAsync()); // No duplicate profile!

        var linkedProfile = await _db.PatientProfiles.FirstAsync();
        var user = await _db.Users.FirstAsync(u => u.Phone == "0987654321");

        Assert.Equal(guestProfileId, linkedProfile.PatientProfileId);
        Assert.Equal(user.UserId, linkedProfile.UserId);
        Assert.Null(linkedProfile.FullName);
        Assert.Null(linkedProfile.Phone);
        Assert.Null(linkedProfile.DateOfBirth);
        Assert.Equal(doctorId, linkedProfile.CreatedBy); // Original doctor creator preserved
        Assert.True(linkedProfile.UpdatedAt > originalCreatedAt);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_PhoneAlreadyRegistered_ThrowsConflictException()
    {
        // Arrange
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.CompleteRegistrationAsync(ValidRequest(), TestContext.Current.CancellationToken));

        Assert.Empty(await _db.Users.ToListAsync());
        Assert.Empty(await _db.PatientProfiles.ToListAsync());
    }

    [Fact]
    public async Task CompleteRegistrationAsync_InvalidFirebaseToken_ThrowsBusinessException()
    {
        // Arrange
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteRegistrationAsync(ValidRequest(), TestContext.Current.CancellationToken));

        Assert.Empty(await _db.Users.ToListAsync());
        Assert.Empty(await _db.PatientProfiles.ToListAsync());
    }

    [Fact]
    public async Task CompleteRegistrationAsync_AutoLoginFails_ThrowsBusinessException()
    {
        // Arrange
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((LoginResponse?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteRegistrationAsync(ValidRequest(), TestContext.Current.CancellationToken));

        Assert.Contains("automatic sign-in failed", ex.Message);
        Assert.Single(await _db.Users.ToListAsync());
        Assert.Single(await _db.PatientProfiles.ToListAsync());
    }
}
