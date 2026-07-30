using System.Globalization;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

/// <summary>
/// UC-04 — kiểm tra dữ liệu tạo tài khoản (bảng Request Fields của CreateUserAccountRequest).
///
/// Ở đây chỉ kiểm HÌNH DẠNG dữ liệu. Những thứ phải hỏi database — số điện thoại đã tồn tại
/// chưa, email đã ai dùng chưa — nằm ở tầng service, vì validator không nên gọi xuống DB.
/// </summary>
public class CreateUserAccountRequestValidator : AbstractValidator<CreateUserAccountRequest>
{
    /// <summary>
    /// Vai trò hợp lệ. Cố ý KHÔNG có ADMIN: theo UC-04, tài khoản quản trị được cấp lúc dựng
    /// hệ thống chứ không tạo qua màn này.
    /// </summary>
    private static readonly string[] AllowedRoles = { "DOCTOR", "NURSE", "PATIENT" };

    public CreateUserAccountRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(15).WithMessage("Phone number must not exceed 15 characters.")
            .Matches(@"^0\d{8,10}$")
            .WithMessage("Phone number must start with 0 and contain 9 to 11 digits.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => AllowedRoles.Contains(r?.Trim().ToUpperInvariant()))
            .WithMessage("Role must be one of DOCTOR, NURSE or PATIENT.");

        // Email không bắt buộc, nhưng đã nhập thì phải đúng dạng — đó là kênh DUY NHẤT để
        // gửi mật khẩu tạm, gõ sai là chủ tài khoản không bao giờ nhận được.
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .Must(BeAValidPastDate)
            .WithMessage("Date of birth must be in yyyy-MM-dd format and must not be in the future.")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));
    }

    /// <summary>Đúng định dạng yyyy-MM-dd và không nằm ở tương lai.</summary>
    internal static bool BeAValidPastDate(string? value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return false;
        }

        return date <= DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
