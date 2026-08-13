using System.Security.Claims;

using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-13, UC-14 — Appointment Scheduling (Module 8).
/// Endpoints cho Patient đặt lịch và xem/hủy lịch hẹn.
/// </summary>
[ApiController]
[Route("api/v1/appointments")]
[Authorize(Roles = "PATIENT")]
[Produces("application/json")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;
    private readonly IPatientProfileRepository _patientProfileRepo;

    public AppointmentsController(
        IAppointmentService appointmentService,
        IPatientProfileRepository patientProfileRepo)
    {
        _appointmentService = appointmentService;
        _patientProfileRepo = patientProfileRepo;
    }

    /// <summary>Lấy PatientProfileId từ JWT.</summary>
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
    /// GET /api/v1/appointments/slots — Danh sách slot còn trống (UC-13).
    /// BR-02: Chỉ trả về slot OPEN.
    /// Giới hạn: trong vòng 2 tuần.
    /// </summary>
    [HttpGet("slots")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OpenSlotResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOpenSlots(
        [FromQuery] string? doctorId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var slots = await _appointmentService.ListOpenSlotsAsync(doctorId, fromDate, toDate, ct);
        return Ok(ApiResponse<IReadOnlyList<OpenSlotResponse>>.Ok(slots));
    }

    /// <summary>
    /// GET /api/v1/appointments — Danh sách lịch hẹn của tôi (UC-14).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyAppointments(
        [FromQuery] AppointmentStatus? status = null,
        CancellationToken ct = default)
    {
        var patientProfileId = await GetPatientProfileIdAsync(ct);
        var appointments = await _appointmentService.ListMyAppointmentsAsync(patientProfileId, status, ct);
        return Ok(ApiResponse<IReadOnlyList<AppointmentSummaryResponse>>.Ok(appointments));
    }

    /// <summary>
    /// GET /api/v1/appointments/{id} — Chi tiết lịch hẹn.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var appointment = await _appointmentService.GetByIdAsync(id, ct);
        if (appointment == null)
            return NotFound(ApiResponse<object>.Fail(404, $"Appointment '{id}' not found."));
        return Ok(ApiResponse<AppointmentResponse>.Ok(appointment));
    }

    /// <summary>
    /// POST /api/v1/appointments — Đặt lịch hẹn mới (UC-13).
    /// BR-01: Slot phải tồn tại và có status = OPEN.
    /// BR-02: Patient không được đặt trùng slot đã có BOOKED appointment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BookAppointment(
        [FromBody] BookAppointmentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var patientProfileId = await GetPatientProfileIdAsync(ct);
            var appointment = await _appointmentService.BookAppointmentAsync(patientProfileId, request, ct);
            return CreatedAtAction(
                nameof(GetById),
                new { id = appointment.AppointmentId },
                ApiResponse<AppointmentResponse>.Ok(appointment));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/v1/appointments/{id}/cancel — Hủy lịch hẹn (UC-14).
    /// BR-01: Chỉ patient sở hữu mới được hủy.
    /// BR-02: Lý do hủy bắt buộc.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(
        Guid id,
        [FromBody] CancelAppointmentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var patientProfileId = await GetPatientProfileIdAsync(ct);
            var appointment = await _appointmentService.CancelAppointmentAsync(id, patientProfileId, request, ct);
            return Ok(ApiResponse<AppointmentResponse>.Ok(appointment));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(403, ex.Message));
        }
    }
}
