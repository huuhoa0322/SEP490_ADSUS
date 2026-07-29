namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Response cho GET /api/v1/patients/{id}/adherence (tỉ lệ tuân thủ tổng).
/// Tính bằng AdherenceCalculator.Calculate trên tất cả logs của bệnh nhân
/// trong khoảng thời gian.
/// </summary>
public sealed record AdherenceSummary(
    Guid PatientId,
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalDoses,
    int TakenDoses,
    int PendingDoses,
    decimal AdherencePercent,
    string AdherenceLevel);

/// <summary>
/// Phân loại mức tuân thủ theo §11.3 #4 quyết định mockup:
/// - ≥80%: good (xanh)
/// - 50..&lt;80%: warning (amber)
/// - &lt;50%: poor (đỏ)
/// Backend KHÔNG return error 4xx cho adherence thấp (§11.4 quyết định) —
/// chỉ log + warn UI.
/// </summary>
public static class AdherenceLevel
{
    public const string Good = "good";
    public const string Warning = "warning";
    public const string Poor = "poor";

    public static string FromPercent(decimal pct)
        => pct >= 80m ? Good
         : pct >= 50m ? Warning
         : Poor;
}