using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho IntakeLogResponseMapper.
/// Status derive từ ConfirmedAt + ScheduledTime vs nowUtc — master convention Opt-X.
/// 3 cases: TAKEN (ConfirmedAt has value), PENDING (future), OVERTIME (past, not confirmed).
/// </summary>
public class IntakeLogResponseMapperTests
{
    // Dùng fixed UTC time để deterministic: 2026-07-28 10:00 UTC
    private static readonly DateTime NowUtc = new(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc);

    private static MedicationIntakeLog Log(
        DateTime scheduledUtc,
        DateTime? confirmedUtc = null)
        => new()
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = Guid.NewGuid(),
            ScheduledTime = scheduledUtc,
            ConfirmedAt = confirmedUtc,
        };

    [Fact]
    public void FromEntity_ConfirmedAtHasValue_ReturnsTaken()
    {
        var log = Log(NowUtc.AddHours(-2), NowUtc.AddHours(-1));

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("TAKEN", result.Status);
    }

    [Fact]
    public void FromEntity_NoConfirmedAtAndScheduledTimeInFuture_ReturnsPending()
    {
        var log = Log(NowUtc.AddHours(2)); // scheduled 12:00, now is 10:00

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("PENDING", result.Status);
    }

    [Fact]
    public void FromEntity_NoConfirmedAtAndScheduledTimeInPast_ReturnsOvertime()
    {
        var log = Log(NowUtc.AddHours(-2)); // scheduled 08:00, now is 10:00 — already late

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("OVERTIME", result.Status);
    }

    [Fact]
    public void FromEntity_NoConfirmedAtAndScheduledTimeEqualsNow_ReturnsOvertime()
    {
        var log = Log(NowUtc); // scheduled exactly at now

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("OVERTIME", result.Status);
    }

    [Fact]
    public void FromEntity_AllFieldsMappedCorrectly()
    {
        var intakeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var scheduled = NowUtc.AddHours(-3);
        var confirmed = NowUtc.AddHours(-2);
        var log = new MedicationIntakeLog
        {
            IntakeId = intakeId,
            PrescriptionItemId = itemId,
            ScheduledTime = scheduled,
            ConfirmedAt = confirmed,
        };

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal(intakeId, result.IntakeId);
        Assert.Equal(itemId, result.PrescriptionItemId);
        Assert.Equal(scheduled, result.ScheduledTime);
        Assert.Equal(confirmed, result.ConfirmedAt);
    }
}
