using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using FluentValidation;

namespace ADSUS_BE.BLL.MedicalRecord.Validators;

// Giới hạn độ dài dưới đây là rào phòng thủ tự đặt, không phải luật nghiệp vụ — cột DB là
// TEXT nên không giới hạn. Mục đích chỉ là chặn ai đó dán vào vài megabyte văn bản.
public sealed class CreatePatientProfileRequestValidator : AbstractValidator<CreatePatientProfileRequest>
{
    public CreatePatientProfileRequestValidator()
    {
        RuleFor(x => x.PatientUserId)
            .NotEmpty().WithMessage("Patient user id is required.");

        // Gender là chuỗi chứ không phải enum (xem Step 3b), nên kiểm bằng cách thử đọc.
        RuleFor(x => x.Gender)
            .Must(value => EnumExtensions.ParseGenderType(value) is not null)
            .When(x => !string.IsNullOrWhiteSpace(x.Gender))
            .WithMessage("Gender must be FEMALE, MALE or OTHER.");

        RuleForEach(x => x.Diseases).SetValidator(new PatientDiseaseInputValidator());
        RuleForEach(x => x.Allergies).SetValidator(new PatientAllergyInputValidator());
    }
}

public sealed class UpdatePatientProfileRequestValidator : AbstractValidator<UpdatePatientProfileRequest>
{
    public UpdatePatientProfileRequestValidator()
    {
        // Bắt buộc ở #18 vì đây là thay toàn bộ: thiếu giới tính là vô tình xoá mất giá trị cũ.
        RuleFor(x => x.Gender)
            .Must(value => EnumExtensions.ParseGenderType(value) is not null)
            .WithMessage("Gender must be FEMALE, MALE or OTHER.");

        RuleForEach(x => x.Diseases).SetValidator(new PatientDiseaseInputValidator());
        RuleForEach(x => x.Allergies).SetValidator(new PatientAllergyInputValidator());
    }
}

public sealed class PatientDiseaseInputValidator : AbstractValidator<PatientDiseaseInput>
{
    public PatientDiseaseInputValidator()
    {
        RuleFor(x => x.DiseaseId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Note must be 500 characters or fewer.");
    }
}

public sealed class PatientAllergyInputValidator : AbstractValidator<PatientAllergyInput>
{
    public PatientAllergyInputValidator()
    {
        RuleFor(x => x.AllergyTypeId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Note must be 500 characters or fewer.");
    }
}
