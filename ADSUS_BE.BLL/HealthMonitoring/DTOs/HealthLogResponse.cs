namespace ADSUS_BE.BLL.HealthMonitoring.DTOs;

/// <summary>
/// Response DTO for health log entries.
/// Based on API Spec Module09 endpoints #55 and #56.
/// </summary>
public class HealthLogResponse
{
    /// <summary>
    /// Unique identifier of the health log.
    /// </summary>
    public Guid HealthLogId { get; set; }

    /// <summary>
    /// The patient profile ID that owns this log.
    /// </summary>
    public Guid PatientProfileId { get; set; }

    /// <summary>
    /// Date of the log entry (server date).
    /// </summary>
    public DateOnly LogDate { get; set; }

    /// <summary>
    /// Type: EXERCISE or DIET.
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Content describing the exercise or diet.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Timestamp when the log was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
