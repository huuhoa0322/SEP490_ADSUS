using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;

namespace ADSUS_BE.UnitTests.AppointmentScheduling.Validators;

public sealed class CreateScheduleSlotRequestValidatorTests
{
    private readonly CreateScheduleSlotRequestValidator _validator = new();

    private static CreateScheduleSlotRequest ValidRequest() => new(
        DoctorId: Guid.NewGuid(),
        SlotDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        StartTime: "09:00",
        EndTime: "10:00");

    [Fact]
    public void Valid_Passes()
    {
        Assert.True(_validator.Validate(ValidRequest()).IsValid);
    }

    [Fact]
    public void Empty_DoctorId_Fails()
    {
        var r = ValidRequest() with { DoctorId = Guid.Empty };
        Assert.False(_validator.Validate(r).IsValid);
    }

    [Fact]
    public void Past_Date_Fails()
    {
        var r = ValidRequest() with { SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) };
        Assert.False(_validator.Validate(r).IsValid);
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("9:000")]
    [InlineData("25:00")]
    public void BadStartTime_Fails(string start)
    {
        var r = ValidRequest() with { StartTime = start };
        Assert.False(_validator.Validate(r).IsValid);
    }

    [Fact]
    public void Gap_LessThan15Min_Fails()
    {
        var r = ValidRequest() with { StartTime = "09:00", EndTime = "09:14" };
        Assert.False(_validator.Validate(r).IsValid);
    }

    [Fact]
    public void Gap_Exactly15Min_Passes()
    {
        var r = ValidRequest() with { StartTime = "09:00", EndTime = "09:15" };
        Assert.True(_validator.Validate(r).IsValid);
    }

    [Fact]
    public void End_BeforeStart_Fails()
    {
        var r = ValidRequest() with { StartTime = "10:00", EndTime = "09:00" };
        Assert.False(_validator.Validate(r).IsValid);
    }
}