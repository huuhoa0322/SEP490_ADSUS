using System.Text.RegularExpressions;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

/// <summary>
/// Validator cho UpdateAppointmentClinicalInfoRequest (UC-13).
/// Reason cannot contain HTML tags.
/// </summary>
public sealed class UpdateAppointmentClinicalInfoRequestValidator : AbstractValidator<UpdateAppointmentClinicalInfoRequest>
{
    private static readonly Regex HtmlTagRegex = new(@"<[a-zA-Z\/][^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public UpdateAppointmentClinicalInfoRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => string.IsNullOrEmpty(reason) || !HtmlTagRegex.IsMatch(reason))
                .WithMessage("Reason cannot contain HTML tags.");
    }
}
