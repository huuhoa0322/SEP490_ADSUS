using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

/// <summary>
/// UC-03 — chỉ kiểm HÌNH DẠNG dữ liệu.
///
/// Cố ý KHÔNG kiểm số điện thoại hay email có tồn tại không: trả lời câu đó chính là làm lộ
/// tài khoản nào có thật (AF-01). Việc đối chiếu nằm ở service và im lặng.
/// </summary>
public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(15).WithMessage("Phone number must not exceed 15 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.");
    }
}
