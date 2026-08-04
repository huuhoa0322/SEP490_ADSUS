using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class UltrasoundImageContentValidatorTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
    private static readonly byte[] FakeJpegBytes = "%PDF-1.4 not really a jpeg"u8.ToArray();

    private static UploadedFile MakeFile(byte[] content, string fileName = "anh.jpg") =>
        new(fileName, "image/jpeg", content.Length, new MemoryStream(content));

    [Fact]
    public async Task ValidateAndResolveContentTypeAsync_RealJpegBytes_ReturnsImageJpeg()
    {
        // Arrange
        var file = MakeFile(JpegBytes);

        // Act
        var contentType = await UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(file);

        // Assert
        Assert.Equal("image/jpeg", contentType);
    }

    [Fact]
    public async Task ValidateAndResolveContentTypeAsync_RealPngBytes_ReturnsImagePng()
    {
        // Arrange
        var file = MakeFile(PngBytes, "anh.png");

        // Act
        var contentType = await UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(file);

        // Assert
        Assert.Equal("image/png", contentType);
    }

    [Fact]
    public async Task ValidateAndResolveContentTypeAsync_FileRenamedToJpgButNotAnImage_ThrowsBusinessException()
    {
        // Arrange — đúng kịch bản BR-01: đổi tên file giả thành .jpg, ContentType client gửi
        // vẫn khai "image/jpeg", nhưng nội dung thật không phải JPEG/PNG.
        var file = MakeFile(FakeJpegBytes);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(file));
        Assert.Contains("not a JPEG or PNG image", ex.Message);
    }

    [Fact]
    public async Task ValidateAndResolveContentTypeAsync_EmptyFile_ThrowsBusinessException()
    {
        // Arrange
        var file = MakeFile(Array.Empty<byte>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(file));
        Assert.Contains("is empty", ex.Message);
    }

    [Fact]
    public async Task ValidateAndResolveContentTypeAsync_OverSizeLimit_ThrowsBusinessException()
    {
        // Arrange — Length khai 21MB, không cần dữ liệu byte thật vì kiểm Length trước khi đọc.
        var file = new UploadedFile(
            "anh_lon.png", "image/png", UltrasoundImageContentValidator.MaxFileSizeBytes + 1, Stream.Null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(file));
        Assert.Contains("20MB limit", ex.Message);
    }

    [Fact]
    public async Task ValidateAndResolveContentTypeAsync_ValidFile_ResetsStreamPositionToStart()
    {
        // Arrange — quan trọng: bước đọc magic-byte không được để lại luồng đã bị "ăn" 8 byte
        // đầu, nếu không bước upload sau đó sẽ đẩy lên thiếu dữ liệu.
        var file = MakeFile(PngBytes, "anh.png");

        // Act
        await UltrasoundImageContentValidator.ValidateAndResolveContentTypeAsync(file);

        // Assert
        Assert.Equal(0, file.Content.Position);
    }
}
