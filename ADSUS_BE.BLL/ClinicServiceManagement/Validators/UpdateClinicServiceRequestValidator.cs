using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.ClinicServiceManagement.Validators;

public class UpdateClinicServiceRequestValidator : AbstractValidator<UpdateClinicServiceRequest>
{
    public UpdateClinicServiceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().When(x => x.Name != null).WithMessage("Tên dịch vụ không được để trống.")
            .MaximumLength(200).When(x => x.Name != null).WithMessage("Tên dịch vụ không được vượt quá 200 ký tự.");

        RuleFor(x => x.Price)
            .GreaterThan(0).When(x => x.Price.HasValue).WithMessage("Giá dịch vụ phải lớn hơn 0.");
    }
}
