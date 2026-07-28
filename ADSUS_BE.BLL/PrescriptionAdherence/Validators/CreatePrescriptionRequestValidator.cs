using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Validators;

/// <summary>
/// Validation cho POST /api/v1/prescriptions (UC-18). Áp dụng:
/// - CaseId, DoctorId, Items không được rỗng
/// - DurationDays trong [1, 365] (§3.1)
/// - Dosage không rỗng, tối đa 100 ký tự (master PrescriptionItem schema)
/// - ScheduleSlots phải có ít nhất 1 giá trị (§3.1: ≥1 khung uống)
/// - GeneralNote tối đa 2000 ký tự (master schema)
/// - Instructions tối đa 1000 ký tự (consistency với IntakeLogResponse)
/// </summary>
public sealed class CreatePrescriptionRequestValidator : AbstractValidator<CreatePrescriptionRequest>
{
    public CreatePrescriptionRequestValidator()
    {
        RuleFor(r => r.CaseId)
            .NotEmpty().WithMessage("CaseId không được để trống.");

        RuleFor(r => r.DoctorId)
            .NotEmpty().WithMessage("DoctorId không được để trống.");

        RuleFor(r => r.GeneralNote)
            .MaximumLength(2000).WithMessage("Ghi chú đơn tối đa 2000 ký tự.")
            .When(r => !string.IsNullOrEmpty(r.GeneralNote));

        RuleFor(r => r.Items)
            .NotEmpty().WithMessage("Đơn thuốc phải có ít nhất 1 dòng thuốc.");

        RuleForEach(r => r.Items).SetValidator(new CreatePrescriptionItemDtoValidator());
    }
}

public sealed class CreatePrescriptionItemDtoValidator : AbstractValidator<CreatePrescriptionItemDto>
{
    public CreatePrescriptionItemDtoValidator()
    {
        RuleFor(i => i.MedicineId)
            .NotEmpty().WithMessage("Mỗi dòng thuốc phải chỉ định MedicineId.");

        RuleFor(i => i.Dosage)
            .NotEmpty().WithMessage("Liều lượng không được để trống.")
            .MaximumLength(100).WithMessage("Liều lượng tối đa 100 ký tự.");

        RuleFor(i => i.DurationDays)
            .InclusiveBetween((short)1, (short)365)
            .WithMessage("DurationDays phải nằm trong khoảng [1, 365].");

        RuleFor(i => i.Instructions)
            .MaximumLength(1000).WithMessage("Hướng dẫn tối đa 1000 ký tự.")
            .When(i => !string.IsNullOrEmpty(i.Instructions));

        RuleFor(i => i.ScheduleSlots)
            .NotEmpty().WithMessage("Mỗi dòng thuốc phải có ít nhất 1 khung uống (MORNING/NOON/EVENING).");
    }
}