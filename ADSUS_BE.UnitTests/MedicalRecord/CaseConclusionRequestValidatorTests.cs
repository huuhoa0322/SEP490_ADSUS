using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

// Validator này dùng chung cho cả luồng "Lưu kết luận" và "Kết thúc ca khám" (xem comment gốc
// tại CaseRequestValidators.cs) — chưa có test nào trước đợt P12 29/08/2026 dù cả 2 field đều
// bắt buộc.
public class CaseConclusionRequestValidatorTests
{
    private readonly CaseConclusionRequestValidator _validator = new();

    private static CaseConclusionRequest ValidRequest() => new(
        FinalDiagnosis: "U tuyến xơ vú phải (BI-RADS 3)",
        DoctorConclusion: "Theo dõi định kỳ sau 6 tháng");

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyFinalDiagnosis_Fails()
    {
        // Arrange
        var request = ValidRequest() with { FinalDiagnosis = "" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CaseConclusionRequest.FinalDiagnosis));
    }

    [Fact]
    public void FinalDiagnosis_5001Chars_Fails()
    {
        // Arrange
        var request = ValidRequest() with { FinalDiagnosis = new string('A', 5001) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CaseConclusionRequest.FinalDiagnosis));
    }

    [Fact]
    public void EmptyDoctorConclusion_Fails()
    {
        // Arrange
        var request = ValidRequest() with { DoctorConclusion = "" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CaseConclusionRequest.DoctorConclusion));
    }

    [Fact]
    public void DoctorConclusion_5001Chars_Fails()
    {
        // Arrange
        var request = ValidRequest() with { DoctorConclusion = new string('A', 5001) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CaseConclusionRequest.DoctorConclusion));
    }
}
