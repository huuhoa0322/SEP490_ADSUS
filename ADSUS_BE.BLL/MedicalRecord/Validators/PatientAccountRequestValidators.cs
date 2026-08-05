using ADSUS_BE.BLL.MedicalRecord.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.MedicalRecord.Validators;

public sealed class CreatePatientAccountRequestValidator : AbstractValidator<CreatePatientAccountRequest>
{
    public CreatePatientAccountRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^0\d{9}$").WithMessage("Phone number must be 10 digits starting with 0.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        // Ngày sinh trong tương lai là lỗi nhập liệu, không phải trường hợp nghiệp vụ nào cả.
        RuleFor(x => x.DateOfBirth)
            .Must(d => d!.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must not be in the future.")
            .When(x => x.DateOfBirth.HasValue);
    }
}

public sealed class UpdatePatientAccountRequestValidator : AbstractValidator<UpdatePatientAccountRequest>
{
    public UpdatePatientAccountRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^0\d{9}$").WithMessage("Phone number must be 10 digits starting with 0.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DateOfBirth)
            .Must(d => d!.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must not be in the future.")
            .When(x => x.DateOfBirth.HasValue);
    }
}
