using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Unit tests for CancelAppointmentRequestValidator (Module 8 - UC-14).
/// BR-02: CancellationReason is required.
/// </summary>
public class CancelAppointmentRequestValidatorTests
{
    private readonly CancelAppointmentRequestValidator _validator;

    public CancelAppointmentRequestValidatorTests()
    {
        _validator = new CancelAppointmentRequestValidator();
    }

    #region Valid Requests

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "Schedule conflict - need to reschedule"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MinimumLengthReason_Passes()
    {
        // Arrange - 3 characters minimum
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "Busy"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_LongReason_Passes()
    {
        // Arrange - long but valid reason
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "I need to cancel due to an emergency situation that requires immediate attention and I will contact the clinic later to reschedule."
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Empty/Null Validation

    [Fact]
    public void Validate_EmptyReason_Fails()
    {
        // Arrange
        var request = new CancelAppointmentRequest
        {
            CancellationReason = ""
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CancellationReason)
            .WithErrorMessage("Cancellation reason is required.");
    }

    [Fact]
    public void Validate_WhitespaceOnly_Fails()
    {
        // Arrange
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "   "
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CancellationReason)
            .WithErrorMessage("Cancellation reason is required.");
    }

    [Fact]
    public void Validate_WhitespaceWithTabsAndNewlines_Fails()
    {
        // Arrange
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "\t\n  \t\n"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CancellationReason);
    }

    #endregion

    #region Length Validation

    [Theory]
    [InlineData("AB")]     // Too short - 2 chars
    [InlineData("A")]      // Too short - 1 char
    [InlineData("")]       // Empty
    public void Validate_TooShort_Fails(string reason)
    {
        // Arrange
        var request = new CancelAppointmentRequest
        {
            CancellationReason = reason
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CancellationReason);
    }

    [Fact]
    public void Validate_Exactly3Characters_Passes()
    {
        // Arrange - exactly 3 characters should pass
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "abc"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Vietnamese Characters Validation

    [Fact]
    public void Validate_VietnameseCharacters_Passes()
    {
        // Arrange - Vietnamese text with diacritics
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "Tôi cần hủy lịch khám vì có việc đột xuất"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_VietnameseWithSpecialChars_Passes()
    {
        // Arrange - Vietnamese with various special characters
        var request = new CancelAppointmentRequest
        {
            CancellationReason = "Cần hủy - có việc gấp, sẽ gọi lại sau!"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
