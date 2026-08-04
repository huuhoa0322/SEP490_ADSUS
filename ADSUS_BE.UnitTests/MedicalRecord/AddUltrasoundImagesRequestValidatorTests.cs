using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Validators;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class AddUltrasoundImagesRequestValidatorTests
{
    private readonly AddUltrasoundImagesRequestValidator _validator = new();

    private static AddUltrasoundImagesRequest ValidRequest() => new(
        Images: new[] { new UploadedFile("a.png", "image/png", 10, Stream.Null) },
        Note: "Ảnh bổ sung góc nghiêng");

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
    public void EmptyImagesList_Fails()
    {
        // Arrange — NGƯỢC với #20: đặc tả #21 quy định lỗi này trả 400, nên kiểm ở validator
        // là đúng (xem flag N2 trong tài liệu thiết kế).
        var request = ValidRequest() with { Images = Array.Empty<UploadedFile>() };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddUltrasoundImagesRequest.Images));
    }

    [Fact]
    public void Note_1001Chars_Fails()
    {
        // Arrange
        var request = ValidRequest() with { Note = new string('a', 1001) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddUltrasoundImagesRequest.Note));
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
}
