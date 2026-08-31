using System.Net;
using ADSUS_BE.BLL.AIModelManagement.DTOs;
using ADSUS_BE.BLL.AIModelManagement.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace ADSUS_BE.UnitTests.AIModelManagement;

public class AiModelServiceTests
{
    private readonly Mock<IAiModelVersionRepository> _modelRepoMock = new();
    private readonly Mock<IAuditLogRepository> _auditMock = new();
    private readonly Mock<IHttpClientFactory> _httpMock = new();
    private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
    private readonly Mock<ILogger<AiModelService>> _loggerMock = new();

    private readonly AiBackendSettings _settings = new() { WebhookUrl = "http://fake-ai-backend", Token = "fake-token" };
    private readonly AiModelService _sut;

    public AiModelServiceTests()
    {
        var optionsMock = new Mock<IOptions<AiBackendSettings>>();
        optionsMock.Setup(o => o.Value).Returns(_settings);

        var client = new HttpClient(_httpHandlerMock.Object);
        _httpMock.Setup(f => f.CreateClient("AiBackend")).Returns(client);

        _sut = new AiModelService(_modelRepoMock.Object, _auditMock.Object, _httpMock.Object, optionsMock.Object, _loggerMock.Object);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content, Exception? exceptionToThrow = null)
    {
        if (exceptionToThrow != null)
        {
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(exceptionToThrow);
        }
        else
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }
    }
    
    // UT_Ai_01: SearchVersionsAsync -> Returns PagedResult
    [Fact]
    public async Task SearchVersionsAsync_ValidKeyword_ReturnsPagedResult()
    {
        // Arrange
        var items = new List<AiModelVersion>
        {
            new AiModelVersion { ModelVersionId = Guid.NewGuid(), VersionCode = "v1" }
        };
        _modelRepoMock.Setup(r => r.SearchAsync("v1", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // Act
        var result = await _sut.SearchVersionsAsync("v1", 1, 20);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("v1", result.Items[0].VersionCode);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    // UT_Ai_02: GetVersionByIdAsync -> Returns DTO
    [Fact]
    public async Task GetVersionByIdAsync_ExistingId_ReturnsDto()
    {
        var id = Guid.NewGuid();
        _modelRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion { ModelVersionId = id, VersionCode = "v2" });

        var result = await _sut.GetVersionByIdAsync(id);
        Assert.Equal(id, result.ModelVersionId);
        Assert.Equal("v2", result.VersionCode);
    }

    // UT_Ai_03: GetVersionByIdAsync -> Throws ResourceNotFoundException
    [Fact]
    public async Task GetVersionByIdAsync_NonExistingId_ThrowsResourceNotFoundException()
    {
        var id = Guid.NewGuid();
        _modelRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.GetVersionByIdAsync(id));
    }

    // UT_Ai_04: RegisterVersionAsync -> Duplicate -> Throws BusinessException
    [Fact]
    public async Task RegisterVersionAsync_DuplicateVersionCode_ThrowsBusinessException()
    {
        var req = new RegisterModelVersionRequest { VersionCode = "v1", HfRepoId = "r", HfFilename = "f" };
        _modelRepoMock.Setup(r => r.VersionCodeExistsAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessException>(() => _sut.RegisterVersionAsync(req, Guid.NewGuid()));
    }

    // UT_Ai_05: RegisterVersionAsync -> Success -> Adds to Repo and AuditLog
    [Fact]
    public async Task RegisterVersionAsync_Valid_SavesAndReturnsDto()
    {
        var req = new RegisterModelVersionRequest { VersionCode = "v1", HfRepoId = "r", HfFilename = "f" };
        _modelRepoMock.Setup(r => r.VersionCodeExistsAsync("v1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        
        var adminId = Guid.NewGuid();
        var result = await _sut.RegisterVersionAsync(req, adminId);

        _modelRepoMock.Verify(r => r.AddAsync(It.Is<AiModelVersion>(v => v.VersionCode == "v1" && v.Status == ModelVersionStatus.Inactive), It.IsAny<CancellationToken>()), Times.Once);
        _auditMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "REGISTER_AI_MODEL"), It.IsAny<CancellationToken>()), Times.Once);
        _modelRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal("v1", result.VersionCode);
        Assert.Equal("Inactive", result.Status);
    }

    // UT_Ai_05b: GetActiveVersionAsync -> No Active Version -> Returns Null
    [Fact]
    public async Task GetActiveVersionAsync_NoActiveVersion_ReturnsNull()
    {
        _modelRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        var result = await _sut.GetActiveVersionAsync();

        Assert.Null(result);
    }

    // UT_Ai_05c: GetActiveVersionAsync -> Has Active Version -> Returns Slim Doctor-facing Dto (UC-20: code/status only)
    [Fact]
    public async Task GetActiveVersionAsync_HasActiveVersion_ReturnsVersionCodeAndStatusOnly()
    {
        var v = new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(),
            VersionCode = "v3",
            Status = ModelVersionStatus.Active,
            MetricsPrecision = 99.9m,
            RegisteredBy = Guid.NewGuid()
        };
        _modelRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(v);

        var result = await _sut.GetActiveVersionAsync();

        Assert.NotNull(result);
        Assert.Equal("v3", result!.VersionCode);
        Assert.Equal("Active", result.Status);
    }

    // UT_Ai_06: UpdateVersionAsync -> Not Found -> Throws ResourceNotFoundException
    [Fact]
    public async Task UpdateVersionAsync_NonExistingId_ThrowsResourceNotFoundException()
    {
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.UpdateVersionAsync(Guid.NewGuid(), new UpdateModelVersionRequest(), Guid.NewGuid()));
    }

    // UT_Ai_07: UpdateVersionAsync -> Active -> Throws BusinessException
    [Fact]
    public async Task UpdateVersionAsync_ActiveVersion_ThrowsBusinessException()
    {
        var v = new AiModelVersion { Status = ModelVersionStatus.Active };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(v);

        await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateVersionAsync(Guid.NewGuid(), new UpdateModelVersionRequest(), Guid.NewGuid()));
    }

    // UT_Ai_08: UpdateVersionAsync -> Success
    [Fact]
    public async Task UpdateVersionAsync_Success_UpdatesAndLogs()
    {
        var v = new AiModelVersion { Status = ModelVersionStatus.Inactive };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(v);

        var req = new UpdateModelVersionRequest { Description = "New Desc" };
        await _sut.UpdateVersionAsync(Guid.NewGuid(), req, Guid.NewGuid());

        Assert.Equal("New Desc", v.Description);
        _auditMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "UPDATE_AI_MODEL"), It.IsAny<CancellationToken>()), Times.Once);
        _modelRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UT_Ai_09: ActivateVersionAsync -> Not Found -> Throws ResourceNotFoundException
    [Fact]
    public async Task ActivateVersionAsync_NonExistingId_ThrowsResourceNotFoundException()
    {
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiModelVersion?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    // UT_Ai_10: ActivateVersionAsync -> Already Active -> Throws BusinessException
    [Fact]
    public async Task ActivateVersionAsync_AlreadyActive_ThrowsBusinessException()
    {
        var v = new AiModelVersion { Status = ModelVersionStatus.Active };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(v);

        await Assert.ThrowsAsync<BusinessException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    // UT_Ai_11: ActivateVersionAsync -> Backend 404 -> Rollback and throw
    [Fact]
    public async Task ActivateVersionAsync_BackendReturns404_RollbackAndThrow()
    {
        var target = new AiModelVersion { Status = ModelVersionStatus.Inactive };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(target);
        
        SetupHttpResponse(HttpStatusCode.NotFound, "Repository Not Found");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Contains("Không tìm thấy mô hình", ex.Message);
        
        _modelRepoMock.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UT_Ai_12: ActivateVersionAsync -> Backend 401 -> Rollback and throw
    [Fact]
    public async Task ActivateVersionAsync_BackendReturns401_RollbackAndThrow()
    {
        var target = new AiModelVersion { Status = ModelVersionStatus.Inactive };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(target);
        
        SetupHttpResponse(HttpStatusCode.Unauthorized, "Unauthorized access");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Contains("Lỗi xác thực", ex.Message);
        
        _modelRepoMock.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UT_Ai_13: ActivateVersionAsync -> Backend 500 -> Rollback and throw
    [Fact]
    public async Task ActivateVersionAsync_BackendReturns500_RollbackAndThrow()
    {
        var target = new AiModelVersion { Status = ModelVersionStatus.Inactive };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(target);
        
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Something went wrong");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Contains("Quá trình kích hoạt thất bại", ex.Message);
        
        _modelRepoMock.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UT_Ai_13b: ActivateVersionAsync -> Backend Timeout -> Rollback and throw
    [Fact]
    public async Task ActivateVersionAsync_BackendTimeout_RollbackAndThrow()
    {
        var target = new AiModelVersion { Status = ModelVersionStatus.Inactive };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(target);
        
        SetupHttpResponse(HttpStatusCode.OK, "", new TaskCanceledException("Timeout"));

        await Assert.ThrowsAsync<TaskCanceledException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
        
        _modelRepoMock.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UT_Ai_14: ActivateVersionAsync -> Success -> Active/Inactive swap and Commit
    [Fact]
    public async Task ActivateVersionAsync_Success_SwapsStatusAndCommits()
    {
        var currentActive = new AiModelVersion { Status = ModelVersionStatus.Active, RegisteredAt = DateTime.UtcNow.AddDays(-1) };
        var target = new AiModelVersion { Status = ModelVersionStatus.Inactive, RegisteredAt = DateTime.UtcNow }; // Newer -> Activate
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _modelRepoMock.Setup(r => r.GetActiveVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(currentActive);
        
        SetupHttpResponse(HttpStatusCode.OK, "Success");

        await _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid());
        
        Assert.Equal(ModelVersionStatus.Inactive, currentActive.Status);
        Assert.Equal(ModelVersionStatus.Active, target.Status);
        
        _modelRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); // One for currentActive, one at the end
        _modelRepoMock.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "ACTIVATE_AI_MODEL"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // UT_Ai_15: ActivateVersionAsync -> DB Save Fails -> Rollback and throw
    [Fact]
    public async Task ActivateVersionAsync_DbSaveFails_RollbacksAndThrows()
    {
        var target = new AiModelVersion { Status = ModelVersionStatus.Inactive };
        _modelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _modelRepoMock.Setup(r => r.GetActiveVersionAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AiModelVersion?)null);
        
        // Setup DB to throw on SaveChanges
        _modelRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("DB constraint failed"));
            
        SetupHttpResponse(HttpStatusCode.OK, "Success"); // Backend would succeed if it got here, but it throws on DB commit after

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => _sut.ActivateVersionAsync(Guid.NewGuid(), Guid.NewGuid()));
        
        _modelRepoMock.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
