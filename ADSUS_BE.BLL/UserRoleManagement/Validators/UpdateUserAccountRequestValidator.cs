using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

/// <summary>
/// UC-04 FT-09 — kiểm tra dữ liệu sửa tài khoản và phân lại vai trò.
///
/// Cùng luật với lúc tạo, trừ số điện thoại (không sửa được nên không có trong DTO — BR-02).
/// </summary>
public class UpdateUserAccountRequestValidator : AbstractValidator<UpdateUserAccountRequest>
{
    private static readonly string[] AllowedRoles = { "DOCTOR", "NURSE", "PATIENT" };

    public UpdateUserAccountRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => AllowedRoles.Contains(r?.Trim().ToUpperInvariant()))
            .WithMessage("Role must be one of DOCTOR, NURSE or PATIENT.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .Must(CreateUserAccountRequestValidator.BeAValidPastDate)
            .WithMessage("Date of birth must be in yyyy-MM-dd format and must not be in the future.")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));
    }
}
