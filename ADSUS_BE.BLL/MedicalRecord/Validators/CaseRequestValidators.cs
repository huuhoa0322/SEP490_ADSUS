using ADSUS_BE.BLL.MedicalRecord.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.MedicalRecord.Validators;

/// <summary>
/// Chỉ kiểm các trường vô hướng. Số lượng ảnh KHÔNG kiểm ở đây — và từ quyết định ghi đè
/// 07/08/2026, cũng không kiểm ở đâu khác nữa: ảnh siêu âm giờ hoàn toàn tùy chọn khi tạo ca
/// khám (#20). Ảnh thật sự được ghi nhận sau đó qua luồng "Xem kết quả AI" → xác nhận phân
/// tích (CaseDiagnosisService.ConfirmAnalysisAsync), không phải qua endpoint bổ sung ảnh thô
/// riêng — endpoint đó (#21, AddUltrasoundImagesRequestValidator) đã bị xoá 29/08/2026 vì
/// không còn client nào gọi tới (audit code-vs-tài-liệu).
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
