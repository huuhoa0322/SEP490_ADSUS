using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Validators;

/// <summary>
/// Validation cho PUT /api/v1/me/reminder-preference.
/// Giờ nhắc phải nằm trong khung hợp lệ:
///   - Sáng: 05:00 – 10:59 (ICT)
///   - Trưa: 11:00 – 16:59 (ICT)
///   - Tối:  17:00 – 23:59 (ICT)
///
/// Giờ gửi dạng "HH:mm" (ví dụ "07:30").
/// </summary>
public sealed class ReminderPreferenceValidator : AbstractValidator<UpdateReminderPreferenceRequest>
{
    public ReminderPreferenceValidator()
    {
        RuleFor(r => r.MorningTime)
            .Must(BeValidMorningTime)
            .WithMessage("Giờ Sáng phải từ 05:00 đến 10:59.")
            .When(r => !string.IsNullOrEmpty(r.MorningTime));

        RuleFor(r => r.MiddayTime)
            .Must(BeValidMiddayTime)
            .WithMessage("Giờ Trưa phải từ 11:00 đến 16:59.")
            .When(r => !string.IsNullOrEmpty(r.MiddayTime));

        RuleFor(r => r.EveningTime)
            .Must(BeValidEveningTime)
            .WithMessage("Giờ Tối phải từ 17:00 đến 23:59.")
            .When(r => !string.IsNullOrEmpty(r.EveningTime));
    }

    // Sáng: 05:00–10:59
    private static bool BeValidMorningTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (!TryParseTime(value, out var t)) return false;
        return t.Hour >= 5 && (t.Hour < 10 || (t.Hour == 10 && t.Minute <= 59));
    }

    // Trưa: 11:00–16:59
    private static bool BeValidMiddayTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (!TryParseTime(value, out var t)) return false;
        return t.Hour >= 11 && (t.Hour < 16 || (t.Hour == 16 && t.Minute <= 59));
    }

    // Tối: 17:00–23:59
    private static bool BeValidEveningTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (!TryParseTime(value, out var t)) return false;
        return t.Hour >= 17 && t.Hour <= 23;
    }

    private static bool TryParseTime(string value, out TimeOnly result)
    {
        return TimeOnly.TryParse(value, out result);
    }
}
