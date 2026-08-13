using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Validators;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Validators;
using ADSUS_BE.DAL.Data;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class UserAccountRequestValidatorTests
{
    private readonly CreateUserAccountRequestValidator _create = new();
    private readonly UpdateUserAccountRequestValidator _update = new();

    [Fact]
    public void Validate_DateOfBirthIsToday_IsRejected()
    {
        var result = _create.Validate(CreateRequest(ClinicClock.Today()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "Account holder must be at least 18 years old.");
    }

    [Fact]
    public void Validate_OneDayShortOfEighteenYears_IsRejected()
    {
        var dateOfBirth = ClinicClock.Today().AddYears(-18).AddDays(1);

        var result = _create.Validate(CreateRequest(dateOfBirth));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ExactlyEighteenYears_IsAccepted()
    {
        var dateOfBirth = ClinicClock.Today().AddYears(-18);

        var result = _create.Validate(CreateRequest(dateOfBirth));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateValidate_DateOfBirthUnder18_IsRejected()
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

    // ---------- Định dạng số điện thoại ----------

    [Theory]
    [InlineData("0900000001")]
    [InlineData("0987654321")]
    public void Validate_TenDigitPhoneNumber_IsAccepted(string soDienThoai)
    {
        var request = CreateRequest(ClinicClock.Today().AddYears(-30));
        request.PhoneNumber = soDienThoai;

        Assert.True(_create.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("090000001", "chỉ 9 chữ số")]
    [InlineData("09000000012", "11 chữ số")]
    [InlineData("1900000001", "không bắt đầu bằng 0")]
    [InlineData("090000000a", "có chữ cái")]
    [InlineData("0900 000 001", "có khoảng trắng")]
    [InlineData("+84900000001", "dạng quốc tế")]
    public void Validate_MalformedPhoneNumber_IsRejected(string soDienThoai, string lyDo)
    {
        // Khoảng cũ là 9–11 chữ số, quá rộng: gõ thiếu hoặc thừa một số vẫn lọt qua, mà số
        // điện thoại là ĐỊNH DANH ĐĂNG NHẬP (BR-02) — sai một chữ số là tạo ra một tài khoản
        // không ai đăng nhập được, và số thật thì vẫn còn trống.
        var request = CreateRequest(ClinicClock.Today().AddYears(-30));
        request.PhoneNumber = soDienThoai;

        Assert.False(_create.Validate(request).IsValid, lyDo);
    }

    [Theory]
    [InlineData("090000001")]
    [InlineData("09000000012")]
    [InlineData("khong-phai-so")]
    public void ForgotPasswordValidate_MalformedPhoneNumber_IsRejected(string soDienThoai)
    {
        // Chỗ này TRƯỚC ĐÂY không kiểm định dạng, chỉ kiểm độ dài tối đa — nên cùng một số
        // sai bị chặn ở màn tạo tài khoản lại đi lọt tới tận database ở màn quên mật khẩu.
        var result = new ForgotPasswordRequestValidator().Validate(new ForgotPasswordRequest
        {
            PhoneNumber = soDienThoai,
            Email = "a@example.com",
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void LoginValidate_DoesNotEnforcePhoneNumberFormat()
    {
        // Cố ý khác hai màn kia. Đăng nhập là ĐỐI CHIẾU chứ không phải nhập liệu: số sai định
        // dạng thì đằng nào cũng không khớp tài khoản nào, cứ để GB-06 trả về đúng một câu.
        //
        // Quan trọng hơn: siết định dạng ở đây là nhốt người dùng ra ngoài — tài khoản cũ có
        // số 9 hay 11 chữ số sẽ không đăng nhập được nữa dù mật khẩu vẫn đúng.
        var result = new LoginRequestValidator().Validate(new LoginRequest
        {
            PhoneNumber = "090000001",
            Password = "Aa123456@",
        });

        Assert.True(result.IsValid);
    }

    private static CreateUserAccountRequest CreateRequest(DateOnly dateOfBirth) => new()
    {
        PhoneNumber = "0900000001",
        FullName = "Nguyễn Văn A",
        Role = "DOCTOR",
        DateOfBirth = dateOfBirth.ToString("yyyy-MM-dd"),
    };
}
