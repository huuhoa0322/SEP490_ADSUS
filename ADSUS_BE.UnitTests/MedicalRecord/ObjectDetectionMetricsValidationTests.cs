using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AIDiagnosis.Services;
using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Services;

/// <summary>
/// Comprehensive, forensic-grade verification suite for Object Detection Metrics Standardization.
/// Covers:
/// - T1: Feature Coverage (Confidence descending sort, 1:1 match, greedy matching with candidate fallback).
/// - T2: Boundary & Corner Cases (0 AI, 0 GT, exact 0.50 threshold, sub/super-threshold, confidence inversion).
/// - T3: Lifecycle & Recalculation (Caliper update idempotence, live metrics resync from DB history).
/// - T4: Missing Ground Truth Bug (0-prediction images counted in totalGt, eliminating false 100% recall).
/// - Sentinel AiPrediction records: Confidence = 0m ignored in matching and metric calculations.
/// </summary>
public class ObjectDetectionMetricsValidationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IFileStorageService> _storageMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<ILogger<CaseDiagnosisService>> _loggerMock = new();
    private readonly AiModelVersionRepository _modelVersionRepo;
    private readonly AiPredictionRepository _predictionRepo;
    private readonly DoctorAnnotationRepository _annotationRepo;
    private readonly CaseDiagnosisService _caseDiagnosisService;
    private readonly AiMetricsService _aiMetricsService;

    private readonly Guid _caseId = Guid.NewGuid();
    private readonly Guid _activeModelId = Guid.NewGuid();

    public ObjectDetectionMetricsValidationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options);

        // Seed InProgress Case for check-in gate
        _db.Cases.Add(new Case
        {
            CaseId = _caseId,
            PatientProfileId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = CaseStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // Seed Active AI Model Version
        var activeModel = new AiModelVersion
        {
            ModelVersionId = _activeModelId,
            VersionCode = "YOLO_TEST_v1",
            Status = ModelVersionStatus.Active,
            HfRepoId = "org/repo",
            HfFilename = "model.pt",
            LiveTp = 0,
            LiveFp = 0,
            LiveFn = 0,
            LiveMap50 = 0m,
            RegisteredAt = DateTime.UtcNow
        };
        _db.AiModelVersions.Add(activeModel);
        _db.SaveChanges();

        // Repositories
        _modelVersionRepo = new AiModelVersionRepository(_db);
        _predictionRepo = new AiPredictionRepository(_db);
        _annotationRepo = new DoctorAnnotationRepository(_db);
        var imageRepo = new UltrasoundImageRepository(_db);
        var caseRepo = new CaseRepository(_db);

        // Mock Config
        var configMock = new Mock<IConfiguration>();
        configMock.SetupGet(c => c["AiBackend:WebhookUrl"]).Returns("http://localhost:8000");

        // Mock HttpClientFactory
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        _caseDiagnosisService = new CaseDiagnosisService(new ADSUS_BE.DAL.Repositories.Implementations.UnitOfWork(_db),
            _storageMock.Object,
            _httpClientFactoryMock.Object,
            _modelVersionRepo,
            imageRepo,
            _predictionRepo,
            _annotationRepo,
            caseRepo,
            configMock.Object,
            _loggerMock.Object,
            new AiDiagnosisStateTracker()
        );

        _aiMetricsService = new AiMetricsService(
            _modelVersionRepo,
            _predictionRepo,
            _annotationRepo
        );
    }

    public void Dispose()
    {
        try { _db.Database.EnsureDeleted(); } catch { }
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ConfirmAnalysisAsync kiểm tra magic bytes (NFR-SEC-07) — byte giả { 1, 2, 3 } sẽ bị từ chối.
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

    private static ConfirmAnalysisRequest CreateConfirmRequest(
        string aiPredictionsJson,
        string doctorAnnotationsJson,
        string? burntFileName = null,
        string? originalFileName = null)
    {
        return new ConfirmAnalysisRequest
        {
            OriginalImageStream = new MemoryStream(PngBytes),
            OriginalImageContentType = "image/png",
            OriginalImageFileName = originalFileName ?? "ultrasound_original.png",
            BurntImageStream = new MemoryStream(PngBytes),
            BurntImageContentType = "image/png",
            BurntImageFileName = burntFileName ?? "ultrasound_burnt.png",
            AiPredictionsJson = aiPredictionsJson,
            DoctorAnnotationsJson = doctorAnnotationsJson,
            Note = "Object detection validation test"
        };
    }

    // =========================================================================
    // T1: Feature Coverage Tests
    // =========================================================================

    [Fact]
    public async Task T1_01_OneToOne_PerfectMatch_CalculatesTruePositiveCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        // 1 AI box and 1 Doctor GT box at identical coordinates [0, 0, 100, 100] -> IoU = 1.0
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.95}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);

        // Verify Batch Recalculation also calculates 100% mAP50
        await _aiMetricsService.CalculateMap50Async(_activeModelId, ct);
        await _db.Entry(model).ReloadAsync(ct);

        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
        Assert.Equal(100.0m, model.LiveMap50);
    }

    [Fact]
    public async Task T1_02_ConfidenceDescendingOrdering_PriorityForHigherConfidenceMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        // AI predictions arrive in arbitrary order: Low confidence (0.40) first, High confidence (0.95) second.
        // Doctor has only 1 ground truth box.
        // The algorithm MUST evaluate confidence descending: 0.95 gets evaluated first and claims GT.
        // 0.40 is evaluated second, finds GT already claimed, and becomes FP.
        var aiJson = @"[
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100,""confidence"":0.40},
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100,""confidence"":0.95}
        ]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(1, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
    }

    [Fact]
    public async Task T1_03_GreedyIoUMatching_WithCandidateFallback_MatchesSecondaryUnassignedDoctorBox()
    {
        var ct = TestContext.Current.CancellationToken;
        // Demonstrates the critical Greedy Fallback fix:
        // Doctor GT1: [0, 0, 100, 100] (Area 10000)
        // Doctor GT2: [50, 0, 150, 100] (Area 10000)
        //
        // AI Pred 1: [0, 0, 100, 100], Conf 0.95 -> IoU with GT1 is 1.0 -> claims GT1.
        // AI Pred 2: [20, 0, 120, 100], Conf 0.85
        //   IoU(Pred2, GT1): Inter = [20..100, 0..100] = 80*100 = 8000. Union = 12000. IoU = 8000/12000 = 0.667.
        //   IoU(Pred2, GT2): Inter = [50..120, 0..100] = 70*100 = 7000. Union = 13000. IoU = 7000/13000 = 0.538.
        //
        // In buggy greedy matching without fallback:
        //   Pred 2 sees highest IoU overall is GT1 (0.667). GT1 is already claimed by Pred 1.
        //   Without fallback, it immediately dropped Pred 2 to FP, leaving GT2 as FN (TP=1, FP=1, FN=1).
        // With greedy fallback:
        //   GT1 is skipped because it is claimed. Pred 2 evaluates unassigned GT2 (IoU = 0.538 >= 0.5) and matches it!
        //   Result: TP=2, FP=0, FN=0!
        var aiJson = @"[
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100,""confidence"":0.95},
            {""xmin"":20,""ymin"":0,""xmax"":120,""ymax"":100,""confidence"":0.85}
        ]";
        var docJson = @"[
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100},
            {""xmin"":50,""ymin"":0,""xmax"":150,""ymax"":100}
        ]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(2, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);

        // Verify batch evaluation also implements greedy fallback identically
        await _aiMetricsService.CalculateMap50Async(_activeModelId, ct);
        await _db.Entry(model).ReloadAsync(ct);

        Assert.Equal(2, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
        Assert.Equal(100.0m, model.LiveMap50);
    }

    [Fact]
    public async Task T1_04_DuplicateDetections_MultiplePredictionsAroundSingleGroundTruth()
    {
        var ct = TestContext.Current.CancellationToken;
        // 3 AI predictions around a single Doctor ground truth:
        // Conf 0.90, 0.80, 0.70 all overlap GT with IoU > 0.5.
        // Only the highest confidence detection (0.90) gets TP. The remaining 2 become FP.
        var aiJson = @"[
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100,""confidence"":0.90},
            {""xmin"":5,""ymin"":5,""xmax"":95,""ymax"":95,""confidence"":0.80},
            {""xmin"":10,""ymin"":10,""xmax"":90,""ymax"":90,""confidence"":0.70}
        ]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(2, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
    }

    // =========================================================================
    // T2: Boundary & Corner Case Tests
    // =========================================================================

    [Fact]
    public async Task T2_01_ZeroAiPredictions_WithPositiveGroundTruth_YieldsZeroFpAndCorrectFn()
    {
        var ct = TestContext.Current.CancellationToken;
        // AI predicts 0 boxes. Doctor annotates 3 lesions.
        // Expected: TP=0, FP=0, FN=3.
        // LivePrecision: null (0/0), LiveRecall: 0 / (0 + 3) = 0.0 (NOT 100%).
        var aiJson = "[]";
        var docJson = @"[
            {""xmin"":0,""ymin"":0,""xmax"":50,""ymax"":50},
            {""xmin"":60,""ymin"":0,""xmax"":110,""ymax"":50},
            {""xmin"":120,""ymin"":0,""xmax"":170,""ymax"":50}
        ]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(0, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(3, model.LiveFn);

        // Test DTO mathematical properties
        var dto = new AiModelVersionDto
        {
            LiveTp = model.LiveTp,
            LiveFp = model.LiveFp,
            LiveFn = model.LiveFn
        };
        Assert.Null(dto.LivePrecision); // 0 / (0 + 0) -> null
        Assert.Equal(0.0m, dto.LiveRecall); // 0 / (0 + 3) = 0.0m
    }

    [Fact]
    public async Task T2_02_ZeroGroundTruth_WithPositiveAiPredictions_YieldsAllFalsePositives()
    {
        var ct = TestContext.Current.CancellationToken;
        // Normal ultrasound (0 doctor annotations). AI produces 2 false alarms.
        // Expected: TP=0, FP=2, FN=0.
        // LivePrecision: 0 / (0 + 2) = 0.0m, LiveRecall: null (0 / (0 + 0)).
        var aiJson = @"[
            {""xmin"":10,""ymin"":10,""xmax"":60,""ymax"":60,""confidence"":0.85},
            {""xmin"":70,""ymin"":10,""xmax"":120,""ymax"":60,""confidence"":0.75}
        ]";
        var docJson = "[]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(0, model.LiveTp);
        Assert.Equal(2, model.LiveFp);
        Assert.Equal(0, model.LiveFn);

        var dto = new AiModelVersionDto
        {
            LiveTp = model.LiveTp,
            LiveFp = model.LiveFp,
            LiveFn = model.LiveFn
        };
        Assert.Equal(0.0m, dto.LivePrecision);
        Assert.Null(dto.LiveRecall);
    }

    [Fact]
    public async Task T2_03_NormalUltrasound_ZeroAiPredictions_AndZeroGroundTruth()
    {
        var ct = TestContext.Current.CancellationToken;
        // Normal ultrasound confirmed normal by doctor: 0 AI predictions, 0 Doctor annotations.
        // Expected: TP=0, FP=0, FN=0.
        // Both precision and recall denominators are 0 -> both null.
        var aiJson = "[]";
        var docJson = "[]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(0, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);

        var dto = new AiModelVersionDto
        {
            LiveTp = model.LiveTp,
            LiveFp = model.LiveFp,
            LiveFn = model.LiveFn
        };
        Assert.Null(dto.LivePrecision);
        Assert.Null(dto.LiveRecall);
    }

    [Fact]
    public async Task T2_04_ExactIoUThreshold_PointFifty_ClassifiedAsTruePositive()
    {
        var ct = TestContext.Current.CancellationToken;
        // Exact 0.50000000 boundary match test:
        // AI Box: [0, 0, 100, 100] (Area = 10000)
        // Doc Box: [0, 0, 100, 200] (Area = 20000)
        // Intersection = [0..100, 0..100] = 10000.
        // Union = 10000 + 20000 - 10000 = 20000.
        // IoU = 10000 / 20000 = 0.50000000.
        // IoU >= 0.50 MUST be accepted as True Positive.
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.90}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":200}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
    }

    [Fact]
    public async Task T2_05_SubThresholdBoundary_PointFourNineNineNine_ClassifiedAsFalsePositive()
    {
        var ct = TestContext.Current.CancellationToken;
        // Sub-threshold boundary test (IoU = 0.499975... < 0.50):
        // AI Box: [0, 0, 100, 100] (Area = 10000)
        // Doc Box: [0, 0, 100, 200.01] (Area = 20001)
        // Inter = 10000. Union = 20001. IoU = 10000 / 20001 = 0.499975.
        // Must strictly reject match -> TP=0, FP=1, FN=1.
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.90}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":200.01}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(0, model.LiveTp);
        Assert.Equal(1, model.LiveFp);
        Assert.Equal(1, model.LiveFn);
    }

    [Fact]
    public async Task T2_06_SuperThresholdBoundary_PointFiveZeroZeroOne_ClassifiedAsTruePositive()
    {
        var ct = TestContext.Current.CancellationToken;
        // Super-threshold boundary test (IoU = 0.500025... >= 0.50):
        // AI Box: [0, 0, 100, 100] (Area = 10000)
        // Doc Box: [0, 0, 100, 199.99] (Area = 19999)
        // Inter = 10000. Union = 19999. IoU = 10000 / 19999 = 0.500025.
        // Must accept match -> TP=1, FP=0, FN=0.
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.90}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":199.99}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
    }

    [Fact]
    public async Task T2_07_ConfidenceInversion_LowConfidenceFirstInInput_PreservesCorrectPriorities()
    {
        var ct = TestContext.Current.CancellationToken;
        // Inverted confidence scenario:
        // Input array has lower confidence prediction (0.35) at index 0 and higher confidence (0.90) at index 1.
        // Both overlap the same GT box.
        // High confidence (0.90) MUST match first, claiming GT. Low confidence (0.35) becomes FP.
        var aiJson = @"[
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100,""confidence"":0.35},
            {""xmin"":0,""ymin"":0,""xmax"":100,""ymax"":100,""confidence"":0.90}
        ]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";

        var request = CreateConfirmRequest(aiJson, docJson);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, request, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(1, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
    }

    // =========================================================================
    // T3: Lifecycle & Recalculation Tests
    // =========================================================================

    [Fact]
    public async Task T3_01_CaliperReSaving_Idempotence_DoesNotMultiplyMetrics()
    {
        var ct = TestContext.Current.CancellationToken;
        // Step 1: Doctor confirms image 1 with 1 AI prediction and 1 matching GT box.
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.90}]";
        var docJson1 = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";

        var req1 = CreateConfirmRequest(aiJson, docJson1, burntFileName: "case_img_1_burnt.png");
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, req1, ct);

        // Xác nhận lại phải CHỈ ĐÍCH DANH ảnh đã lưu (tên file chứa imageId) — tên file gốc trên
        // máy bác sĩ không đủ để phân biệt, vì một ca có nhiều ảnh (xem T3_05).
        var savedImage = Assert.Single(await _db.UltrasoundImages.Where(i => i.CaseId == _caseId).ToListAsync(ct));
        var sameImageBurntName = $"case_img_{savedImage.ImageId}_burnt.png";

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);

        // Step 2: Doctor re-saves caliper on the SAME image with identical coordinates.
        // Metrics must NOT double (LiveTp must still be 1, not 2).
        var req2 = CreateConfirmRequest(aiJson, docJson1, burntFileName: sameImageBurntName);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, req2, ct);

        await _db.Entry(model).ReloadAsync(ct);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);

        // Step 3: Doctor adjusts caliper on the SAME image to a completely different location [500..600].
        // Old metrics (TP=1, FP=0, FN=0) must be deducted.
        // New metrics (TP=0, FP=1, FN=1) must be added.
        // Net result: LiveTp = 0, LiveFp = 1, LiveFn = 1.
        var docJson2 = "[{\"xmin\":500,\"ymin\":500,\"xmax\":600,\"ymax\":600}]";
        var req3 = CreateConfirmRequest(aiJson, docJson2, burntFileName: sameImageBurntName);
        await _caseDiagnosisService.ConfirmAnalysisAsync(_caseId, req3, ct);

        await _db.Entry(model).ReloadAsync(ct);
        Assert.Equal(0, model.LiveTp);
        Assert.Equal(1, model.LiveFp);
        Assert.Equal(1, model.LiveFn);

        // Vẫn chỉ 1 ảnh — cả 3 lần đều là cùng một ảnh.
        Assert.Single(await _db.UltrasoundImages.Where(i => i.CaseId == _caseId).ToListAsync(ct));
    }

    [Fact]
    public async Task T3_05_SequentialDifferentImages_EachConfirmCreatesNewImage_AndMetricsAccumulate()
    {
        // Bác sĩ tải và xác nhận lần lượt 2 ảnh KHÁC NHAU trong cùng một ca. FE luôn gửi tên file
        // gốc trên máy, không chứa imageId. Ảnh thứ hai phải là ảnh mới, không được ghi đè ảnh
        // thứ nhất (bản cũ coi "ca có đúng 1 ảnh" là xác nhận lại ảnh đó → Storage trả 409).
        var ct = TestContext.Current.CancellationToken;
        var aiJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100,\"confidence\":0.90}]";
        var docJson = "[{\"xmin\":0,\"ymin\":0,\"xmax\":100,\"ymax\":100}]";

        await _caseDiagnosisService.ConfirmAnalysisAsync(
            _caseId, CreateConfirmRequest(aiJson, docJson, burntFileName: "burnt_IMG_001.jpg", originalFileName: "IMG_001.jpg"), ct);
        await _caseDiagnosisService.ConfirmAnalysisAsync(
            _caseId, CreateConfirmRequest(aiJson, docJson, burntFileName: "burnt_IMG_002.jpg", originalFileName: "IMG_002.jpg"), ct);

        var images = await _db.UltrasoundImages.Where(i => i.CaseId == _caseId).ToListAsync(ct);
        Assert.Equal(2, images.Count);
        Assert.NotEqual(images[0].FileRef, images[1].FileRef);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.Equal(2, model!.LiveTp);
    }

    [Fact]
    public async Task T3_02_RecalculateMap50_ResynchronizesLiveMetrics_FromDbHistory()
    {
        var ct = TestContext.Current.CancellationToken;
        // Create 3 historical images in database:
        var img1 = Guid.NewGuid();
        var img2 = Guid.NewGuid();
        var img3 = Guid.NewGuid();

        // Image 1: 1 match (TP=1)
        _db.DoctorAnnotations.Add(new DoctorAnnotation { AnnotationId = Guid.NewGuid(), ImageId = img1, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100, Source = "doctor_added" });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = img1, ModelVersionId = _activeModelId, Confidence = 0.90m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100 });

        // Image 2: 1 match + 1 false alarm (TP=1, FP=1)
        _db.DoctorAnnotations.Add(new DoctorAnnotation { AnnotationId = Guid.NewGuid(), ImageId = img2, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100, Source = "doctor_added" });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = img2, ModelVersionId = _activeModelId, Confidence = 0.85m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100 });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = img2, ModelVersionId = _activeModelId, Confidence = 0.70m, BboxXmin = 300, BboxYmin = 300, BboxXmax = 400, BboxYmax = 400 });

        // Image 3: 0 AI predictions (sentinel with Conf 0m), 1 Doctor GT (FN=1)
        _db.DoctorAnnotations.Add(new DoctorAnnotation { AnnotationId = Guid.NewGuid(), ImageId = img3, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100, Source = "doctor_added" });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = img3, ModelVersionId = _activeModelId, Confidence = 0.0m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 0, BboxYmax = 0 });

        // Intentionally corrupt live metrics in the model record to simulate drift
        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        model.LiveTp = 999;
        model.LiveFp = 888;
        model.LiveFn = 777;
        model.LiveMap50 = 12.34m;
        await _db.SaveChangesAsync(ct);

        // Trigger batch recalculation
        await _aiMetricsService.CalculateMap50Async(_activeModelId, ct);
        await _db.Entry(model).ReloadAsync(ct);

        // Resynchronization Invariant:
        // Total GT = 3. Valid Predictions = 2 TP + 1 FP.
        // LiveTp = 2, LiveFp = 1, LiveFn = 3 - 2 = 1.
        Assert.Equal(2, model.LiveTp);
        Assert.Equal(1, model.LiveFp);
        Assert.Equal(1, model.LiveFn);
        Assert.True(model.LiveMap50 > 0m);

        // Verify Live Precision & Recall calculations
        var dto = new AiModelVersionDto
        {
            LiveTp = model.LiveTp,
            LiveFp = model.LiveFp,
            LiveFn = model.LiveFn
        };
        // LivePrecision = 2 / (2 + 1) = 0.6666...
        Assert.NotNull(dto.LivePrecision);
        Assert.Equal(Math.Round(2m / 3m, 4), Math.Round(dto.LivePrecision.Value, 4));
        // LiveRecall = 2 / (2 + 1) = 0.6666...
        Assert.NotNull(dto.LiveRecall);
        Assert.Equal(Math.Round(2m / 3m, 4), Math.Round(dto.LiveRecall.Value, 4));
    }

    // =========================================================================
    // T4: Missing Ground Truth Bug Elimination
    // =========================================================================

    [Fact]
    public async Task T4_01_BatchMap50_IncludesZeroPredictionImagesInTotalGt_EliminatesFalse100PercentRecall()
    {
        var ct = TestContext.Current.CancellationToken;
        // Root-cause verification of the Missing Ground Truth bug:
        // Cohort of 5 evaluated ultrasound images:
        // Image 1: 1 Doctor GT, AI detected 1 box (Conf 0.95, TP=1).
        // Images 2, 3, 4, 5 (4 images): AI completely missed all lesions (predicted 0 boxes, stored via sentinel Conf 0m).
        // Each of images 2..5 has 1 Doctor GT.
        //
        // Total GTs across ALL evaluated images = 5.
        // Total TPs = 1, FPs = 0, FNs = 4.
        //
        // Buggy code only queried images present in AiPredictions with detections -> totalGt was 1 -> Recall = 1/1 = 100%!
        // Fixed code accounts for all evaluated images -> totalGt = 5 -> Recall = 1/5 = 20.0%, mAP50 = 20.0%!

        var img1 = Guid.NewGuid();
        _db.DoctorAnnotations.Add(new DoctorAnnotation { AnnotationId = Guid.NewGuid(), ImageId = img1, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100, Source = "doctor_added" });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = img1, ModelVersionId = _activeModelId, Confidence = 0.95m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100 });

        for (int i = 2; i <= 5; i++)
        {
            var imgId = Guid.NewGuid();
            _db.DoctorAnnotations.Add(new DoctorAnnotation { AnnotationId = Guid.NewGuid(), ImageId = imgId, BboxXmin = 10, BboxYmin = 10, BboxXmax = 60, BboxYmax = 60, Source = "doctor_added" });
            // Zero-prediction sentinel record
            _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = imgId, ModelVersionId = _activeModelId, Confidence = 0.0m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 0, BboxYmax = 0 });
        }
        await _db.SaveChangesAsync(ct);

        await _aiMetricsService.CalculateMap50Async(_activeModelId, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(4, model.LiveFn);

        // mAP50 calculation: 1 TP at Recall 1/5 = 0.20 with Precision 1/1 = 1.0. Area = 1.0 * 0.20 = 0.20 -> 20.00%
        Assert.Equal(20.00m, model.LiveMap50);

        var dto = new AiModelVersionDto
        {
            LiveTp = model.LiveTp,
            LiveFp = model.LiveFp,
            LiveFn = model.LiveFn
        };
        Assert.Equal(1.0m, dto.LivePrecision); // 1 / (1 + 0) = 1.0 (100%)
        Assert.Equal(0.2m, dto.LiveRecall);    // 1 / (1 + 4) = 0.2 (20%) - NOT 100%!
    }

    // =========================================================================
    // Sentinel AiPrediction Records Tests
    // =========================================================================

    [Fact]
    public async Task Sentinel_ZeroConfidencePredictions_AreIgnoredInMatchingAndCalculation()
    {
        var ct = TestContext.Current.CancellationToken;
        // An image has:
        // 1 Sentinel prediction (Confidence = 0.0m)
        // 1 Real prediction (Confidence = 0.90m, Box [0, 0, 100, 100])
        // 1 Doctor GT Box [0, 0, 100, 100]
        //
        // The sentinel MUST be ignored:
        // - It must not be counted as a candidate detection
        // - It must not be matched to the doctor box
        // - It must not increment False Positives
        var imgId = Guid.NewGuid();
        _db.DoctorAnnotations.Add(new DoctorAnnotation { AnnotationId = Guid.NewGuid(), ImageId = imgId, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100, Source = "doctor_added" });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = imgId, ModelVersionId = _activeModelId, Confidence = 0.0m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 0, BboxYmax = 0 });
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = imgId, ModelVersionId = _activeModelId, Confidence = 0.90m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100 });
        await _db.SaveChangesAsync(ct);

        await _aiMetricsService.CalculateMap50Async(_activeModelId, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(1, model.LiveTp);
        Assert.Equal(0, model.LiveFp); // NOT 1 FP
        Assert.Equal(0, model.LiveFn);
        Assert.Equal(100.0m, model.LiveMap50);
    }

    [Fact]
    public async Task Sentinel_ZeroConfidencePredictions_OnNormalImage_YieldsZeroFp()
    {
        var ct = TestContext.Current.CancellationToken;
        // An evaluated normal image with 0 Doctor GTs and 1 Sentinel (Confidence = 0.0m).
        // Must yield TP=0, FP=0, FN=0, LiveMap50=0m.
        var imgId = Guid.NewGuid();
        _db.AiPredictions.Add(new AiPrediction { PredictionId = Guid.NewGuid(), ImageId = imgId, ModelVersionId = _activeModelId, Confidence = 0.0m, BboxXmin = 0, BboxYmin = 0, BboxXmax = 0, BboxYmax = 0 });
        await _db.SaveChangesAsync(ct);

        await _aiMetricsService.CalculateMap50Async(_activeModelId, ct);

        var model = await _db.AiModelVersions.FindAsync(new object[] { _activeModelId }, ct);
        Assert.NotNull(model);
        Assert.Equal(0, model.LiveTp);
        Assert.Equal(0, model.LiveFp);
        Assert.Equal(0, model.LiveFn);
        Assert.Equal(0m, model.LiveMap50);
    }
}
