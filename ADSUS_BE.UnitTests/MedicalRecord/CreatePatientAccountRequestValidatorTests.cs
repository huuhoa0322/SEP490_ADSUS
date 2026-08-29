using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CreatePatientAccountRequestValidatorTests
{
    private readonly CreatePatientAccountRequestValidator _validator = new();

    private static CreatePatientAccountRequest ValidRequest() => new(
        PhoneNumber: "0912345678",
        FullName: "Nguyễn Thị Hoa",
        DateOfBirth: new DateOnly(1992, 5, 14),
        Email: "hoa@example.com");

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyPhoneNumber_Fails()
    {
        // Arrange
        var request = ValidRequest() with { PhoneNumber = "" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientAccountRequest.PhoneNumber));
    }

    [Theory]
    [InlineData("12345678")] // không bắt đầu bằng 0
    [InlineData("091234567")] // 9 chữ số, thiếu 1
    [InlineData("09123456789")] // 11 chữ số, thừa 1
    [InlineData("091234567a")] // có ký tự chữ
    public void InvalidPhoneNumberFormat_Fails(string phone)
    {
        // Arrange
        var request = ValidRequest() with { PhoneNumber = phone };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientAccountRequest.PhoneNumber));
    }

    [Fact]
    public void EmptyFullName_Fails()
    {
        // Arrange
        var request = ValidRequest() with { FullName = "" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientAccountRequest.FullName));
    }

    [Fact]
    public void FullName_101Chars_Fails()
    {
        // Arrange
        var request = ValidRequest() with { FullName = new string('A', 101) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientAccountRequest.FullName));
    }

    [Fact]
    public void NullEmail_Passes()
    {
        // Arrange — email tuỳ chọn khi tạo tài khoản Bệnh nhân (chỉ dùng cho quên mật khẩu).
        var request = ValidRequest() with { Email = null };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidEmailFormat_Fails()
    {
        // Arrange
        var request = ValidRequest() with { Email = "not-an-email" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientAccountRequest.Email));
    }

    [Fact]
    public void NullDateOfBirth_Passes()
    {
        // Arrange
        var request = ValidRequest() with { DateOfBirth = null };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void FutureDateOfBirth_Fails()
    {
        // Arrange
        var request = ValidRequest() with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientAccountRequest.DateOfBirth));
    }
}
