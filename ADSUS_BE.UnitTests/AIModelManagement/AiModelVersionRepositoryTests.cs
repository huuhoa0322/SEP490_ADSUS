using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.AIModelManagement;

public class AiModelVersionRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task SearchAsync_NoKeyword_ReturnsAllOrderedByStatusThenRegisteredAtDescending()
    {
        await using var db = CreateContext();
        var sut = new AiModelVersionRepository(db);

        db.AiModelVersions.Add(new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "v1", Status = ModelVersionStatus.Inactive,
            HfRepoId = "r", HfFilename = "f", RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        db.AiModelVersions.Add(new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "v2", Status = ModelVersionStatus.Active,
            HfRepoId = "r", HfFilename = "f", RegisteredAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        db.AiModelVersions.Add(new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "v3", Status = ModelVersionStatus.Inactive,
            HfRepoId = "r", HfFilename = "f", RegisteredAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var (items, totalItems) = await sut.SearchAsync(null, page: 1, pageSize: 20, CancellationToken.None);

        Assert.Equal(3, totalItems);
        // OrderBy(Status) -> Active (0) before Inactive (1), then ThenByDescending(RegisteredAt) within each group
        Assert.Equal("v2", items[0].VersionCode);
        Assert.Equal("v3", items[1].VersionCode);
        Assert.Equal("v1", items[2].VersionCode);
    }

    [Fact]
    public async Task SearchAsync_WithKeyword_FiltersByVersionCodeOrHfFilename()
    {
        await using var db = CreateContext();
        var sut = new AiModelVersionRepository(db);

        db.AiModelVersions.Add(new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "YOLO26_v1", Status = ModelVersionStatus.Inactive,
            HfRepoId = "r", HfFilename = "model.pt", RegisteredAt = DateTime.UtcNow
        });
        db.AiModelVersions.Add(new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "other", Status = ModelVersionStatus.Inactive,
            HfRepoId = "r", HfFilename = "yolo26_weights.pt", RegisteredAt = DateTime.UtcNow
        });
        db.AiModelVersions.Add(new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "unrelated", Status = ModelVersionStatus.Inactive,
            HfRepoId = "r", HfFilename = "resnet.pt", RegisteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (items, totalItems) = await sut.SearchAsync("yolo26", page: 1, pageSize: 20, CancellationToken.None);

        Assert.Equal(2, totalItems);
        Assert.All(items, v => Assert.True(
            v.VersionCode.Contains("YOLO26", StringComparison.OrdinalIgnoreCase) ||
            v.HfFilename.Contains("yolo26", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task SearchAsync_ReturnsUntrackedEntities()
    {
        // Regression test cho P11 review Feature 6 (30/08/2026): SearchAsync dùng cho list hiển thị
        // Admin, không bao giờ sửa-rồi-lưu lại — phải AsNoTracking() để tránh giữ change tracker
        // không cần thiết trên cả trang kết quả.
        await using var db = CreateContext();
        var sut = new AiModelVersionRepository(db);

        var version = new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(), VersionCode = "v1", Status = ModelVersionStatus.Inactive,
            HfRepoId = "r", HfFilename = "f", RegisteredAt = DateTime.UtcNow
        };
        db.AiModelVersions.Add(version);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (items, _) = await sut.SearchAsync(null, page: 1, pageSize: 20, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal(EntityState.Detached, db.Entry(items[0]).State);
    }
}
