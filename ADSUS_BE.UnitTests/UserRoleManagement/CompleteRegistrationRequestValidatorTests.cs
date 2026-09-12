using System;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Validators;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class CompleteRegistrationRequestValidatorTests
{
    private readonly CompleteRegistrationRequestValidator _sut = new();

    private static CompleteRegistrationRequest ValidRequest() => new(
        RegistrationToken: "some-opaque-token",
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
    public void Validate_EmptyRegistrationToken_Fails()
    {
        var request = ValidRequest() with { RegistrationToken = "" };

        var result = _sut.Validate(request);

        Assert.False(result.IsValid);
    }
}
