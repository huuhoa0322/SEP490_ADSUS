using ADSUS_BE.BLL.AIModelManagement.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.AIModelManagement.Validators;

public class RegisterModelVersionRequestValidator : AbstractValidator<RegisterModelVersionRequest>
{
    public RegisterModelVersionRequestValidator()
    {
        RuleFor(x => x.VersionCode)
            .NotEmpty().WithMessage("Mã phiên bản không được để trống.")
            .MaximumLength(50).WithMessage("Mã phiên bản không được vượt quá 50 ký tự.");

        RuleFor(x => x.HfRepoId)
            .NotEmpty().WithMessage("Hugging Face Repo ID không được để trống.")
            .MaximumLength(255).WithMessage("Hugging Face Repo ID không được vượt quá 255 ký tự.");

        RuleFor(x => x.HfFilename)
            .NotEmpty().WithMessage("Hugging Face Filename không được để trống.")
            .MaximumLength(255).WithMessage("Hugging Face Filename không được vượt quá 255 ký tự.");

        RuleFor(x => x.MetricsPrecision)
            .InclusiveBetween(0, 100).When(x => x.MetricsPrecision.HasValue)
            .WithMessage("Precision phải nằm trong khoảng từ 0 đến 100.");

        RuleFor(x => x.MetricsMap50)
            .InclusiveBetween(0, 100).When(x => x.MetricsMap50.HasValue)
            .WithMessage("mAP50 phải nằm trong khoảng từ 0 đến 100.");

        RuleFor(x => x.MetricsRecall)
            .InclusiveBetween(0, 1).When(x => x.MetricsRecall.HasValue)
            .WithMessage("Recall phải nằm trong khoảng từ 0 đến 1.");
    }
}
