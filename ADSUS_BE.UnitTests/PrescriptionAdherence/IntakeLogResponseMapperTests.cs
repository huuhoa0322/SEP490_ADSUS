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
        // scheduled 09:00, now is 10:00 — 1 hour late, within the 2-hour OVERTIME window
        var log = Log(NowUtc.AddHours(-1));

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
    public void FromEntity_ScheduledTimeMoreThanTwoHoursAgo_NoConfirmed_ReturnsMissed()
    {
        // scheduled 08:00, now is 10:00 — already 2 hours late → MISSED
        var log = Log(NowUtc.AddHours(-3), null);

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("MISSED", result.Status);
    }

    [Fact]
    public void FromEntity_ScheduledTimeExactlyTwoHoursAgo_NoConfirmed_ReturnsMissed()
    {
        // exactly 2 hours late → boundary: >= 2h = MISSED
        var log = Log(NowUtc.AddHours(-2), null);

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("MISSED", result.Status);
    }

    [Fact]
    public void FromEntity_ScheduledTimeJustUnderTwoHours_NoConfirmed_ReturnsOvertime()
    {
        // 1 hour 59 minutes late → still in the 2-hour window → OVERTIME
        var log = Log(NowUtc.AddHours(-1).AddMinutes(-59), null);

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("OVERTIME", result.Status);
    }

    [Fact]
    public void FromEntity_ConfirmedBeforeTwoHours_ReturnsTaken_EvenIfPastTwoHours()
    {
        // confirmed at 09:00, scheduled 08:00, now 10:00 — taken before MISSED window → TAKEN
        var log = Log(NowUtc.AddHours(-2), NowUtc.AddHours(-1));

        var result = IntakeLogResponseMapper.FromEntity(log, NowUtc);

        Assert.Equal("TAKEN", result.Status);
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
