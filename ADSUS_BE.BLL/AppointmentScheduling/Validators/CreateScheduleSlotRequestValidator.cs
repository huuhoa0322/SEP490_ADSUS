using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

/// <summary>
/// Validator cho CreateScheduleSlotRequest (UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); Start &lt; End; range > 15 phút.
/// </summary>
public sealed class CreateScheduleSlotRequestValidator : AbstractValidator<CreateScheduleSlotRequest>
{
    public CreateScheduleSlotRequestValidator()
    {
        RuleFor(x => x.VisitDate)
            .NotEqual(default(DateOnly)).WithMessage("VisitDate is required.");

        RuleFor(x => x.StartTime)
            .NotEqual(default(TimeOnly)).WithMessage("StartTime is required.");

        RuleFor(x => x.EndTime)
            .NotEqual(default(TimeOnly)).WithMessage("EndTime is required.");

        RuleFor(x => x)
            .Must(x => x.StartTime < x.EndTime)
                .WithMessage("StartTime must be earlier than EndTime.");

        // Range > 15 phút (BR-01 UC-15).
        RuleFor(x => x)
            .Must(x => x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan() > TimeSpan.FromMinutes(15))
                .WithMessage("Slot duration must be greater than 15 minutes.");

        // BR-01 (revised): VisitDate + StartTime phải > now (UTC). Vd: ngày 5/8 đã 12h
        // thì không thể tạo ca 8h-9h ngày 5/8 nữa.
        RuleFor(x => x)
            .Must(x => x.VisitDate.ToDateTime(x.StartTime, DateTimeKind.Utc) > DateTime.UtcNow)
                .WithMessage("Slot start time must be in the future.");
    }
}