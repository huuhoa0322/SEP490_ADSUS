using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class UpdatePatientProfileRequestValidatorTests
{
    private readonly UpdatePatientProfileRequestValidator _validator = new();

    private static UpdatePatientProfileRequest ValidRequest() => new(
        Gender: "MALE",
        Diseases: new System.Collections.Generic.List<PatientDiseaseInput>(),
        Allergies: new System.Collections.Generic.List<PatientAllergyInput>());

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
    public void EmptyGenderString_Fails()
    {
        // Arrange — #18 là thay toàn bộ, Gender bắt buộc (không như #17).
        var request = ValidRequest() with { Gender = "" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientProfileRequest.Gender));
    }

    [Fact]
    public void InvalidGenderString_Fails()
    {
        // Arrange
        var request = ValidRequest() with { Gender = "UNKNOWN" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdatePatientProfileRequest.Gender));
    }


}
