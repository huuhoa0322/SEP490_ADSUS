using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AIDiagnosis.Services;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.AIDiagnosis;

public class CaseDiagnosisRobustnessTests
{
    private readonly AppDbContext _db;
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<IHttpClientFactory> _httpFactoryMock = new();
    private readonly Mock<IAiModelVersionRepository> _modelRepoMock = new();
    private readonly Mock<IUltrasoundImageRepository> _imagesMock = new();
    private readonly Mock<IAiPredictionRepository> _predictionsMock = new();
    private readonly Mock<IDoctorAnnotationRepository> _annotationsMock = new();
    private readonly Mock<ICaseRepository> _casesMock = new();
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<ILogger<CaseDiagnosisService>> _loggerMock = new();
    private readonly CaseDiagnosisService _sut;

    public CaseDiagnosisRobustnessTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _configMock.Setup(c => c["AiBackend:WebhookUrl"]).Returns("http://localhost:8000");

        _sut = new CaseDiagnosisService(new ADSUS_BE.DAL.Repositories.Implementations.UnitOfWork(_db),
            _storageMock.Object,
            _httpFactoryMock.Object,
            _modelRepoMock.Object,
            _imagesMock.Object,
            _predictionsMock.Object,
            _annotationsMock.Object,
            _casesMock.Object,
            _configMock.Object,
            _loggerMock.Object,
            new AiDiagnosisStateTracker()
        );
    }

    [Fact]
    public async Task AnalyzeImageAsync_CaseNotFound_ThrowsResourceNotFoundException()
    {
        var caseId = Guid.NewGuid();
        _casesMock.Setup(c => c.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Case?)null);

        using var stream = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _sut.AnalyzeImageAsync(caseId, stream, "test.png", "image/png", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnalyzeImageAsync_CaseStatusIsBooked_ThrowsBusinessException()
    {
        var caseId = Guid.NewGuid();
        var medicalCase = new Case
        {
            CaseId = caseId,
            Status = CaseStatus.Booked
        };

        _casesMock.Setup(c => c.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);

        using var stream = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _sut.AnalyzeImageAsync(caseId, stream, "test.png", "image/png", TestContext.Current.CancellationToken));

        Assert.Contains("This case has not been checked in yet", ex.Message);
    }

    [Fact]
    public async Task AnalyzeImageAsync_NoActiveAiModel_ThrowsBusinessException()
    {
        var caseId = Guid.NewGuid();
        var medicalCase = new Case
        {
            CaseId = caseId,
            Status = CaseStatus.InProgress
        };

        _casesMock.Setup(c => c.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);

        _modelRepoMock.Setup(m => m.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        using var stream = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _sut.AnalyzeImageAsync(caseId, stream, "test.png", "image/png", TestContext.Current.CancellationToken));

        Assert.Contains("Hệ thống chưa có phiên bản AI nào được kích hoạt", ex.Message);
    }

    [Fact]
    public async Task AnalyzeImageAsync_ImageExceeds20MB_ThrowsBusinessException()
    {
        var caseId = Guid.NewGuid();
        var medicalCase = new Case
        {
            CaseId = caseId,
            Status = CaseStatus.InProgress
        };

        _casesMock.Setup(c => c.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);

        var largeStreamMock = new Mock<Stream>();
        largeStreamMock.SetupGet(s => s.CanSeek).Returns(true);
        largeStreamMock.SetupGet(s => s.Length).Returns(21L * 1024 * 1024);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _sut.AnalyzeImageAsync(caseId, largeStreamMock.Object, "large.png", "image/png", TestContext.Current.CancellationToken));

        Assert.Contains("20MB", ex.Message);
    }
}
