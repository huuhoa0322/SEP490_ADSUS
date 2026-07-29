using ADSUS_BE.BLL.Auth.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    // Password policy comes from TDS §4.3: 8–72 characters, at least one uppercase letter
    // and one digit. The upper bound is not arbitrary — 72 bytes is BCrypt's limit and
    // anything beyond it is silently truncated.
    private const int MinLength = 8;
    private const int MaxLength = 72;

    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(MinLength).WithMessage($"New password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength).WithMessage($"New password must not exceed {MaxLength} characters.")
            .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one digit.");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.NewPassword).WithMessage("Password confirmation does not match the new password.");

        // The UCS explicitly states that "the new password must differ from the old one" is
        // NOT a rule — it exists in neither the PRD nor the TDS, so it must not be added here.
    }
}
