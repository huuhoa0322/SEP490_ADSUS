using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

/// <summary>
/// Validator cho BookAppointmentRequest (UC-13).
/// BR-01: ScheduleSlotId is required.
/// </summary>
public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.ScheduleSlotId)
            .NotEmpty()
                .WithMessage("ScheduleSlotId is required.");
    }
}
