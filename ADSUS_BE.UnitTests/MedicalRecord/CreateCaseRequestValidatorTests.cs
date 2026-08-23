using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CreateCaseRequestValidatorTests
{
    private readonly CreateCaseRequestValidator _validator = new();

    private static CreateCaseRequest ValidRequest() => new(
        PatientProfileId: Guid.NewGuid(),
        ResponsibleDoctorId: Guid.NewGuid(),
        ClinicalInfo: "Đau tức vú trái",
        Symptoms: null,
        // Số lượng ảnh KHÔNG được validator này kiểm (thuộc CaseService.CreateAsync) — vẫn
        // cần 1 phần tử ở đây để không trộn hai mối quan tâm khác nhau vào cùng 1 test.
        Images: new[] { new UploadedFile("a.png", "image/png", 10, Stream.Null) });

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
    public void EmptyPatientProfileId_Fails()
    {
        // Arrange
        var request = ValidRequest() with { PatientProfileId = Guid.Empty };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCaseRequest.PatientProfileId));
    }

    [Fact]
    public void EmptyResponsibleDoctorId_Fails()
    {
        // Arrange
        var request = ValidRequest() with { ResponsibleDoctorId = Guid.Empty };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCaseRequest.ResponsibleDoctorId));
    }

    [Fact]
    public void ClinicalInfo_5001Chars_Fails()
    {
        // Arrange
        var request = new CreateCaseRequest(
            Guid.Empty, Guid.Empty, new string('A', 5001), null, new List<UploadedFile>());

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCaseRequest.ClinicalInfo));
    }

    [Fact]
    public void EmptyImagesList_StillPassesHereByDesign()
    {
        // Arrange — cố ý: đặc tả #20 quy định lỗi "chưa có ảnh" trả 422, mà FluentValidation
        // luôn cho ra 400, nên luật này nằm ở CaseService.CreateAsync, KHÔNG ở validator này.
        var request = ValidRequest() with { Images = Array.Empty<UploadedFile>() };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
