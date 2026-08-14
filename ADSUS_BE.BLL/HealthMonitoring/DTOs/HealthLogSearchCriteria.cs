namespace ADSUS_BE.BLL.HealthMonitoring.DTOs;

/// <summary>
/// Search criteria for querying health logs (UC-21, FT-35).
/// Based on API Spec Module09 endpoint #56.
/// </summary>
public class HealthLogSearchCriteria
{
    /// <summary>
    /// Date to query logs for, in YYYY-MM-DD format.
    /// Defaults to today (server date) if not provided.
    /// </summary>
    public DateOnly? Date { get; set; }
}
