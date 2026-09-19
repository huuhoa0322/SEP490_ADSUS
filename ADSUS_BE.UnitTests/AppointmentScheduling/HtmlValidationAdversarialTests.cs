using System;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Validators;
using ADSUS_BE.UnitTests.Common.TestData;
using FluentValidation.TestHelper;
using Xunit;

namespace ADSUS_BE.UnitTests.AppointmentScheduling;

public class HtmlValidationAdversarialTests
{
    private readonly BookAppointmentRequestValidator _bookValidator = new();
    private readonly UpdateAppointmentClinicalInfoRequestValidator _updateValidator = new();

    [Theory]
    [MemberData(nameof(HtmlValidationTestData.CrossPlatformHtmlVectors), MemberType = typeof(HtmlValidationTestData))]
    public void BookValidator_HtmlAndMedicalNotations_EvaluatesCorrectly(string? payload, bool expectedValid, string testLabel)
    {
        Assert.False(string.IsNullOrEmpty(testLabel));
        var request = new BookAppointmentRequest
        {
            ScheduleSlotId = Guid.NewGuid(),
            Reason = payload
        };

        var result = _bookValidator.TestValidate(request);

        if (expectedValid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Reason);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                .WithErrorMessage("Reason cannot contain HTML tags.");
        }
    }

    [Theory]
    [MemberData(nameof(HtmlValidationTestData.CrossPlatformHtmlVectors), MemberType = typeof(HtmlValidationTestData))]
    public void UpdateValidator_HtmlAndMedicalNotations_EvaluatesCorrectly(string? payload, bool expectedValid, string testLabel)
    {
        Assert.False(string.IsNullOrEmpty(testLabel));
        var request = new UpdateAppointmentClinicalInfoRequest
        {
            Reason = payload
        };

        var result = _updateValidator.TestValidate(request);

        if (expectedValid)
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Reason);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                .WithErrorMessage("Reason cannot contain HTML tags.");
        }
    }
}
