using System;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

public class HtmlValidationAdversarialTests
{
    private readonly BookAppointmentRequestValidator _bookValidator = new();
    private readonly UpdateAppointmentClinicalInfoRequestValidator _updateValidator = new();

    #region Group 1: Tricky HTML / XSS Vectors Must Fail

    [Theory]
    [InlineData("<SCRIPT>alert(1)</SCRIPT>")]
    [InlineData("<div style=\"display:none\">")]
    [InlineData("<img/src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("<a href=\"javascript:...\">")]
    [InlineData("<br/>")]
    [InlineData("<hr/>")]
    [InlineData("<textarea></textarea>")]
    [InlineData("</br>")]
    [InlineData("</p>")]
    [InlineData("<IMG SRC=\"javascript:alert(1);\">")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<BODY ONLOAD=alert(1)>")]
    [InlineData("<iframe src=\"http://evil.com\">")]
    [InlineData("<STYLE>.evil{display:none}</STYLE>")]
    [InlineData("<details ontoggle=\"alert(1)\">")]
    [InlineData("<b>bold</b>")]
    [InlineData("<INPUT TYPE=\"IMAGE\" SRC=\"javascript:alert(1);\">")]
    public void BookValidator_TrickyHtmlVectors_FailsValidation(string htmlPayload)
    {
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = htmlPayload
        };

        var result = _bookValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage("Reason cannot contain HTML tags.");
    }

    [Theory]
    [InlineData("<SCRIPT>alert(1)</SCRIPT>")]
    [InlineData("<div style=\"display:none\">")]
    [InlineData("<img/src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("<a href=\"javascript:...\">")]
    [InlineData("<br/>")]
    [InlineData("<hr/>")]
    [InlineData("<textarea></textarea>")]
    [InlineData("</br>")]
    [InlineData("</p>")]
    [InlineData("<IMG SRC=\"javascript:alert(1);\">")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<BODY ONLOAD=alert(1)>")]
    [InlineData("<iframe src=\"http://evil.com\">")]
    [InlineData("<STYLE>.evil{display:none}</STYLE>")]
    [InlineData("<details ontoggle=\"alert(1)\">")]
    [InlineData("<b>bold</b>")]
    [InlineData("<INPUT TYPE=\"IMAGE\" SRC=\"javascript:alert(1);\">")]
    public void UpdateValidator_TrickyHtmlVectors_FailsValidation(string htmlPayload)
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = htmlPayload
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage("Reason cannot contain HTML tags.");
    }

    #endregion

    #region Group 2: Medical Notations Must Pass

    [Theory]
    [InlineData("Đau bụng < 3 ngày")]
    [InlineData("Nhiệt độ > 38.5°C")]
    [InlineData("Huyết áp < 120/80 mmHg")]
    [InlineData("SpO2 > 95%")]
    [InlineData("Bạch cầu < 4.0 và SpO2 > 95%")]
    [InlineData("Thân nhiệt <38°C")]
    [InlineData("HbA1c < 6.5%")]
    [InlineData("Bạch cầu <4.0")]
    [InlineData("Glucose < 70 mg/dL")]
    [InlineData("Creatinine <1.2 mg/dL")]
    [InlineData("Kali < 3.5 mEq/L")]
    [InlineData("Tiểu cầu <150 G/L")]
    [InlineData("AST < 35 U/L & ALT < 35 U/L")]
    [InlineData("PaO2 < 60 mmHg, PaCO2 > 45 mmHg")]
    [InlineData("Sốt cao > 39°C kéo dài < 2 ngày")]
    [InlineData("Cân nặng < 2500g")]
    [InlineData("Chiều dài đầu mông < 10mm")]
    public void BookValidator_MedicalNotations_PassesValidation(string medicalNote)
    {
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = medicalNote
        };

        var result = _bookValidator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData("Đau bụng < 3 ngày")]
    [InlineData("Nhiệt độ > 38.5°C")]
    [InlineData("Huyết áp < 120/80 mmHg")]
    [InlineData("SpO2 > 95%")]
    [InlineData("Bạch cầu < 4.0 và SpO2 > 95%")]
    [InlineData("Thân nhiệt <38°C")]
    [InlineData("HbA1c < 6.5%")]
    [InlineData("Bạch cầu <4.0")]
    [InlineData("Glucose < 70 mg/dL")]
    [InlineData("Creatinine <1.2 mg/dL")]
    [InlineData("Kali < 3.5 mEq/L")]
    [InlineData("Tiểu cầu <150 G/L")]
    [InlineData("AST < 35 U/L & ALT < 35 U/L")]
    [InlineData("PaO2 < 60 mmHg, PaCO2 > 45 mmHg")]
    [InlineData("Sốt cao > 39°C kéo dài < 2 ngày")]
    [InlineData("Cân nặng < 2500g")]
    [InlineData("Chiều dài đầu mông < 10mm")]
    public void UpdateValidator_MedicalNotations_PassesValidation(string medicalNote)
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = medicalNote
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    #endregion

    #region Group 3: Null, Empty, and Whitespace Strings Must Pass

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    [InlineData("         ")]
    public void BookValidator_NullEmptyWhitespace_PassesValidation(string? blankReason)
    {
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = blankReason
        };

        var result = _bookValidator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    [InlineData("         ")]
    public void UpdateValidator_NullEmptyWhitespace_PassesValidation(string? blankReason)
    {
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = blankReason
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    #endregion
}
