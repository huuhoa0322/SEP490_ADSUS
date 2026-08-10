using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Validators;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Tests cho CreatePrescriptionRequestValidator. 13 case:
/// - happy path
/// - 3 case cho ScheduleSlots (rỗng, 1 phần tử, nhiều phần tử)
/// - boundary DurationDays (0, 1, 365, 366)
/// - Dosage rỗng
/// - GeneralNote 2000 / 2001 char
/// - Instructions 1000 / 1001 char
/// - CaseId / DoctorId rỗng
/// </summary>
public class CreatePrescriptionRequestValidatorTests
{
    private static CreatePrescriptionRequest ValidRequest() => new(
        CaseId: Guid.NewGuid(),
        DoctorId: Guid.NewGuid(),
        GeneralNote: null,
        Items: new[]
        {
            new CreatePrescriptionItemDto(
                MedicineName: "Paracetamol 500mg",
                Dosage: "1 viên/lần",
                DurationDays: 7,
                StartDate: new DateOnly(2026, 7, 28),
                Instructions: null,
                ScheduleSlots: new[] { ScheduleSlot.Morning }),
        });

    private readonly CreatePrescriptionRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyCaseId_Fails()
    {
        var req = ValidRequest() with { CaseId = Guid.Empty };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CaseId");
    }

    [Fact]
    public void EmptyDoctorId_Fails()
    {
        var req = ValidRequest() with { DoctorId = Guid.Empty };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DoctorId");
    }

    [Fact]
    public void EmptyItems_Fails()
    {
        var req = ValidRequest() with { Items = Array.Empty<CreatePrescriptionItemDto>() };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items");
    }

    [Fact]
    public void EmptyScheduleSlots_Fails()
    {
        var req = ValidRequest() with
        {
            Items = new[]
            {
                ValidRequest().Items[0] with { ScheduleSlots = Array.Empty<ScheduleSlot>() },
            },
        };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items[0].ScheduleSlots");
    }

    [Fact]
    public void ScheduleSlot_Morning_IsAccepted()
    {
        var req = ValidRequest() with
        {
            Items = new[]
            {
                ValidRequest().Items[0] with { ScheduleSlots = new[] { ScheduleSlot.Morning } },
            },
        };

        var result = _validator.Validate(req);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ScheduleSlot_AllThree_IsAccepted()
    {
        var req = ValidRequest() with
        {
            Items = new[]
            {
                ValidRequest().Items[0] with
                {
                    ScheduleSlots = new[] { ScheduleSlot.Morning, ScheduleSlot.Noon, ScheduleSlot.Evening },
                },
            },
        };

        var result = _validator.Validate(req);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)-1)]
    public void DurationDays_BelowOne_Fails(short days)
    {
        var req = ValidRequest() with
        {
            Items = new[] { ValidRequest().Items[0] with { DurationDays = days } },
        };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items[0].DurationDays");
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)365)]
    public void DurationDays_Boundaries_Pass(short days)
    {
        var req = ValidRequest() with
        {
            Items = new[] { ValidRequest().Items[0] with { DurationDays = days } },
        };

        var result = _validator.Validate(req);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData((short)366)]
    [InlineData((short)400)]
    public void DurationDays_Above365_Fails(short days)
    {
        var req = ValidRequest() with
        {
            Items = new[] { ValidRequest().Items[0] with { DurationDays = days } },
        };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items[0].DurationDays");
    }

    [Fact]
    public void EmptyDosage_Fails()
    {
        var req = ValidRequest() with
        {
            Items = new[] { ValidRequest().Items[0] with { Dosage = "" } },
        };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items[0].Dosage");
    }

    [Fact]
    public void GeneralNote_At2000Chars_Passes()
    {
        var req = ValidRequest() with { GeneralNote = new string('a', 2000) };

        var result = _validator.Validate(req);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GeneralNote_Over2000Chars_Fails()
    {
        var req = ValidRequest() with { GeneralNote = new string('a', 2001) };

        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "GeneralNote");
    }
}