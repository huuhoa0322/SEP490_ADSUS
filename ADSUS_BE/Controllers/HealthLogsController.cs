using System.Security.Claims;

using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using ADSUS_BE.BLL.HealthMonitoring.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-21 — Health Monitoring (Module 9).
/// Endpoints cho Patient ghi nhật ký sức khỏe hàng ngày.
/// FT-35: Log daily exercise & diet
/// FT-40: Health log reminder (JOB-03)
/// FT-41: Home-screen health log widget (read-only)
/// </summary>
[ApiController]
[Route("api/v1/health-logs")]
[Authorize(Roles = "PATIENT")]
[Produces("application/json")]
public sealed class HealthLogsController : ControllerBase
{
    private readonly IHealthLogService _healthLogService;
    private readonly IPatientProfileRepository _patientProfileRepo;
    private readonly IValidator<LogHealthDataRequest> _validator;

    public HealthLogsController(
        IHealthLogService healthLogService,
        IPatientProfileRepository patientProfileRepo,
        IValidator<LogHealthDataRequest> validator)
    {
        _healthLogService = healthLogService;
        _patientProfileRepo = patientProfileRepo;
        _validator = validator;
    }

    /// <summary>
    /// Gets the PatientProfileId from JWT.
    /// </summary>
    private async Task<Guid> GetPatientProfileIdAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing NameIdentifier claim.");

        var userGuid = Guid.Parse(userId);
        var profile = await _patientProfileRepo.GetByUserIdAsync(userGuid, ct)
            ?? throw new InvalidOperationException("Patient profile not found.");

        return profile.PatientProfileId;
    }

    /// <summary>
    /// POST /api/v1/health-logs — Log a new health entry (UC-21, FT-35).
    /// BR-02: Patient may add multiple entries per day (accumulate, not overwrite).
    /// Response: 201 Created with HealthLogResponse.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<HealthLogResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogHealthData(
        [FromBody] LogHealthDataRequest? request,
        CancellationToken ct = default)
    {
        // Handle null body
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.Fail(400, "Request body is required."));
        }

        // Validate request
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(400, errors));
        }

        // Get patient profile ID from JWT
        var patientProfileId = await GetPatientProfileIdAsync(ct);

        // Log health data
        var result = await _healthLogService.LogHealthDataAsync(request, patientProfileId, ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<HealthLogResponse>.Ok(result, "Health log saved successfully"));
    }

    /// <summary>
    /// GET /api/v1/health-logs — Get health logs for a specific date (UC-21, FT-35, FT-41).
    /// Query param: date (YYYY-MM-DD, defaults to today).
    /// Response: 200 OK with array of HealthLogResponse, ordered by CreatedAt ASC.
    /// Note: Unpaginated per API Spec F2 (single day bounded).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HealthLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHealthLogs(
        [FromQuery] string? date = null,
        CancellationToken ct = default)
    {
        // Parse date if provided
        DateOnly? parsedDate = null;
        if (!string.IsNullOrEmpty(date))
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d))
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Invalid date format. Use YYYY-MM-DD."));
            }
            parsedDate = d;
        }

        // Get patient profile ID from JWT
        var patientProfileId = await GetPatientProfileIdAsync(ct);

        // Get health logs
        var criteria = new HealthLogSearchCriteria { Date = parsedDate };
        var logs = await _healthLogService.GetHealthLogsAsync(patientProfileId, criteria, ct);

        return Ok(ApiResponse<IReadOnlyList<HealthLogResponse>>.Ok(logs, "Health logs retrieved successfully"));
    }
}
