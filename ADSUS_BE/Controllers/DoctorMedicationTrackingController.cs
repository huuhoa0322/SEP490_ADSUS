using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;
using ADSUS_BE.BLL.DoctorMedicationTracking.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/me/medication-tracking")]
[Authorize(Roles = "DOCTOR")]
[Produces("application/json")]
public sealed class DoctorMedicationTrackingController : ControllerBase
{
    private readonly IDoctorMedicationTrackingService _service;

    public DoctorMedicationTrackingController(IDoctorMedicationTrackingService service)
    {
        _service = service;
    }

    [HttpGet("patients")]
    [ProducesResponseType(typeof(ApiResponse<DoctorPatientListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientList(
        [FromQuery] string? search,
        [FromQuery] string? adherenceLevel,
        [FromQuery] bool? hasOverdueDoses,
        CancellationToken ct = default)
    {
        var doctorId = GetDoctorId();
        var result = await _service.GetPatientListAsync(doctorId, search, adherenceLevel, hasOverdueDoses, DateTime.UtcNow, ct);
        return Ok(ApiResponse<DoctorPatientListResponse>.Ok(result, "Patient list retrieved successfully"));
    }

    [HttpGet("patients/{patientId:guid}/prescriptions")]
    [ProducesResponseType(typeof(ApiResponse<PatientPrescriptionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientPrescriptions(
        Guid patientId,
        CancellationToken ct = default)
    {
        var doctorId = GetDoctorId();
        var result = await _service.GetPatientDetailAsync(doctorId, patientId, null, ct);
        return Ok(ApiResponse<PatientPrescriptionDetailResponse>.Ok(result, "Patient prescriptions retrieved successfully"));
    }

    [HttpPost("patients/{patientId:guid}/remind")]
    [ProducesResponseType(typeof(ApiResponse<RemindResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendReminders(
        Guid patientId,
        [FromBody] RemindRequest request,
        CancellationToken ct = default)
    {
        var doctorId = GetDoctorId();
        var result = await _service.SendRemindersAsync(doctorId, patientId, request, null, ct);
        return Ok(ApiResponse<RemindResponse>.Ok(result, result.Message));
    }

    private Guid GetDoctorId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Invalid access token.");
        return Guid.Parse(idStr);
    }
}
