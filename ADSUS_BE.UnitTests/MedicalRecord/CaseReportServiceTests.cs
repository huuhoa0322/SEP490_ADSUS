using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.DAL.ExternalServices;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net.Http;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CaseReportServiceTests
{
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly CaseReportService _sut;

    static CaseReportServiceTests()
    {
        // QuestPDF.Settings.License chỉ được set trong ADSUS_BE/Program.cs, không chạy trong
        // process dotnet test — set riêng ở đây, không đụng tới CaseReportService.cs.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public CaseReportServiceTests()
    {
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        _sut = new CaseReportService(
            _cases.Object, 
            _storage.Object, 
            _httpClientFactory.Object, 
            Mock.Of<ILogger<CaseReportService>>());
    }


    [Fact]
    public async Task GenerateReportAsync_EndCase_ReturnsNonEmptyPdfBytes()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert
        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Fact]
    public async Task GenerateReportAsync_WithImages_EmbedsImagesSuccessfully()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        medicalCase.UltrasoundImages.Add(new UltrasoundImage { FileRef = "image1.jpg", Note = "Gan nhiễm mỡ" });
        medicalCase.UltrasoundImages.Add(new UltrasoundImage { FileRef = "image2.jpg", Note = null });
        
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
              
        _storage.Setup(s => s.CreateSignedUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://dummy-url.com/image.jpg");

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert
        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
        _storage.Verify(s => s.CreateSignedUrlAsync("image1.jpg", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.CreateSignedUrlAsync("image2.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_ImageSignedUrlIsNull_StillGeneratesPdfWithoutCrashing()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        medicalCase.UltrasoundImages.Add(new UltrasoundImage { FileRef = "missing.jpg" });
        
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
              
        // Giả lập storage không ký được URL (file không tồn tại)
        _storage.Setup(s => s.CreateSignedUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert - Vẫn xuất được PDF bình thường
        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Fact]
    public async Task GenerateReportAsync_ImageDownloadThrowsException_LogsWarningAndGeneratesPdf()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        medicalCase.UltrasoundImages.Add(new UltrasoundImage { FileRef = "broken-link.jpg" });
        
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);
              
        _storage.Setup(s => s.CreateSignedUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://dummy-url.com/broken.jpg");

        // Giả lập HttpClient ném lỗi (ví dụ 404, hoặc timeout)
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert - Vẫn xuất được PDF bình thường
        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Theory]
    [InlineData(CaseStatus.Created)]
    [InlineData(CaseStatus.Confirmed)]
    public async Task GenerateReportAsync_CaseNotYetEnded_ThrowsBusinessException(CaseStatus incompleteStatus)
    {
        // Arrange — AF-01/BR-01: chỉ ca END mới xuất được báo cáo.
        var pendingCase = MedicalRecordTestData.MakeCase(status: incompleteStatus);
        _cases.Setup(r => r.GetDetailAsync(pendingCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(pendingCase);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _sut.GenerateReportAsync(pendingCase.CaseId));
    }

    [Fact]
    public async Task GenerateReportAsync_CaseNotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        _cases.Setup(r => r.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Case?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => _sut.GenerateReportAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GenerateReportAsync_CaseWithNoPrescription_StillGeneratesPdfWithoutThrowing()
    {
        // Arrange — nhánh "Không có đơn thuốc cho lần khám này." trong BuildPdf.
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert
        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public async Task GenerateReportAsync_EndCase_EmbedsVietnameseCapableFontNotArial()
    {
        // Arrange — khoá lại fix rủi ro font đã ghi ở finding #5 review: server thật deploy
        // trên Render (Linux), không có Arial cài sẵn — QuestPDF phải tự nhúng Noto Sans
        // (đăng ký qua FontManager.RegisterFont), không phụ thuộc font có sẵn trên OS host.
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert — tên font PostScript được PDF nhúng vào /BaseFont đọc được trực tiếp từ
        // byte thô (không cần thư viện parse PDF); Latin1 an toàn vì mọi ký tự trong PDF
        // dictionary đều nằm trong dải byte đơn.
        var pdfText = System.Text.Encoding.Latin1.GetString(pdfBytes);
        Assert.Contains("NotoSans", pdfText);
        Assert.DoesNotContain("Arial", pdfText);
    }
}
