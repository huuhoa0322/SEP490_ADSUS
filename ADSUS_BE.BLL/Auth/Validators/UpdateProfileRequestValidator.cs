using System.Globalization;
using ADSUS_BE.BLL.Auth.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.Auth.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        // UC-10 Request Fields: Full name bat buoc.
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        // Email khong bat buoc, nhung neu co thi phai dung dinh dang (BR-01).
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        // BR-01: ngay sinh khong bat buoc, nhung KHONG duoc o tuong lai.
        RuleFor(x => x.DateOfBirth)
            .Must(BeAParsableDate).WithMessage("Date of birth must be in yyyy-MM-dd format.")
            .When(x => !string.IsNullOrWhiteSpace(x.DateOfBirth));

        RuleFor(x => x.DateOfBirth)
            .Must(NotBeInTheFuture).WithMessage("Date of birth must not be in the future.")
            .When(x => BeAParsableDate(x.DateOfBirth));
    }

    private static bool BeAParsableDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);

    private static bool NotBeInTheFuture(string? value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            // Sai dinh dang thi de luat BeAParsableDate bao loi, cho nay bo qua.
            return true;
        }

        // So voi ngay hom nay theo UTC. Chenh mui gio toi da 1 ngay, chap nhan duoc voi
        // mot truong ngay sinh.
        return date <= DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
