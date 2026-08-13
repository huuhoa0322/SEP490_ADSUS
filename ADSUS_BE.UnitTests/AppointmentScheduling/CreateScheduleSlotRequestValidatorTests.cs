using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Unit tests for CreateScheduleSlotRequestValidator (Module 8 - UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); StartTime < EndTime; range > 15 phút.
/// </summary>
public class CreateScheduleSlotRequestValidatorTests
{
    private readonly CreateScheduleSlotRequestValidator _validator;

    public CreateScheduleSlotRequestValidatorTests()
    {
        _validator = new CreateScheduleSlotRequestValidator();
    }

    #region Valid Requests

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_Exactly16Minutes_Passes()
    {
        // Arrange - duration exactly 16 minutes (greater than 15)
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 16), // 16 minutes
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x);
    }

    #endregion

    #region Date Validation

    [Fact]
    public void Validate_MissingVisitDate_Fails()
    {
        // Arrange
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = default,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.VisitDate)
            .WithErrorMessage("VisitDate is required.");
    }

    [Fact]
    public void Validate_PastDate_Fails()
    {
        // Arrange - date in the past
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Slot start time must be in the future.");
    }

    [Fact]
    public void Validate_TodayButPastTime_Fails()
    {
        // Arrange - today but time has already passed
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            StartTime = new TimeOnly(0, 1), // Past time
            EndTime = new TimeOnly(1, 0),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Slot start time must be in the future.");
    }

    #endregion

    #region Time Validation

    [Fact]
    public void Validate_MissingStartTime_Fails()
    {
        // Arrange
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = default,
            EndTime = new TimeOnly(10, 0),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StartTime)
            .WithErrorMessage("StartTime is required.");
    }

    [Fact]
    public void Validate_MissingEndTime_Fails()
    {
        // Arrange
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = default,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EndTime)
            .WithErrorMessage("EndTime is required.");
    }

    [Fact]
    public void Validate_StartTimeEqualsEndTime_Fails()
    {
        // Arrange
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 0), // Same time
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("StartTime must be earlier than EndTime.");
    }

    [Fact]
    public void Validate_StartTimeAfterEndTime_Fails()
    {
        // Arrange
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(9, 0), // End before Start
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("StartTime must be earlier than EndTime.");
    }

    #endregion

    #region Duration Validation

    [Fact]
    public void Validate_Exactly15Minutes_Fails()
    {
        // Arrange - duration exactly 15 minutes (should fail - must be greater than 15)
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 15), // Exactly 15 minutes
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Slot duration must be greater than 15 minutes.");
    }

    [Fact]
    public void Validate_14Minutes_Fails()
    {
        // Arrange - duration less than 15 minutes
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 14), // 14 minutes
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Slot duration must be greater than 15 minutes.");
    }

    [Fact]
    public void Validate_1MinuteDuration_Fails()
    {
        // Arrange - very short duration
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 1), // 1 minute
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Slot duration must be greater than 15 minutes.");
    }

    #endregion

    #region Boundary Value Analysis

    [Theory]
    [InlineData(15, 0, 15, 16, true)]   // 16 min - Pass
    [InlineData(15, 0, 15, 0, false)]  // 0 min - Fail
    [InlineData(15, 0, 14, 59, false)] // end before start - Fail
    [InlineData(0, 0, 0, 16, true)]    // 16 min - Pass
    public void Validate_DurationBoundaryCases_ExpectedResult(
        int startHour, int startMin, int endHour, int endMin, bool shouldPass)
    {
        // Arrange
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(startHour, startMin),
            EndTime = new TimeOnly(endHour, endMin),
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        if (shouldPass)
        {
            result.ShouldNotHaveValidationErrorFor(x => x);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("Slot duration must be greater than 15 minutes.");
        }
    }

    #endregion

    #region Combined Validation

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange - multiple invalid fields
        var request = new CreateScheduleSlotRequest
        {
            VisitDate = default,
            StartTime = default,
            EndTime = default,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.VisitDate);
        result.ShouldHaveValidationErrorFor(x => x.StartTime);
        result.ShouldHaveValidationErrorFor(x => x.EndTime);
    }

    #endregion

    #region Helper Methods

    private static CreateScheduleSlotRequest CreateValidRequest()
    {
        return new CreateScheduleSlotRequest
        {
            VisitDate = GetFutureDate(),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
        };
    }

    private static DateOnly GetFutureDate()
    {
        var today = DateTime.UtcNow.Date;
        // Get a date at least 2 days in the future to ensure time validation passes
        return DateOnly.FromDateTime(today.AddDays(2));
    }

    #endregion
}
