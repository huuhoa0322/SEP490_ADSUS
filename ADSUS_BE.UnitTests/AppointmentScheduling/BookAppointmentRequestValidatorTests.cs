using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Unit tests for BookAppointmentRequestValidator (Module 8 - UC-13).
/// BR-01: ScheduleSlotId is required.
/// </summary>
public class BookAppointmentRequestValidatorTests
{
    private readonly BookAppointmentRequestValidator _validator;

    public BookAppointmentRequestValidatorTests()
    {
        _validator = new BookAppointmentRequestValidator();
    }

    #region Valid Requests

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = "Regular checkup"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidRequestWithoutReason_Passes()
    {
        // Arrange - Reason is optional
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid()
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNullReason_Passes()
    {
        // Arrange
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = null
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region ScheduleSlotId Validation

    [Fact]
    public void Validate_EmptyScheduleSlotId_Fails()
    {
        // Arrange - Guid.Empty is considered empty
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ScheduleSlotId)
            .WithErrorMessage("ScheduleSlotId is required.");
    }

    #endregion

    #region Reason Validation

    [Fact]
    public void Validate_EmptyReason_Passes()
    {
        // Arrange - empty reason is allowed
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = ""
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhitespaceReason_Passes()
    {
        // Arrange - whitespace reason is allowed (nullable field)
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = "   "
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Combined Validation

    [Fact]
    public void Validate_BothFieldsProvided_Passes()
    {
        // Arrange
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = "Annual physical examination"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_BothFieldsEmpty_FailsOnSlotId()
    {
        // Arrange
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.Empty
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ScheduleSlotId);
    }

    #endregion
}
