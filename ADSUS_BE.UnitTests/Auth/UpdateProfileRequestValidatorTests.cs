using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Validators;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// UC-10 luật nhập liệu hồ sơ cá nhân.
///
/// Họ tên bắt buộc. Email không bắt buộc nhưng nếu có phải đúng định dạng.
/// BR-01: ngày sinh không bắt buộc nhưng KHÔNG được ở tương lai.
///
/// Số điện thoại cố ý không có trong DTO — BR-02 quy định bệnh nhân không tự đổi được,
/// nên cách chắc chắn nhất là không cho nó tồn tại trong request.
/// </summary>
public class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _sut = new();

    [Fact]
    public void Valid_FullInformation_Passes()
    {
        var result = _sut.Validate(new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            Email = "vana@example.com",
            DateOfBirth = "1990-05-20",
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_FullNameOnly_Passes()
    {
        // Email và ngày sinh đều không bắt buộc.
        var result = _sut.Validate(new UpdateProfileRequest { FullName = "Nguyễn Văn A" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Invalid_MissingFullName_Fails(string fullName)
    {
        var result = _sut.Validate(new UpdateProfileRequest { FullName = fullName });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("khong-phai-email")]
    [InlineData("thieu-cuoi@")]
    public void Invalid_MalformedEmail_Fails(string email)
    {
        var result = _sut.Validate(new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            Email = email,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_DateOfBirthInFuture_Fails()
    {
        // BR-01. Dùng mốc 1 năm sau để không bị lệch múi giờ làm sai kết quả.
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)).ToString("yyyy-MM-dd");

        var result = _sut.Validate(new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            DateOfBirth = futureDate,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_DateOfBirthInPast_Passes()
    {
        var result = _sut.Validate(new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            DateOfBirth = "1985-01-01",
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("20-05-1990")]
    [InlineData("1990/05/20")]
    [InlineData("khong-phai-ngay")]
    public void Invalid_MalformedDateOfBirth_Fails(string dateOfBirth)
    {
        var result = _sut.Validate(new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn A",
            DateOfBirth = dateOfBirth,
        });

        Assert.False(result.IsValid);
    }
}
