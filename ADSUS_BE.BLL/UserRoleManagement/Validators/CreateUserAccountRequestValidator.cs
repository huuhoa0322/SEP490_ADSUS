using System.Globalization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Data;
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
    internal const int MinimumAccountHolderAge = 18;

    /// <summary>
    /// Vai trò hợp lệ. Cố ý KHÔNG có ADMIN: theo UC-04, tài khoản quản trị được cấp lúc dựng
    /// hệ thống chứ không tạo qua màn này.
    /// </summary>
    private static readonly string[] AllowedRoles = { "DOCTOR", "NURSE", "PATIENT" };

    public CreateUserAccountRequestValidator()
    {
        // Luật khai ở PhoneNumberRule, dùng chung với màn quên mật khẩu — xem chú thích ở đó.
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhoneNumberRule.Pattern).WithMessage(PhoneNumberRule.Message);

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
            .Must(BeAParsableDate)
            .WithMessage("Date of birth must be in yyyy-MM-dd format.")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));

        RuleFor(x => x.DateOfBirth)
            .Must(BeAtLeastMinimumAge)
            .WithMessage($"Account holder must be at least {MinimumAccountHolderAge} years old.")
            .When(x => BeAParsableDate(x.DateOfBirth));
    }

    internal static bool BeAParsableDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);

    internal static bool BeAtLeastMinimumAge(string? value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            // Lỗi định dạng do luật BeAParsableDate báo; không tạo thêm lỗi tuổi trùng lặp.
            return true;
        }

        return date <= ClinicClock.Today().AddYears(-MinimumAccountHolderAge);
    }
}
