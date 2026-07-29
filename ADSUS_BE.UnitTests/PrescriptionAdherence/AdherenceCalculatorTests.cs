using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Pure-domain tests cho AdherenceCalculator. Status derive từ ConfirmedAt
/// (master convention). Không phụ thuộc EF/DB.
/// </summary>
public class AdherenceCalculatorTests
{
    private readonly DateTime _now = new(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc);

    private static MedicationIntakeLog Log(DateTime scheduledUtc, DateTime? confirmedUtc)
        => new()
        {
            IntakeId = Guid.NewGuid(),
            PrescriptionItemId = Guid.NewGuid(),
            ScheduledTime = scheduledUtc,
            ConfirmedAt = confirmedUtc,
        };

    [Fact]
    public void Calculate_EmptyLogs_ReturnsZero()
    {
        var result = AdherenceCalculator.Calculate(Array.Empty<MedicationIntakeLog>(), _now);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void Calculate_AllTaken_ReturnsHundred()
    {
        var logs = new[]
        {
            Log(_now.AddHours(-4), _now.AddHours(-3)),
            Log(_now.AddHours(-2), _now.AddHours(-1)),
            Log(_now.AddHours(-1), _now.AddMinutes(-30)),
        };

        var result = AdherenceCalculator.Calculate(logs, _now);

        Assert.Equal(100m, result);
    }

    [Fact]
    public void Calculate_AllPendingDue_ReturnsZero()
    {
        var logs = new[]
        {
            Log(_now.AddHours(-4), null),
            Log(_now.AddHours(-2), null),
        };

        var result = AdherenceCalculator.Calculate(logs, _now);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void Calculate_HalfTaken_ReturnsFifty()
    {
        var logs = new[]
        {
            Log(_now.AddHours(-4), _now.AddHours(-3)),   // TAKEN
            Log(_now.AddHours(-3), null),                // PENDING
        };

        var result = AdherenceCalculator.Calculate(logs, _now);

        Assert.Equal(50m, result);
    }

    [Fact]
    public void Calculate_FutureLogsNotCounted_OnlyDueLogsInRatio()
    {
        // 2 logs đã đến hạn (1 taken, 1 pending) + 2 logs tương lai (chưa tính)
        var logs = new[]
        {
            Log(_now.AddHours(-2), _now.AddHours(-1)),   // TAKEN — due
            Log(_now.AddHours(-1), null),                // PENDING — due
            Log(_now.AddHours(1), null),                 // FUTURE — bỏ qua
            Log(_now.AddHours(2), null),                 // FUTURE — bỏ qua
        };

        var result = AdherenceCalculator.Calculate(logs, _now);

        // 1/2 = 50%
        Assert.Equal(50m, result);
    }

    [Fact]
    public void StatusOf_NullConfirmedAt_ReturnsPending()
    {
        var log = Log(_now, null);

        Assert.Equal("PENDING", AdherenceCalculator.StatusOf(log));
    }

    [Fact]
    public void StatusOf_NonNullConfirmedAt_ReturnsTaken()
    {
        var log = Log(_now, _now);

        Assert.Equal("TAKEN", AdherenceCalculator.StatusOf(log));
    }
}