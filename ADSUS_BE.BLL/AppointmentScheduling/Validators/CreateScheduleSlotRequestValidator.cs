using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

/// <summary>
/// Validator cho CreateScheduleSlotRequest (UC-15).
/// BR-01: VisitDate không trong quá khứ; Start &lt; End; range > 15 phút.
/// </summary>
public sealed class CreateScheduleSlotRequestValidator : AbstractValidator<CreateScheduleSlotRequest>
{
    public CreateScheduleSlotRequestValidator()
    {
        RuleFor(x => x.DoctorId)
            .NotEmpty().WithMessage("DoctorId is required.");

        RuleFor(x => x.VisitDate)
            .NotEqual(default(DateOnly)).WithMessage("VisitDate is required.")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date))
                .WithMessage("VisitDate must not be in the past.");

        RuleFor(x => x.StartTime)
            .NotEqual(default(TimeOnly)).WithMessage("StartTime is required.");

        RuleFor(x => x.EndTime)
            .NotEqual(default(TimeOnly)).WithMessage("EndTime is required.");

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
                .WithMessage("StartTime must be earlier than EndTime.");

        // Range > 15 phút (BR-01 UC-15: "must be greater than 15 minutes")
        RuleFor(x => x)
            .Must(x => x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan() > TimeSpan.FromMinutes(15))
                .WithMessage("Slot duration must be greater than 15 minutes.");
    }
}