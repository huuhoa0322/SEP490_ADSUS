using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CreatePatientProfileRequestValidatorTests
{
    private readonly CreatePatientProfileRequestValidator _validator = new();

    private static CreatePatientProfileRequest ValidRequest() => new(
        PatientUserId: Guid.NewGuid(),
        Gender: "FEMALE",
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
    public void EmptyPatientUserId_Fails()
    {
        // Arrange
        var request = ValidRequest() with { PatientUserId = Guid.Empty };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientProfileRequest.PatientUserId));
    }

    [Theory]
    [InlineData("FEMALE")]
    [InlineData("MALE")]
    [InlineData("OTHER")]
    [InlineData("female")]
    public void ValidGenderValues_Pass(string gender)
    {
        // Arrange
        var request = ValidRequest() with { Gender = gender };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
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
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreatePatientProfileRequest.Gender));
    }

    [Fact]
    public void NullGender_Passes()
    {
        // Arrange — gender là optional ở #17 (mặc định FEMALE ở tầng Service).
        var request = ValidRequest() with { Gender = null };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

}
