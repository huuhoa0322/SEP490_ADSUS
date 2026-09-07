using ADSUS_BE.BLL.AIModelManagement.Mappers;
using ADSUS_BE.DAL.Entities;
using Xunit;

namespace ADSUS_BE.UnitTests.AIModelManagement;

public class AiModelVersionMapperTests
{
    private static AiModelVersion MakeVersion() => new()
    {
        ModelVersionId = Guid.NewGuid(),
        VersionCode = "YOLO26_v1",
        Description = "Test version",
        MetricsPrecision = 91.5m,
        MetricsMap50 = 88.2m,
        MetricsRecall = 0.93m,
        Status = ModelVersionStatus.Active,
        HfRepoId = "org/repo",
        HfFilename = "model.pt",
        RegisteredAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        RegisteredBy = Guid.NewGuid(),
        LiveTp = 10,
        LiveFp = 2,
        LiveFn = 1,
        LiveMap50 = 85.0m,
        LastEvaluatedAt = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void ToDto_MapsAllFieldsIncludingLiveMetrics()
    {
        var entity = MakeVersion();

        var dto = AiModelVersionMapper.ToDto(entity);

        Assert.Equal(entity.ModelVersionId, dto.ModelVersionId);
        Assert.Equal(entity.VersionCode, dto.VersionCode);
        Assert.Equal(entity.Description, dto.Description);
        Assert.Equal(entity.MetricsPrecision, dto.MetricsPrecision);
        Assert.Equal(entity.MetricsMap50, dto.MetricsMap50);
        Assert.Equal(entity.MetricsRecall, dto.MetricsRecall);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(entity.HfRepoId, dto.HfRepoId);
        Assert.Equal(entity.HfFilename, dto.HfFilename);
        Assert.Equal(entity.RegisteredAt, dto.RegisteredAt);
        Assert.Equal(entity.RegisteredBy, dto.RegisteredBy);
        Assert.Equal(entity.LiveTp, dto.LiveTp);
        Assert.Equal(entity.LiveFp, dto.LiveFp);
        Assert.Equal(entity.LiveFn, dto.LiveFn);
        Assert.Equal(entity.LiveMap50, dto.LiveMap50);
        Assert.Equal(entity.LastEvaluatedAt, dto.LastEvaluatedAt);
    }

    [Fact]
    public void ToDto_InactiveStatus_SerializesAsInactiveString()
    {
        var entity = MakeVersion();
        entity.Status = ModelVersionStatus.Inactive;

        var dto = AiModelVersionMapper.ToDto(entity);

        Assert.Equal("Inactive", dto.Status);
    }

    [Fact]
    public void ToActiveDto_MapsOnlyVersionCodeAndStatus()
    {
        var entity = MakeVersion();

        var dto = AiModelVersionMapper.ToActiveDto(entity);

        Assert.Equal(entity.VersionCode, dto.VersionCode);
        Assert.Equal("Active", dto.Status);
    }
}
