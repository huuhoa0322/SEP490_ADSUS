using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.HealthMonitoring.DTOs;

/// <summary>
/// Request DTO for logging health data (UC-21, FT-35).
/// Based on API Spec Module09 endpoint #55.
/// </summary>
public class LogHealthDataRequest
{
    /// <summary>
    /// Type of health log: EXERCISE or DIET.
    /// </summary>
    [Required(ErrorMessage = "Type is required")]
    public string? Type { get; set; }

    /// <summary>
    /// Free text content describing the exercise or diet.
    /// Must be non-empty after trimming.
    /// </summary>
    [Required(ErrorMessage = "Content is required")]
    public string? Content { get; set; }
}
