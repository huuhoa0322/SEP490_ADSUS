using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

public class CompletePasswordResetWithOtpRequestValidator : AbstractValidator<CompletePasswordResetWithOtpRequest>
{
    private const int MinLength = 8;
    private const int MaxLength = 72;

    public CompletePasswordResetWithOtpRequestValidator()
    {
        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(MinLength).WithMessage($"New password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength).WithMessage($"New password must not exceed {MaxLength} characters.")
            .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one digit.");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.NewPassword).WithMessage("Password confirmation does not match the new password.");
    }
}
