using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Validators;

public sealed class CreateScheduleSlotRequestValidator : AbstractValidator<CreateScheduleSlotRequest>
{
    public CreateScheduleSlotRequestValidator()
    {
        RuleFor(r => r.DoctorId).NotEqual(Guid.Empty);
        RuleFor(r => r.SlotDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("SlotDate không được ở quá khứ (UC-15 BR-01).");
        RuleFor(r => r.StartTime)
            .NotEmpty()
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("StartTime phải có định dạng HH:mm.");
        RuleFor(r => r.EndTime)
            .NotEmpty()
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("EndTime phải có định dạng HH:mm.");
        RuleFor(r => r)
            .Must(r =>
            {
                if (!TimeOnly.TryParse(r.StartTime, out var s) ||
                    !TimeOnly.TryParse(r.EndTime, out var e)) return false;
                return e > s.AddMinutes(15);
            })
            .WithMessage("EndTime phải lớn hơn StartTime ít nhất 15 phút (UC-15 BR-01).");
    }
}