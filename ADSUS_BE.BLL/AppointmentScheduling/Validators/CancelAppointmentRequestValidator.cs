using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

/// <summary>
/// Validator cho CancelAppointmentRequest (UC-14).
/// BR-02: CancellationReason is required and must be at least 3 characters.
/// </summary>
public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.CancellationReason)
            .NotEmpty()
                .WithMessage("Cancellation reason is required.")
            .MinimumLength(3)
                .WithMessage("Cancellation reason must be at least 3 characters.");
    }
}
