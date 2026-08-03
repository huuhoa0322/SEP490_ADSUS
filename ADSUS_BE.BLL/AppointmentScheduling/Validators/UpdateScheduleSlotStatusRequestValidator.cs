using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

public sealed class UpdateScheduleSlotStatusRequestValidator
    : AbstractValidator<UpdateScheduleSlotStatusRequest>
{
    public UpdateScheduleSlotStatusRequestValidator()
    {
        RuleFor(r => r.Status)
            .NotEmpty()
            .Must(s => s.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chỉ chấp nhận status = 'CLOSED' (UC-15 BR-02).");
    }
}