using ADSUS_BE.BLL.Auth.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Input shape only. Whether the phone number exists or the password is correct is
        // NOT checked here — answering that in detail would violate GB-06.
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(15).WithMessage("Phone number must not exceed 15 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
