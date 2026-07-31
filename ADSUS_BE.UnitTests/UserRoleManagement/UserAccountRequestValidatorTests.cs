using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Validators;
using ADSUS_BE.DAL.Data;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class UserAccountRequestValidatorTests
{
    private readonly CreateUserAccountRequestValidator _create = new();
    private readonly UpdateUserAccountRequestValidator _update = new();

    [Fact]
    public void Tao_NgaySinhLaHomNay_BiTuChoi()
    {
        var result = _create.Validate(CreateRequest(ClinicClock.Today()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Account holder must be at least 18 years old.");
    }

    [Fact]
    public void Tao_ChuaDu18TuoiMotNgay_BiTuChoi()
    {
        var dateOfBirth = ClinicClock.Today().AddYears(-18).AddDays(1);

        var result = _create.Validate(CreateRequest(dateOfBirth));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Tao_VuaTron18Tuoi_DuocChapNhan()
    {
        var dateOfBirth = ClinicClock.Today().AddYears(-18);

        var result = _create.Validate(CreateRequest(dateOfBirth));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Sua_NgaySinhKhongDu18Tuoi_BiTuChoi()
    {
        var request = new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "DOCTOR",
            DateOfBirth = ClinicClock.Today().AddYears(-18).AddDays(1).ToString("yyyy-MM-dd"),
        };

        var result = _update.Validate(request);

        Assert.False(result.IsValid);
    }

    private static CreateUserAccountRequest CreateRequest(DateOnly dateOfBirth) => new()
    {
        PhoneNumber = "0900000001",
        FullName = "Nguyễn Văn A",
        Role = "DOCTOR",
        DateOfBirth = dateOfBirth.ToString("yyyy-MM-dd"),
    };
}
