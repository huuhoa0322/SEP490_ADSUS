using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

/// <summary>
/// Unit tests for UpdateAppointmentClinicalInfoRequestValidator.
/// </summary>
public class UpdateAppointmentClinicalInfoRequestValidatorTests
{
    private readonly UpdateAppointmentClinicalInfoRequestValidator _validator;

    public UpdateAppointmentClinicalInfoRequestValidatorTests()
    {
        _validator = new UpdateAppointmentClinicalInfoRequestValidator();
    }

    [Fact]
    public void Validate_NullReason_Passes()
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyReason_Passes()
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = ""
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhitespaceReason_Passes()
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = "   "
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Đau bụng nhẹ")]
    [InlineData("Nhiệt độ < 38°C")]
    [InlineData("Bạch cầu < 4.0 và SpO2 > 95%")]
    [InlineData("HA > 140/90 mmHg")]
    [InlineData("Thân nhiệt <38°C")]
    public void Validate_MedicalInequalityAndPlainTextReason_Passes(string validReason)
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = validReason
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("<html>")]
    [InlineData("<b>Đau bụng</b>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("</div>")]
    [InlineData("<iframe src='evil.com'></iframe>")]
    public void Validate_HtmlInReason_Fails(string htmlReason)
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = htmlReason
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage("Reason cannot contain HTML tags.");
    }
}
