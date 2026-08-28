using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho MedicationIntakeScheduleGenerator.
/// Kiểm tra logic skip liều đã qua trong ngày đầu tiên (Bug fix: UC-18 sinh liều overtime).
///
/// TimeConversion (ClinicClock.Offset = +7h):
///   ScheduledUtc = (date + TimeOnly) - Offset
///   TimeOnly values are CLINIC LOCAL TIME (ICT), NOT UTC.
///   Converting to UTC: UTC = TimeOnly - 7h
///
/// Test boundary: _frozenUtc = 08:00 UTC on 2026-08-28
///   - Any slot with UTC &lt;= 08:00 on day 0 → SKIP
///   - Any slot with UTC &gt; 08:00 on day 0 → GENERATE
///   - All slots on dayOffset >= 1 → GENERATE (no skip)
/// </summary>
public class MedicationIntakeScheduleGeneratorTests
{
    // Frozen time: 2026-08-28 08:00 UTC = 15:00 ICT
    // All TimeOnly values in this file are CLINIC LOCAL TIME (ICT = UTC+7).
    private static readonly DateTime _frozenUtc = new(2026, 8, 28, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly _today = DateOnly.FromDateTime(_frozenUtc); // 2026-08-28

    private readonly MedicationIntakeScheduleGenerator _generator = new();

    private async Task<IReadOnlyList<ScheduledDose>> GenerateAsync(TimeOnly morning, TimeOnly midday, TimeOnly evening, short durationDays = 1)
    {
        var item = new PrescriptionItemWithPatient(
            PrescriptionItemId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PatientProfileId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            StartDate: _today,
            DurationDays: durationDays);

        return await _generator.GenerateAsync(
            item,
            slots: new[] { ScheduleSlot.Morning, ScheduleSlot.Noon, ScheduleSlot.Evening },
            patientMorningTime: morning,
            patientMiddayTime: midday,
            patientEveningTime: evening,
            _frozenUtc,
            CancellationToken.None);
    }

    /// <summary>
    /// Doctor prescribes at 15:00 ICT (08:00 UTC) with DurationDays=1.
    /// TimeOnly = CLINIC LOCAL (ICT).
    /// morning=15:30 ICT → 09:00 UTC → > utcNow → GENERATE
    /// midday=15:30 ICT → 08:30 UTC → > utcNow → GENERATE
    /// evening=20:00 ICT → 13:00 UTC → > utcNow → GENERATE
    /// All 3 generated for today (using &lt;= to skip means strictly before utcNow).
    /// </summary>
    [Fact]
    public async Task Generate_DurationOne_PrescribeAfterMorningAndNoon_SkipsMorningAndNoon()
    {
        var morning = new TimeOnly(15, 30); // 08:30 UTC — strictly after utcNow=08:00 UTC
        var midday   = new TimeOnly(15, 30); // 08:30 UTC
        var evening  = new TimeOnly(20, 0);  // 13:00 UTC

        var result = await GenerateAsync(morning, midday, evening, durationDays: 1);

        Assert.Equal(3, result.Count);
        Assert.All(result, d => Assert.Equal(_today, DateOnly.FromDateTime(d.ScheduledTimeUtc)));
    }

    /// <summary>
    /// DurationDays=2, same times as above.
    /// dayOffset=0: all 3 generated → 3 today
    /// dayOffset=1: all 3 generated → 3 tomorrow
    /// Total: 6
    /// </summary>
    [Fact]
    public async Task Generate_DurationTwo_PrescribeAfterMorningAndNoon_GeneratesAllSlotsTomorrow()
    {
        var morning = new TimeOnly(15, 30);
        var midday   = new TimeOnly(15, 30);
        var evening  = new TimeOnly(20, 0);

        var result = await GenerateAsync(morning, midday, evening, durationDays: 2);

        Assert.Equal(6, result.Count);

        var todayDoses = result.Where(d => DateOnly.FromDateTime(d.ScheduledTimeUtc) == _today).ToList();
        Assert.Equal(3, todayDoses.Count);

        var tomorrow = _today.AddDays(1);
        var tomorrowDoses = result.Where(d => DateOnly.FromDateTime(d.ScheduledTimeUtc) == tomorrow).ToList();
        Assert.Equal(3, tomorrowDoses.Count);
    }

    /// <summary>
    /// All slots strictly AFTER 08:00 UTC on day 0.
    /// morning=17:00 ICT → 10:00 UTC, midday=18:00 ICT → 11:00 UTC, evening=22:00 ICT → 15:00 UTC.
    /// All UTC &gt; 08:00 → all 3 generated for day 0.
    /// </summary>
    [Fact]
    public async Task Generate_AllSlotsAfterFrozenTime_GeneratesAllThreeToday()
    {
        var morning = new TimeOnly(17, 0);  // 10:00 UTC
        var midday   = new TimeOnly(18, 0); // 11:00 UTC
        var evening  = new TimeOnly(22, 0); // 15:00 UTC

        var result = await GenerateAsync(morning, midday, evening, durationDays: 1);

        Assert.Equal(3, result.Count);
        Assert.All(result, d => Assert.Equal(_today, DateOnly.FromDateTime(d.ScheduledTimeUtc)));
    }

    /// <summary>
    /// DurationDays=2, Morning-only slot.
    /// morning=15:00 ICT → 08:00 UTC → = utcNow → SKIPPED (≤)
    /// dayOffset=1: 08:00 UTC next day → GENERATED
    /// </summary>
    [Fact]
    public async Task Generate_MorningOnly_PrescribeAfterMorning_SkipsMorningToday_GeneratesMorningTomorrow()
    {
        var morning = new TimeOnly(15, 0); // 08:00 UTC, = utcNow → skipped

        var item = new PrescriptionItemWithPatient(
            PrescriptionItemId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PatientProfileId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            StartDate: _today,
            DurationDays: 2);

        var result = await _generator.GenerateAsync(
            item,
            slots: new[] { ScheduleSlot.Morning },
            patientMorningTime: morning,
            patientMiddayTime: default,
            patientEveningTime: default,
            _frozenUtc,
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(_today.AddDays(1), DateOnly.FromDateTime(result[0].ScheduledTimeUtc));
    }

    /// <summary>
    /// Evening-only slot always generates (evening 20:00 ICT = 13:00 UTC &gt; 08:00 UTC).
    /// </summary>
    [Fact]
    public async Task Generate_EveningOnly_PrescribeAfterMorningAndNoon_GeneratesEveningToday()
    {
        var evening = new TimeOnly(20, 0); // 13:00 UTC

        var item = new PrescriptionItemWithPatient(
            PrescriptionItemId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PatientProfileId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            StartDate: _today,
            DurationDays: 1);

        var result = await _generator.GenerateAsync(
            item,
            slots: new[] { ScheduleSlot.Evening },
            patientMorningTime: default,
            patientMiddayTime: default,
            patientEveningTime: evening,
            _frozenUtc,
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(_today, DateOnly.FromDateTime(result[0].ScheduledTimeUtc));
        Assert.Equal(13, result[0].ScheduledTimeUtc.Hour);
    }

    /// <summary>
    /// All 3 slots scheduled at or before 08:00 UTC on day 0.
    /// morning=07:00 ICT → 00:00 UTC (≤08:00 → SKIP)
    /// midday=08:00 ICT → 01:00 UTC (≤08:00 → SKIP)
    /// evening=09:00 ICT → 02:00 UTC (≤08:00 → SKIP)
    /// All 3 skipped → empty result for today.
    /// </summary>
    [Fact]
    public async Task Generate_AllThreePast_AllDosesSkipped_TodayHasNoDoses()
    {
        var morning = new TimeOnly(7, 0);   // 00:00 UTC
        var midday   = new TimeOnly(8, 0);  // 01:00 UTC
        var evening  = new TimeOnly(9, 0);  // 02:00 UTC

        var item = new PrescriptionItemWithPatient(
            PrescriptionItemId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PatientProfileId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            StartDate: _today,
            DurationDays: 1);

        var result = await _generator.GenerateAsync(
            item,
            slots: new[] { ScheduleSlot.Morning, ScheduleSlot.Noon, ScheduleSlot.Evening },
            patientMorningTime: morning,
            patientMiddayTime: midday,
            patientEveningTime: evening,
            _frozenUtc,
            CancellationToken.None);

        Assert.Empty(result);
    }
}
