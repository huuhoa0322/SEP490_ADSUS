using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using ADSUS_BE.BLL.HealthMonitoring.Validators;

namespace ADSUS_BE.UnitTests.HealthMonitoring;

/// <summary>
/// Tests cho LogHealthDataRequestValidator.
/// Based on API Spec Module09 + UC-21 BR rules:
/// - type: required, must be EXERCISE or DIET
/// - content: required, non-empty after trim
///
/// Test cases (13 total):
/// - Happy path: EXERCISE, DIET (case-insensitive)
/// - Type validation: null, empty, invalid value
/// - Content validation: null, empty, whitespace only
/// - Boundary: trimmed content passes, very long content passes
/// - Combined: both fields missing
/// </summary>
public class LogHealthDataRequestValidatorTests
{
    private readonly LogHealthDataRequestValidator _validator = new();

    #region Happy Path Tests

    [Fact]
    public void ValidExercise_Passes()
    {
        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = "Walked 30 minutes"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidDiet_Passes()
    {
        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "Ate vegetables"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("exercise")]
    [InlineData("Exercise")]
    [InlineData("EXERCISE")]
    [InlineData("ExErCiSe")]
    public void LowercaseExercise_Passes(string type)
    {
        var request = new LogHealthDataRequest
        {
            Type = type,
            Content = "Running"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("diet")]
    [InlineData("Diet")]
    [InlineData("DIET")]
    [InlineData("dIeT")]
    public void LowercaseDiet_Passes(string type)
    {
        var request = new LogHealthDataRequest
        {
            Type = type,
            Content = "Salad"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    #endregion

    #region Type Validation Tests

    [Fact]
    public void NullType_Fails()
    {
        var request = new LogHealthDataRequest
        {
            Type = null!,
            Content = "Test"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Type");
    }

    [Fact]
    public void EmptyType_Fails()
    {
        var request = new LogHealthDataRequest
        {
            Type = "",
            Content = "Test"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Type");
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("SLEEP")]
    [InlineData("WATER")]
    [InlineData("exercisee")]
    [InlineData("diett")]
    [InlineData("123")]
    [InlineData("   ")]
    public void InvalidType_Fails(string invalidType)
    {
        var request = new LogHealthDataRequest
        {
            Type = invalidType,
            Content = "Test"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Type");
    }

    #endregion

    #region Content Validation Tests

    [Fact]
    public void NullContent_Fails()
    {
        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = null!
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Content");
    }

    [Fact]
    public void EmptyContent_Fails()
    {
        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Content");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t \n ")]
    public void WhitespaceOnlyContent_Fails(string whitespaceContent)
    {
        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = whitespaceContent
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Content");
    }

    [Fact]
    public void TrimmedContent_Passes()
    {
        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "  Salad  "
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void VeryLongContent_Passes()
    {
        var request = new LogHealthDataRequest
        {
            Type = "EXERCISE",
            Content = new string('a', 5000)
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UnicodeContent_Passes()
    {
        var request = new LogHealthDataRequest
        {
            Type = "DIET",
            Content = "Bữa sáng: phở, 500ml nước lọc"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    #endregion

    #region Combined Validation Tests

    [Fact]
    public void BothFieldsMissing_Fails()
    {
        var request = new LogHealthDataRequest
        {
            Type = null!,
            Content = null!
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Type");
        Assert.Contains(result.Errors, e => e.PropertyName == "Content");
    }

    [Fact]
    public void BothFieldsEmpty_Fails()
    {
        var request = new LogHealthDataRequest
        {
            Type = "",
            Content = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Type");
        Assert.Contains(result.Errors, e => e.PropertyName == "Content");
    }

    #endregion
}
