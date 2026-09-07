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
    /// <summary>
    /// Có ADMIN, khác với lúc tạo.
    ///
    /// Không phải để cho phép phong Admin — service vẫn chặn mọi thay đổi vai trò dính tới
    /// ADMIN. Có ở đây là để sửa được TÊN và EMAIL của một tài khoản Admin: form phải gửi
    /// lại đúng vai trò hiện tại, mà nếu validator không nhận "ADMIN" thì giao diện buộc
    /// phải gửi một vai trò khác — tức là nói dối trên đường truyền để đi qua được kiểm tra.
    /// </summary>
    private static readonly string[] AllowedRoles = { "ADMIN", "DOCTOR", "NURSE", "PATIENT", "PHARMACIST" };

    public UpdateUserAccountRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => AllowedRoles.Contains(r?.Trim().ToUpperInvariant()))
            .WithMessage("Role must be one of ADMIN, DOCTOR, NURSE, PATIENT or PHARMACIST.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .Must(CreateUserAccountRequestValidator.BeAParsableDate)
            .WithMessage("Date of birth must be in yyyy-MM-dd format.")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));

        RuleFor(x => x.DateOfBirth)
            .Must(CreateUserAccountRequestValidator.BeAtLeastMinimumAge)
            .WithMessage($"Account holder must be at least "
                + $"{CreateUserAccountRequestValidator.MinimumAccountHolderAge} years old.")
            .When(x => CreateUserAccountRequestValidator.BeAParsableDate(x.DateOfBirth));
    }
}
