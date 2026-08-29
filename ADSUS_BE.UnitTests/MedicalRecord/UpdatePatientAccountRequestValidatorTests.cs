using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

// UC-06 AF-02 — Điều dưỡng sửa lỗi nhập liệu trên tài khoản Bệnh nhân. Cùng bộ rule với
// CreatePatientAccountRequestValidator nhưng là 2 class riêng — test riêng để không giả định
// một class thay đổi thì class kia cũng đổi theo.
public class UpdatePatientAccountRequestValidatorTests
{
    private readonly UpdatePatientAccountRequestValidator _validator = new();

    private static UpdatePatientAccountRequest ValidRequest() => new(
        FullName: "Nguyễn Thị Hoa",
        PhoneNumber: "0912345678",
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientAccountRequest.PhoneNumber));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("091234567")]
    [InlineData("09123456789")]
    public void InvalidPhoneNumberFormat_Fails(string phone)
    {
        // Arrange
        var request = ValidRequest() with { PhoneNumber = phone };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientAccountRequest.PhoneNumber));
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientAccountRequest.FullName));
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientAccountRequest.FullName));
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientAccountRequest.Email));
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientAccountRequest.DateOfBirth));
    }
}
