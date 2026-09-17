using System.Text.RegularExpressions;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

/// <summary>
/// Validator cho BookAppointmentRequest (UC-13).
/// BR-01: ScheduleSlotId is required.
/// </summary>
public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    private static readonly Regex HtmlTagRegex = new(@"<[a-zA-Z\/][^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.ScheduleSlotId)
            .NotEmpty()
                .WithMessage("ScheduleSlotId is required.");

        RuleFor(x => x.Reason)
            .Must(reason => string.IsNullOrEmpty(reason) || !HtmlTagRegex.IsMatch(reason))
                .WithMessage("Reason cannot contain HTML tags.");
    }
}
