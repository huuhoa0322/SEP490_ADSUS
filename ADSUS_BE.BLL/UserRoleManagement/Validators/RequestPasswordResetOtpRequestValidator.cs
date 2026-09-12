using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.UserRoleManagement.Validators;

public class RequestPasswordResetOtpRequestValidator : AbstractValidator<RequestPasswordResetOtpRequest>
{
    public RequestPasswordResetOtpRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(PhoneNumberRule.Pattern).WithMessage(PhoneNumberRule.Message);
    }
}
