using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Validators;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class CompleteRegistrationRequestValidatorTests
{
    private readonly CompleteRegistrationRequestValidator _sut = new();

    private static CompleteRegistrationRequest ValidRequest() => new(
        FirebaseIdToken: "some-opaque-firebase-id-token",
        FullName: "Nguyễn Thị Lan",
        Password: "Password123",
        ConfirmPassword: "Password123",
        Email: null,
        DateOfBirth: null);

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _sut.Validate(ValidRequest());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PasswordMismatch_Fails()
    {
        var request = ValidRequest() with { ConfirmPassword = "Different123" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_PasswordMissingDigit_Fails()
    {
        var request = ValidRequest() with { Password = "OnlyLetters", ConfirmPassword = "OnlyLetters" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DateOfBirthInFuture_Fails()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");
        var request = ValidRequest() with { DateOfBirth = futureDate };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyFirebaseIdToken_Fails()
    {
        var request = ValidRequest() with { FirebaseIdToken = "" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("FEMALE")]
    [InlineData("MALE")]
    [InlineData("OTHER")]
    [InlineData("female")]
    [InlineData("male")]
    [InlineData("other")]
    [InlineData("Female")]
    public void Validate_ValidGender_Passes(string gender)
    {
        var request = ValidRequest() with { Gender = gender };
        var result = _sut.Validate(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmptyGender_Passes(string? gender)
    {
        var request = ValidRequest() with { Gender = gender };
        var result = _sut.Validate(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("UNKNOWN")]
    [InlineData("123")]
    public void Validate_InvalidGender_Fails(string gender)
    {
        var request = ValidRequest() with { Gender = gender };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Gender" && e.ErrorMessage == "Gender must be FEMALE, MALE or OTHER.");
    }

    [Fact]
    public void Validate_PasswordMissingUppercase_Fails()
    {
        var request = ValidRequest() with { Password = "password123", ConfirmPassword = "password123" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("uppercase"));
    }

    [Fact]
    public void Validate_PasswordTooShort_Fails()
    {
        var request = ValidRequest() with { Password = "Pass1", ConfirmPassword = "Pass1" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        var request = ValidRequest() with { Email = "not-an-email" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_DateOfBirthInvalidFormat_Fails()
    {
        var request = ValidRequest() with { DateOfBirth = "15/05/1990" };
        var result = _sut.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DateOfBirth");
    }
}
