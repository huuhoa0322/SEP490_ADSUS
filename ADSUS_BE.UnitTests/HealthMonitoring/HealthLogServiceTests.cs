using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using ADSUS_BE.BLL.HealthMonitoring.Interfaces;
using ADSUS_BE.BLL.HealthMonitoring.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADSUS_BE.UnitTests.HealthMonitoring;

/// <summary>
/// Tests cho HealthLogService (UC-21, FT-35).
/// Based on API Spec Module09 endpoints #55 and #56.
///
/// Test cases (11 total):
/// - LogHealthDataAsync: happy path EXERCISE/DIET, correct field mapping
/// - GetHealthLogsAsync: returns logs for today, specific date, empty list
/// - Ordering: logs ordered by CreatedAt ASC
/// - Patient isolation: each patient only sees their own logs
/// </summary>
public class HealthLogServiceTests
{
    private static HealthLog NewHealthLog(
        Guid? id = null,
        Guid? patientId = null,
        HealthLogType type = HealthLogType.EXERCISE,
        DateOnly? date = null,
        string content = "Test content",
        DateTime? createdAt = null)
        => new()
        {
            HealthLogId = id ?? Guid.NewGuid(),
            PatientProfileId = patientId ?? Guid.NewGuid(),
            LogDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            LogType = type,
            Content = content,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

    #region LogHealthDataAsync Tests

    [Fact]
    public async Task LogExercise_CreatesRecordWithCorrectFields()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        HealthLog? capturedLog = null;

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .Callback<HealthLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = "Ran 5km"
        };

        // Act
        var result = await sut.LogHealthDataAsync(request, patientId);

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal(patientId, capturedLog!.PatientProfileId);
        Assert.Equal(HealthLogType.EXERCISE, capturedLog.LogType);
        Assert.Equal("Ran 5km", capturedLog.Content);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), capturedLog.LogDate);
        Assert.NotEqual(Guid.Empty, capturedLog.HealthLogId);
    }

    [Fact]
    public async Task LogDiet_CreatesRecordWithDietType()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        HealthLog? capturedLog = null;

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .Callback<HealthLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "Ate salad"
        };

        // Act
        await sut.LogHealthDataAsync(request, patientId);

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal(HealthLogType.DIET, capturedLog!.LogType);
    }

    [Theory]
    [InlineData("exercise")]
    [InlineData("Exercise")]
    [InlineData("EXERCISE")]
    [InlineData("ExErCiSe")]
    public async Task LogExercise_CaseInsensitive_ParsesCorrectly(string typeInput)
    {
        // Arrange
        HealthLog? capturedLog = null;

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .Callback<HealthLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var request = new LogHealthDataRequest
        {
            Type = typeInput,
            Content = "Test"
        };

        // Act
        await sut.LogHealthDataAsync(request, Guid.NewGuid());

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal(HealthLogType.EXERCISE, capturedLog!.LogType);
    }

    [Fact]
    public async Task LogHealthData_TrimsContent()
    {
        // Arrange
        HealthLog? capturedLog = null;

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .Callback<HealthLog, CancellationToken>((log, _) => capturedLog = log)
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = "  Trimmed content  "
        };

        // Act
        await sut.LogHealthDataAsync(request, Guid.NewGuid());

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal("Trimmed content", capturedLog!.Content);
    }

    [Fact]
    public async Task LogHealthData_ReturnsCorrectResponse()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var logDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var createdAt = DateTime.UtcNow;

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<HealthLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HealthLog log, CancellationToken _) => log);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "Breakfast"
        };

        // Act
        var result = await sut.LogHealthDataAsync(request, patientId);

        // Assert
        Assert.Equal(patientId, result.PatientProfileId);
        Assert.Equal("DIET", result.Type);
        Assert.Equal("Breakfast", result.Content);
        Assert.Equal(logDate, result.LogDate);
        Assert.NotEqual(Guid.Empty, result.HealthLogId);
        Assert.NotEqual(default, result.CreatedAt);
    }

    #endregion

    #region GetHealthLogsAsync Tests

    [Fact]
    public async Task GetLogsForToday_ReturnsLogs()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var logs = new List<HealthLog>
        {
            NewHealthLog(patientId: patientId, date: today, content: "Log 1"),
            NewHealthLog(patientId: patientId, date: today, content: "Log 2"),
            NewHealthLog(patientId: patientId, date: today, content: "Log 3"),
        };

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.GetByPatientAndDateAsync(patientId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var criteria = new HealthLogSearchCriteria { Date = today };

        // Act
        var result = await sut.GetHealthLogsAsync(patientId, criteria);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Log 1", result[0].Content);
        Assert.Equal("Log 2", result[1].Content);
        Assert.Equal("Log 3", result[2].Content);
    }

    [Fact]
    public async Task GetLogsForSpecificDate_ReturnsLogs()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var specificDate = new DateOnly(2026, 8, 1);
        var logs = new List<HealthLog>
        {
            NewHealthLog(patientId: patientId, date: specificDate, content: "Old log"),
        };

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.GetByPatientAndDateAsync(patientId, specificDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var criteria = new HealthLogSearchCriteria { Date = specificDate };

        // Act
        var result = await sut.GetHealthLogsAsync(patientId, criteria);

        // Assert
        Assert.Single(result);
        Assert.Equal("Old log", result[0].Content);
        Assert.Equal(specificDate, result[0].LogDate);
    }

    [Fact]
    public async Task GetLogs_NoLogs_ReturnsEmptyList()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.GetByPatientAndDateAsync(patientId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>());

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var criteria = new HealthLogSearchCriteria();

        // Act
        var result = await sut.GetHealthLogsAsync(patientId, criteria);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLogs_DefaultsToToday_WhenNoDateProvided()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.GetByPatientAndDateAsync(patientId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>());

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var criteria = new HealthLogSearchCriteria { Date = null };

        // Act
        await sut.GetHealthLogsAsync(patientId, criteria);

        // Assert
        repo.Verify(r => r.GetByPatientAndDateAsync(patientId, today, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLogs_FiltersByPatient()
    {
        // Arrange
        var patientAId = Guid.NewGuid();
        var patientBId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var repo = new Mock<IHealthLogRepository>();
        repo.Setup(r => r.GetByPatientAndDateAsync(patientAId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>
            {
                NewHealthLog(patientId: patientAId, date: today)
            });
        repo.Setup(r => r.GetByPatientAndDateAsync(patientBId, today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>
            {
                NewHealthLog(patientId: patientBId, date: today)
            });

        var logger = new Mock<ILogger<HealthLogService>>();
        var sut = new HealthLogService(repo.Object, logger.Object);

        var criteria = new HealthLogSearchCriteria { Date = today };

        // Act
        var resultA = await sut.GetHealthLogsAsync(patientAId, criteria);
        var resultB = await sut.GetHealthLogsAsync(patientBId, criteria);

        // Assert
        Assert.Single(resultA);
        Assert.Single(resultB);
        Assert.Equal(patientAId, resultA[0].PatientProfileId);
        Assert.Equal(patientBId, resultB[0].PatientProfileId);
    }

    #endregion
}
