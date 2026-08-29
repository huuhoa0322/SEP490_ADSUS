using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class CaseDiagnosisServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<IAiModelVersionRepository> _aiModelVersionRepoMock = new();
    private readonly Mock<ILogger<CaseDiagnosisService>> _loggerMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly CaseDiagnosisService _sut;
    private readonly Guid _caseId = Guid.NewGuid();
    private readonly Guid _activeModelId = Guid.NewGuid();

    public CaseDiagnosisServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        // Mock Configuration
        var configMock = new Mock<IConfiguration>();
        configMock.SetupGet(c => c["AiBackend:WebhookUrl"]).Returns("http://localhost:8000");

        // Mock HttpClientFactory
        var client = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // P11 review (29/08/2026): CaseDiagnosisService không còn ghi thẳng qua _db cho
        // UltrasoundImage/AiPrediction/DoctorAnnotation — bọc 3 repository thật quanh cùng
        // _db InMemory để giữ nguyên các assertion cũ (_db.UltrasoundImages, _db.AiPredictions,
        // _db.DoctorAnnotations). IAiModelVersionRepository vẫn giữ Mock thuần (không backed by
        // _db) vì một số test cần kiểm soát trực tiếp việc SaveChangesAsync thành công/thất bại.
        _sut = new CaseDiagnosisService(
            _db,
            _storageMock.Object,
            _httpClientFactoryMock.Object,
            _aiModelVersionRepoMock.Object,
            new UltrasoundImageRepository(_db),
            new AiPredictionRepository(_db),
            new DoctorAnnotationRepository(_db),
            configMock.Object,
            _loggerMock.Object
        );
    }

    public void Dispose()
    {
        try { _db.Database.EnsureDeleted(); } catch { }
        _db.Dispose();
    }

    private Stream MakeFakeImageStream() => new MemoryStream(new byte[] { 1, 2, 3 });

    // =========================================================================
    // AnalyzeImageAsync
    // =========================================================================

    [Fact]
    public async Task AnalyzeImageAsync_NoActiveModel_ThrowsBusinessException()
    {
        // Arrange
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _sut.AnalyzeImageAsync(_caseId, MakeFakeImageStream(), "test.png", "image/png"));
        Assert.Contains("Hệ thống chưa có phiên bản AI nào được kích hoạt", ex.Message);
    }

    [Fact]
    public async Task AnalyzeImageAsync_AiBackendReturnsError_ThrowsBusinessException()
    {
        // Arrange
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion { ModelVersionId = _activeModelId, HfRepoId = "repo", HfFilename = "file.pt" });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Model Server Down")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _sut.AnalyzeImageAsync(_caseId, MakeFakeImageStream(), "test.png", "image/png"));
        Assert.Contains("Lỗi từ hệ thống AI: Model Server Down", ex.Message);
    }

    [Fact]
    public async Task AnalyzeImageAsync_AiBackendReturnsInvalidJson_ThrowsJsonException()
    {
        // Arrange
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion { ModelVersionId = _activeModelId, HfRepoId = "repo", HfFilename = "file.pt" });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("invalid json body")
            });

        // Act & Assert
        await Assert.ThrowsAnyAsync<JsonException>(
            () => _sut.AnalyzeImageAsync(_caseId, MakeFakeImageStream(), "test.png", "image/png"));
    }

    [Fact]
    public async Task AnalyzeImageAsync_AiBackendTimeouts_ThrowsTaskCanceledException()
    {
        // Arrange - Bao phủ ngoại lệ Timeout/Network lỗi từ HttpClient
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion { ModelVersionId = _activeModelId, HfRepoId = "repo", HfFilename = "file.pt" });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _sut.AnalyzeImageAsync(_caseId, MakeFakeImageStream(), "test.png", "image/png"));
    }

    [Fact]
    public async Task AnalyzeImageAsync_Success_ReturnsJsonElement()
    {
        // Arrange
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion { ModelVersionId = _activeModelId, HfRepoId = "repo", HfFilename = "file.pt" });

        var validJson = "[{\"xmin\":1,\"ymin\":2,\"xmax\":3,\"ymax\":4,\"confidence\":0.9}]";
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(validJson)
            });

        // Act
        var result = await _sut.AnalyzeImageAsync(_caseId, MakeFakeImageStream(), "test.png", "image/png");

        // Assert
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(1, result.GetArrayLength());
        // Verify DB was not touched (no ultrasound image created yet)
        Assert.Empty(_db.UltrasoundImages);
    }

    // =========================================================================
    // ConfirmAnalysisAsync
    // =========================================================================

    private ConfirmAnalysisRequest MakeValidConfirmRequest(string aiJson = "[]", string docJson = "[]")
    {
        return new ConfirmAnalysisRequest
        {
            OriginalImageStream = MakeFakeImageStream(),
            OriginalImageFileName = "orig.png",
            OriginalImageContentType = "image/png",
            BurntImageStream = MakeFakeImageStream(),
            BurntImageFileName = "burnt.png",
            BurntImageContentType = "image/png",
            AiPredictionsJson = aiJson,
            DoctorAnnotationsJson = docJson,
            Note = "Test note"
        };
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_NoActiveModel_ThrowsBusinessException()
    {
        // Arrange
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _sut.ConfirmAnalysisAsync(_caseId, MakeValidConfirmRequest()));
        Assert.Contains("Hệ thống chưa có phiên bản AI nào được kích hoạt", ex.Message);
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_InvalidJsonInput_ThrowsJsonException()
    {
        // Arrange
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion { ModelVersionId = _activeModelId });

        var request = MakeValidConfirmRequest(aiJson: "invalid-json");

        // Act & Assert
        await Assert.ThrowsAnyAsync<JsonException>(
            () => _sut.ConfirmAnalysisAsync(_caseId, request));
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_StorageUploadFails_ThrowsExceptionAndNoDbChanges()
    {
        // Arrange
        _storageMock.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("S3 Bucket down"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.ConfirmAnalysisAsync(_caseId, MakeValidConfirmRequest()));
        
        Assert.Equal("S3 Bucket down", ex.Message);
        Assert.Empty(_db.UltrasoundImages); // Transaction not started
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_DbFails_RollbacksAndThrows()
    {
        // Arrange — mô phỏng lỗi ghi DB ở bước cuối (lưu chỉ số AiModelVersion) bằng cách cho
        // SaveChangesAsync ném lỗi trực tiếp qua Mock. (Trước refactor P11 29/08/2026, test này
        // dựa vào việc code gọi _db.AiModelVersions.Update(activeModel) trực tiếp để cố ý tạo
        // tracking-conflict giữa 2 object cùng khoá — dòng Update() dư thừa đó đã bị gỡ, nên kỹ
        // thuật cũ không còn tái hiện được lỗi; mock throw trực tiếp phản ánh đúng ý định gốc:
        // "DB lỗi ở bước cuối thì exception phải lan ra ngoài, không bị nuốt".)
        var activeModel = new AiModelVersion { ModelVersionId = _activeModelId, LiveTp = 0, LiveFp = 0, LiveFn = 0, VersionCode = "v1", HfRepoId = "repo", HfFilename = "file" };
        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeModel);
        _aiModelVersionRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB save failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ConfirmAnalysisAsync(_caseId, MakeValidConfirmRequest()));
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_Success_UpdatesMetricsAndCommits()
    {
        // Arrange
        var activeModel = new AiModelVersion { ModelVersionId = _activeModelId, LiveTp = 0, LiveFp = 0, LiveFn = 0, VersionCode = "v1", HfRepoId = "repo", HfFilename = "file" };
        _db.AiModelVersions.Add(activeModel);
        await _db.SaveChangesAsync();

        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeModel);

        // Setup AI json with 2 boxes, Doc json with 2 boxes
        // Box 1: Exact match -> TP
        // Box 2: AI only -> FP
        // Box 3: Doc only -> FN
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.9}, {\"xmin\":200,\"ymin\":200,\"xmax\":300,\"ymax\":300,\"confidence\":0.8}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}, {\"xmin\":400,\"ymin\":400,\"xmax\":500,\"ymax\":500}]";
        
        var request = MakeValidConfirmRequest(aiJson, docJson);

        // Act
        await _sut.ConfirmAnalysisAsync(_caseId, request);

        // Assert
        // Storage should be called 3 times (original, yolo txt, burnt)
        _storageMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        // Verify Database Records
        var image = await _db.UltrasoundImages.SingleAsync();
        Assert.Equal(_caseId, image.CaseId);
        
        var aiPreds = await _db.AiPredictions.ToListAsync();
        Assert.Equal(2, aiPreds.Count);

        var docAnns = await _db.DoctorAnnotations.ToListAsync();
        Assert.Equal(2, docAnns.Count);

        // Verify Metrics (TP=1, FP=1, FN=1)
        Assert.Equal(1, activeModel.LiveTp);
        Assert.Equal(1, activeModel.LiveFp);
        Assert.Equal(1, activeModel.LiveFn);
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_DoubleMatch_PreventsDoubleCountTP()
    {
        // Arrange
        var activeModel = new AiModelVersion { ModelVersionId = _activeModelId, LiveTp = 0, LiveFp = 0, LiveFn = 0, VersionCode = "v1", HfRepoId = "repo", HfFilename = "file" };
        _db.AiModelVersions.Add(activeModel);
        await _db.SaveChangesAsync();

        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeModel);

        // AI predicts 2 boxes very close to 1 Doc box.
        // Doc box: 0,0,100,100
        // AI box 1: 0,0,100,100 (IoU=1.0)
        // AI box 2: 0,0,95,95 (IoU=0.9)
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.9}, {\"xmin\":0,\"ymin\":0,\"xmax\":95,\"ymax\":95,\"confidence\":0.8}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";
        
        var request = MakeValidConfirmRequest(aiJson, docJson);

        // Act
        await _sut.ConfirmAnalysisAsync(_caseId, request);

        // Assert
        // Only 1 TP because matchedGtIndices prevents double counting.
        // The other AI box becomes an FP.
        Assert.Equal(1, activeModel.LiveTp);
        Assert.Equal(1, activeModel.LiveFp);
        Assert.Equal(0, activeModel.LiveFn);
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_EmptyDoctorAnnotations_CountsAllAiAsFp()
    {
        // Arrange
        var activeModel = new AiModelVersion { ModelVersionId = _activeModelId, LiveTp = 0, LiveFp = 0, LiveFn = 0, VersionCode = "v1", HfRepoId = "repo", HfFilename = "file" };
        _db.AiModelVersions.Add(activeModel);
        await _db.SaveChangesAsync();

        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeModel);

        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.9}]";
        var docJson = "[]"; // Doctor deletes all annotations
        
        var request = MakeValidConfirmRequest(aiJson, docJson);

        // Act
        await _sut.ConfirmAnalysisAsync(_caseId, request);

        // Assert
        Assert.Equal(0, activeModel.LiveTp);
        Assert.Equal(1, activeModel.LiveFp);
        Assert.Equal(0, activeModel.LiveFn); // Fn should be 0 because doc has 0 boxes
    }

    [Fact]
    public async Task ConfirmAnalysisAsync_EmptyAiPredictions_CountsAllDocAsFn()
    {
        // Arrange - Bao phủ logic khi AI không dự đoán được gì nhưng bác sĩ lại vẽ tay (False Negative hoàn toàn)
        var activeModel = new AiModelVersion { ModelVersionId = _activeModelId, LiveTp = 0, LiveFp = 0, LiveFn = 0, VersionCode = "v1", HfRepoId = "repo", HfFilename = "file" };
        _db.AiModelVersions.Add(activeModel);
        await _db.SaveChangesAsync();

        _aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeModel);

        var aiJson = "[]"; // AI returns nothing
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]"; // Doc annotates 1 box
        
        var request = MakeValidConfirmRequest(aiJson, docJson);

        // Act
        await _sut.ConfirmAnalysisAsync(_caseId, request);

        // Assert
        Assert.Equal(0, activeModel.LiveTp);
        Assert.Equal(0, activeModel.LiveFp);
        Assert.Equal(1, activeModel.LiveFn); // Fn should be 1
    }
}
