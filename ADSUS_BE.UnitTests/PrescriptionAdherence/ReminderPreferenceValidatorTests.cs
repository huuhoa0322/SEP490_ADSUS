using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Validators;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho ReminderPreferenceValidator.
/// Validate giờ nhắc trong PUT /api/v1/me/reminder-preference.
/// Slot ranges: Sáng 05:00–10:59, Trưa 11:00–16:59, Tối 17:00–23:59.
/// </summary>
public class ReminderPreferenceValidatorTests
{
    private readonly ReminderPreferenceValidator _validator = new();

    private static UpdateReminderPreferenceRequest ValidBase() => new(
        NotifEnabled: true,
        MorningTime: "07:00",
        MiddayTime: "12:00",
        EveningTime: "20:00");

    // --- MorningTime boundary ---

    [Fact]
    public void ValidMorningTime_07_00_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "07:00" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidMorningTime_10_59_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "10:59" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidMorningTime_05_00_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "05:00" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidMorningTime_04_59_Fails()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "04:59" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MorningTime");
    }

    [Fact]
    public void InvalidMorningTime_11_00_Fails()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "11:00" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MorningTime");
    }

    [Fact]
    public void InvalidMorningTime_NonNumeric_Fails()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "abc" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void InvalidMorningTime_Garbage_Fails()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = "not-a-time" });
        Assert.False(result.IsValid);
    }

    // --- MiddayTime boundary ---

    [Fact]
    public void ValidMiddayTime_11_00_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MiddayTime = "11:00" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidMiddayTime_16_59_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MiddayTime = "16:59" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidMiddayTime_12_00_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MiddayTime = "12:00" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidMiddayTime_10_59_Fails()
    {
        var result = _validator.Validate(ValidBase() with { MiddayTime = "10:59" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MiddayTime");
    }

    [Fact]
    public void InvalidMiddayTime_17_00_Fails()
    {
        var result = _validator.Validate(ValidBase() with { MiddayTime = "17:00" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MiddayTime");
    }

    // --- EveningTime boundary ---

    [Fact]
    public void ValidEveningTime_17_00_Passes()
    {
        var result = _validator.Validate(ValidBase() with { EveningTime = "17:00" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidEveningTime_23_59_Passes()
    {
        var result = _validator.Validate(ValidBase() with { EveningTime = "23:59" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidEveningTime_20_00_Passes()
    {
        var result = _validator.Validate(ValidBase() with { EveningTime = "20:00" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidEveningTime_16_59_Fails()
    {
        var result = _validator.Validate(ValidBase() with { EveningTime = "16:59" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EveningTime");
    }

    [Fact]
    public void InvalidEveningTime_00_00_Fails()
    {
        var result = _validator.Validate(ValidBase() with { EveningTime = "00:00" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EveningTime");
    }

    // --- Null values pass (optional fields) ---

    [Fact]
    public void NullMorningTime_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MorningTime = null });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void NullMiddayTime_Passes()
    {
        var result = _validator.Validate(ValidBase() with { MiddayTime = null });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void NullEveningTime_Passes()
    {
        var result = _validator.Validate(ValidBase() with { EveningTime = null });
        Assert.True(result.IsValid);
    }

    // --- Multiple errors ---

    [Fact]
    public void AllTimesInvalid_MultipleErrors()
    {
        var request = new UpdateReminderPreferenceRequest(
            NotifEnabled: null,
            MorningTime: "04:00",
            MiddayTime: "10:00",
            EveningTime: "00:00");

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }
}
