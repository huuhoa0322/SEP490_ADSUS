using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Module 7 — test cho AdherenceLevel.FromPercent (phân loại good/warning/poor từ %).
/// Ngưỡng theo AdherenceLevel class: ≥80 = good, ≥50 = warning, còn lại = poor.
/// </summary>
public class AdherenceLevelTests
{
    [Theory]
    [InlineData(100, AdherenceLevel.Good)]
    [InlineData(80, AdherenceLevel.Good)]
    [InlineData(79.99, AdherenceLevel.Warning)]
    [InlineData(50, AdherenceLevel.Warning)]
    [InlineData(49.99, AdherenceLevel.Poor)]
    [InlineData(0, AdherenceLevel.Poor)]
    public void FromPercent_Returns_Correct_Level(decimal percent, string expected)
    {
        Assert.Equal(expected, AdherenceLevel.FromPercent(percent));
    }
}
