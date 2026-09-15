using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.BLL.UserRoleManagement.Validators;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class AdversarialChallengerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFirebasePhoneVerificationService> _firebase = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly PatientSelfRegistrationService _sut;
    private readonly CompleteRegistrationRequestValidator _validator = new();

    public AdversarialChallengerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options);

        _sut = new PatientSelfRegistrationService(
            _db,
            _users.Object,
            _firebase.Object,
            _auth.Object,
            Mock.Of<ILogger<PatientSelfRegistrationService>>());
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private static CompleteRegistrationRequest CreateRequest(string? gender = null, string phone = "0912345678") => new(
        FirebaseIdToken: "valid-token",
        FullName: "Adversarial Tester",
        Password: "ValidPassword123",
        ConfirmPassword: "ValidPassword123",
        Email: "test@example.com",
        DateOfBirth: "1990-01-01",
        Gender: gender);

    // --- 1. GENDER VALIDATOR RIGOROUS CHALLENGES ---

    [Theory]
    [InlineData("MALE", true)]
    [InlineData("FEMALE", true)]
    [InlineData("OTHER", true)]
    [InlineData("male", true)]
    [InlineData("female", true)]
    [InlineData("other", true)]
    [InlineData("MaLe", true)]
    [InlineData("FeMaLe", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("\t", true)]
    [InlineData("\n", true)]
    [InlineData("UNKNOWN", false)]
    [InlineData("invalid", false)]
    [InlineData("123", false)]
    [InlineData("NAM", false)]
    [InlineData("NỮ", false)]
    [InlineData("KHAC", false)]
    [InlineData("NONE", false)]
    [InlineData("!@#$%", false)]
    public void Validator_GenderAdversarialInputs_MatchesExpected(string? genderInput, bool shouldBeValid)
    {
        var request = CreateRequest(gender: genderInput);
        var result = _validator.Validate(request);
        var hasGenderError = result.Errors.Any(e => e.PropertyName == "Gender");

        if (shouldBeValid)
        {
            Assert.False(hasGenderError, $"Expected gender '{genderInput}' to be valid, but got error: {result.Errors.FirstOrDefault(e => e.PropertyName == "Gender")?.ErrorMessage}");
        }
        else
        {
            Assert.True(hasGenderError, $"Expected gender '{genderInput}' to be rejected, but it passed validation.");
            Assert.Contains(result.Errors, e => e.PropertyName == "Gender" && e.ErrorMessage == "Gender must be FEMALE, MALE or OTHER.");
        }
    }

    // --- 2. AF-01 ANTI-ENUMERATION CHALLENGE ---

    [Fact]
    public async Task AF01_WhenFirebaseTokenInvalid_DoesNotQueryPhoneRepository()
    {
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteRegistrationAsync(CreateRequest(), TestContext.Current.CancellationToken));

        _users.Verify(u => u.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AF01_WhenPhoneExists_ThrowsConflictExceptionWithoutExposingUserData()
    {
        var phone = "0912345678";
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phone);
        _users.Setup(u => u.PhoneExistsAsync(phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _sut.CompleteRegistrationAsync(CreateRequest(phone: phone), CancellationToken.None));

        Assert.Equal("This phone number is already registered.", ex.Message);
        Assert.Empty(_db.Users);
        Assert.Empty(_db.PatientProfiles);
    }

    // --- 3. GUEST PROFILE LINKING CHALLENGES ---

    [Fact]
    public async Task GuestLinking_WhenGuestProfileExists_ProperlyLinksAndClearsGuestFields()
    {
        var phone = "0912345678";
        var guestId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var originalCreated = DateTime.UtcNow.AddMonths(-1);

        var guest = new PatientProfile
        {
            PatientProfileId = guestId,
            UserId = null,
            Phone = phone,
            FullName = "Guest Patient Original Name",
            DateOfBirth = new DateOnly(1985, 5, 20),
            CreatedBy = doctorId,
            CreatedAt = originalCreated,
            UpdatedAt = originalCreated,
        };
        _db.PatientProfiles.Add(guest);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phone);
        _users.Setup(u => u.PhoneExistsAsync(phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { AccessToken = "valid-token", UserId = Guid.NewGuid() });

        await _sut.CompleteRegistrationAsync(CreateRequest(phone: phone), TestContext.Current.CancellationToken);

        Assert.Equal(1, await _db.PatientProfiles.CountAsync(TestContext.Current.CancellationToken));
        var linked = await _db.PatientProfiles.SingleAsync(TestContext.Current.CancellationToken);
        var user = await _db.Users.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(guestId, linked.PatientProfileId);
        Assert.Equal(user.UserId, linked.UserId);
        Assert.Null(linked.FullName);
        Assert.Null(linked.Phone);
        Assert.Null(linked.DateOfBirth);
        Assert.Equal(doctorId, linked.CreatedBy);
        Assert.True(linked.UpdatedAt > originalCreated);
    }

    [Fact]
    public async Task GuestLinking_WhenProfileAlreadyHasUserId_DoesNotHijackProfile()
    {
        var phone = "0912345678";
        var existingOtherUserId = Guid.NewGuid();
        var existingProfileId = Guid.NewGuid();

        var claimedProfile = new PatientProfile
        {
            PatientProfileId = existingProfileId,
            UserId = existingOtherUserId,
            Phone = phone,
            FullName = "Claimed Patient",
            CreatedBy = existingOtherUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
        };
        _db.PatientProfiles.Add(claimedProfile);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phone);
        _users.Setup(u => u.PhoneExistsAsync(phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { AccessToken = "tok", UserId = Guid.NewGuid() });

        await _sut.CompleteRegistrationAsync(CreateRequest(phone: phone), TestContext.Current.CancellationToken);

        Assert.Equal(2, await _db.PatientProfiles.CountAsync(TestContext.Current.CancellationToken));
        var original = await _db.PatientProfiles.FirstAsync(p => p.PatientProfileId == existingProfileId, TestContext.Current.CancellationToken);
        Assert.Equal(existingOtherUserId, original.UserId);

        var newUser = await _db.Users.SingleAsync(TestContext.Current.CancellationToken);
        var newProfile = await _db.PatientProfiles.FirstAsync(p => p.PatientProfileId != existingProfileId, TestContext.Current.CancellationToken);
        Assert.Equal(newUser.UserId, newProfile.UserId);
        Assert.Equal(newUser.UserId, newProfile.CreatedBy);
    }

    [Fact]
    public async Task NewProfile_WhenNoGuestProfileExists_CreatesCleanProfileWithUserIdAsCreator()
    {
        var phone = "0912345678";
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phone);
        _users.Setup(u => u.PhoneExistsAsync(phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { AccessToken = "tok", UserId = Guid.NewGuid() });

        await _sut.CompleteRegistrationAsync(CreateRequest(phone: phone), TestContext.Current.CancellationToken);

        var user = await _db.Users.SingleAsync(TestContext.Current.CancellationToken);
        var profile = await _db.PatientProfiles.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(user.UserId, profile.UserId);
        Assert.Equal(user.UserId, profile.CreatedBy);
        Assert.NotEqual(Guid.Empty, profile.PatientProfileId);
        Assert.Null(profile.FullName);
        Assert.Null(profile.Phone);
        Assert.Null(profile.DateOfBirth);
    }
}
