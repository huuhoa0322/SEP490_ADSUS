using ADSUS_BE.BLL.MedicalRecord.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.MedicalRecord.Validators;

/// <summary>
/// Chỉ kiểm các trường vô hướng. Số lượng ảnh KHÔNG kiểm ở đây — và từ quyết định ghi đè
/// 07/08/2026, cũng không kiểm ở đâu khác nữa: ảnh siêu âm giờ hoàn toàn tùy chọn khi tạo ca
/// khám (#20). Bổ sung ảnh sau đó (#21) vẫn bắt buộc ≥1 ảnh, xem AddUltrasoundImagesRequestValidator.
/// </summary>
public sealed class CreateCaseRequestValidator : AbstractValidator<CreateCaseRequest>
{
    public CreateCaseRequestValidator()
    {
        RuleFor(x => x.PatientProfileId)
            .NotEmpty().WithMessage("Patient profile id is required.");

        RuleFor(x => x.ResponsibleDoctorId)
            .NotEmpty().WithMessage("Responsible doctor id is required.");

        RuleFor(x => x.ClinicalInfo)
            .MaximumLength(5000).WithMessage("Clinical info must be 5000 characters or fewer.");
    }
}

/// <summary>
/// Ở đây thì ngược lại: đặc tả #21 quy định "không đính kèm file" trả 400, nên kiểm bằng
/// validator là đúng. Sự khác biệt giữa #20 và #21 đã được ghi lại ở flag N2.
/// </summary>
public sealed class AddUltrasoundImagesRequestValidator : AbstractValidator<AddUltrasoundImagesRequest>
{
    public AddUltrasoundImagesRequestValidator()
    {
        RuleFor(x => x.Images)
            .NotEmpty().WithMessage("At least one image file is required.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must be 1000 characters or fewer.");
    }
}

/// <summary>
/// Thêm 07/08/2026 — cả hai trường bắt buộc, dùng chung cho cả Lưu kết luận và Kết thúc ca
/// khám (xem CaseConclusionRequest): không cho lưu/kết thúc với kết luận bỏ trống.
/// </summary>
public sealed class CaseConclusionRequestValidator : AbstractValidator<CaseConclusionRequest>
{
    public CaseConclusionRequestValidator()
    {
        RuleFor(x => x.FinalDiagnosis)
            .NotEmpty().WithMessage("Final diagnosis is required.")
            .MaximumLength(5000).WithMessage("Final diagnosis must be 5000 characters or fewer.");

        RuleFor(x => x.DoctorConclusion)
            .NotEmpty().WithMessage("Doctor conclusion is required.")
            .MaximumLength(5000).WithMessage("Doctor conclusion must be 5000 characters or fewer.");
    }
}
