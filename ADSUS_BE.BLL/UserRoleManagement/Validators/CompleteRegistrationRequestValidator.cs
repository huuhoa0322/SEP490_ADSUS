using System.Globalization;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

/// <summary>
/// Bước 3 — chính sách mật khẩu giống hệt <c>ChangePasswordRequestValidator</c> (TDS §4.3).
/// KHÔNG kiểm tuổi tối thiểu cho DateOfBirth — khác CreateUserAccountRequestValidator của
/// Admin, mirror PatientAccountService (Điều dưỡng): đây là dữ liệu lâm sàng bệnh nhân tự
/// khai, không phải "account holder" hành chính (xem Global Constraints).
/// </summary>
public class CompleteRegistrationRequestValidator : AbstractValidator<CompleteRegistrationRequest>
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 72;
    private const string DateFormat = "yyyy-MM-dd";

    public CompleteRegistrationRequestValidator()
    {
        RuleFor(x => x.RegistrationToken)
            .NotEmpty().WithMessage("Registration token is required.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinPasswordLength).WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .MaximumLength(MaxPasswordLength).WithMessage($"Password must not exceed {MaxPasswordLength} characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.Password).WithMessage("Password confirmation does not match the password.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .Must(BeAParsableDate).WithMessage("Date of birth must be in yyyy-MM-dd format.")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));

        RuleFor(x => x.DateOfBirth)
            .Must(BeNotInTheFuture).WithMessage("Date of birth must not be in the future.")
            .When(x => BeAParsableDate(x.DateOfBirth));
    }

    private static bool BeAParsableDate(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool BeNotInTheFuture(string? value)
    {
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
        return date <= DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
