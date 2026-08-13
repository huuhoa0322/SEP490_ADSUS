using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.HealthMonitoring.Validators;

/// <summary>
/// Validator for LogHealthDataRequest (UC-21, FT-35).
/// Based on API Spec Module09 endpoint #55.
/// BR-02: type must be EXERCISE or DIET, content must be non-empty.
/// </summary>
public sealed class LogHealthDataRequestValidator : AbstractValidator<LogHealthDataRequest>
{
    private static readonly string[] ValidTypes = { "EXERCISE", "DIET" };

    public LogHealthDataRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
                .WithMessage("Type is required.")
            .Must(BeValidType)
                .WithMessage("Type must be EXERCISE or DIET.");

        RuleFor(x => x.Content)
            .NotEmpty()
                .WithMessage("Content is required.")
            .Must(content => !string.IsNullOrWhiteSpace(content))
                .WithMessage("Content is required.");
    }

    private static bool BeValidType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;

        return ValidTypes.Contains(type.ToUpperInvariant());
    }
}
