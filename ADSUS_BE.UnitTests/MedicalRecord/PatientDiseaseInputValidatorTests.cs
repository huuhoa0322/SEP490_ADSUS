using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

// Áp dụng qua RuleForEach trong Create/UpdatePatientProfileRequestValidator, nhưng 2 test file
// đó luôn truyền Diseases rỗng nên chưa từng thực thi validator này — bổ sung độc lập ở đây.
public class PatientDiseaseInputValidatorTests
{
    private readonly PatientDiseaseInputValidator _validator = new();

    private static PatientDiseaseInput ValidRequest() => new(
        DiseaseId: Guid.NewGuid(),
        Note: "Đã điều trị ổn định");

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
    public void EmptyDiseaseId_Fails()
    {
        // Arrange
        var request = ValidRequest() with { DiseaseId = Guid.Empty };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PatientDiseaseInput.DiseaseId));
    }

    [Fact]
    public void NullNote_Passes()
    {
        // Arrange
        var request = ValidRequest() with { Note = null };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Note_501Chars_Fails()
    {
        // Arrange
        var request = ValidRequest() with { Note = new string('A', 501) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PatientDiseaseInput.Note));
    }
}
