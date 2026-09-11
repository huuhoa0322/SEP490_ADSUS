using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.ClinicServiceManagement.Validators;

public class CreateClinicServiceRequestValidator : AbstractValidator<CreateClinicServiceRequest>
{
    public CreateClinicServiceRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã dịch vụ không được để trống.")
            .MaximumLength(50).WithMessage("Mã dịch vụ không được vượt quá 50 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên dịch vụ không được để trống.")
            .MaximumLength(200).WithMessage("Tên dịch vụ không được vượt quá 200 ký tự.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Giá dịch vụ phải lớn hơn 0.");
    }
}
