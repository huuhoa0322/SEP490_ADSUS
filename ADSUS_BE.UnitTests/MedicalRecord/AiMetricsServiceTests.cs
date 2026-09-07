using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.MedicalRecord;

public class AiMetricsServiceTests
{
    private readonly AppDbContext _db;
    private readonly AiMetricsService _sut;

    public AiMetricsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        // P11 review (29/08/2026): AiMetricsService không còn nhận AppDbContext trực tiếp,
        // giữ nguyên phong cách test EF InMemory thật (không mock) bằng cách bọc 3 repository
        // thật quanh cùng _db — hành vi giống hệt code cũ, chỉ đổi đường truy cập dữ liệu.
        _sut = new AiMetricsService(
            new AiModelVersionRepository(_db),
            new AiPredictionRepository(_db),
            new DoctorAnnotationRepository(_db));
    }

    // UT_Metrics_01: CalculateMap50Async -> Version not found -> Throws InvalidOperationException
    [Fact]
    public async Task CalculateMap50Async_VersionNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CalculateMap50Async(Guid.NewGuid()));
    }

    // UT_Metrics_02: CalculateMap50Async -> Empty Predictions or GTs -> Sets LiveMap50 to 0
    [Fact]
    public async Task CalculateMap50Async_EmptyData_SetsLiveMap50ToZero()
    {
        var versionId = Guid.NewGuid();
        var model = new AiModelVersion { ModelVersionId = versionId, VersionCode = "v1", HfFilename = "f", HfRepoId = "r" };
        _db.AiModelVersions.Add(model);
        await _db.SaveChangesAsync();

        await _sut.CalculateMap50Async(versionId);

        var updatedModel = await _db.AiModelVersions.FindAsync(versionId);
        Assert.Equal(0, updatedModel!.LiveMap50);
        Assert.True(updatedModel.LastEvaluatedAt > DateTime.MinValue);
    }

    // UT_Metrics_03: CalculateMap50Async -> Success -> Calculates Map50
    [Fact]
    public async Task CalculateMap50Async_Success_CalculatesMap50()
    {
        var versionId = Guid.NewGuid();
        var model = new AiModelVersion { ModelVersionId = versionId, VersionCode = "v1", HfFilename = "f", HfRepoId = "r" };
        _db.AiModelVersions.Add(model);

        var imageId = Guid.NewGuid();

        // Add DoctorAnnotations (Ground Truth)
        _db.DoctorAnnotations.Add(new DoctorAnnotation
        {
            AnnotationId = Guid.NewGuid(), ImageId = imageId,
            Source = "USER", // required property
            BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100
        });
        
        // Add AiPredictions
        // Prediction 1: Perfect match -> IoU = 1.0 (TP)
        _db.AiPredictions.Add(new AiPrediction
        {
            PredictionId = Guid.NewGuid(), ImageId = imageId, ModelVersionId = versionId,
            Confidence = 0.9m,
            BboxXmin = 0, BboxYmin = 0, BboxXmax = 100, BboxYmax = 100
        });

        // Prediction 2: No overlap -> IoU = 0.0 (FP)
        _db.AiPredictions.Add(new AiPrediction
        {
            PredictionId = Guid.NewGuid(), ImageId = imageId, ModelVersionId = versionId,
            Confidence = 0.8m,
            BboxXmin = 200, BboxYmin = 200, BboxXmax = 300, BboxYmax = 300
        });

        await _db.SaveChangesAsync();

        // Calculate Map50
        await _sut.CalculateMap50Async(versionId);

        var updatedModel = await _db.AiModelVersions.FindAsync(versionId);
        
        // TP list is [1, 0]
        // Precisions: [1/1, 1/2] -> [1.0, 0.5]
        // Recalls: [1/1, 1/1] -> [1.0, 1.0]
        // Interpolated Precisions: [1.0, 0.5]
        // mAP50 area calculation depends on VOC 2012 algorithm. With TP=1 at Recall=1, area is 1.0.
        // It is converted to percentage (100.0).
        Assert.Equal(100.0m, updatedModel!.LiveMap50);
        Assert.True(updatedModel.LastEvaluatedAt > DateTime.MinValue);
    }
}
