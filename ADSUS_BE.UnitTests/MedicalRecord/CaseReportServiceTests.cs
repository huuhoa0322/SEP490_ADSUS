using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CaseReportServiceTests
{
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly CaseReportService _sut;

    static CaseReportServiceTests()
    {
        // QuestPDF.Settings.License chỉ được set trong ADSUS_BE/Program.cs, không chạy trong
        // process dotnet test — set riêng ở đây, không đụng tới CaseReportService.cs.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public CaseReportServiceTests()
    {
        _sut = new CaseReportService(_cases.Object, Mock.Of<ILogger<CaseReportService>>());
    }

    [Fact]
    public async Task GenerateReportAsync_ConfirmedCase_ReturnsNonEmptyPdfBytes()
    {
        // Arrange
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);
        MedicalRecordTestData.MakePrescription(
            medicalCase, medicalCase.VisitDate, DateTime.UtcNow);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert — không parse nội dung PDF ở Unit Test (đã có Integration/manual test làm
        // việc đó); chỉ khẳng định QuestPDF thực sự sinh ra file PDF hợp lệ, không rỗng.
        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Fact]
    public async Task GenerateReportAsync_CaseNotYetConfirmed_ThrowsBusinessException()
    {
        // Arrange — AF-01/BR-01: chỉ ca CONFIRMED mới xuất được báo cáo.
        var pendingCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Created);
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
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(medicalCase);

        // Act
        var pdfBytes = await _sut.GenerateReportAsync(medicalCase.CaseId);

        // Assert
        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public async Task GenerateReportAsync_ConfirmedCase_EmbedsVietnameseCapableFontNotArial()
    {
        // Arrange — khoá lại fix rủi ro font đã ghi ở finding #5 review: server thật deploy
        // trên Render (Linux), không có Arial cài sẵn — QuestPDF phải tự nhúng Noto Sans
        // (đăng ký qua FontManager.RegisterFont), không phụ thuộc font có sẵn trên OS host.
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.Confirmed);
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
