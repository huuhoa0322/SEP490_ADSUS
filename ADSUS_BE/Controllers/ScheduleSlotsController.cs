using System.Security.Claims;

using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-15 — Manage Clinic Schedule (Module 8 — Appointment Scheduling).
/// Allowed roles: Doctor (Doctor tự quản lý lịch của chính mình).
/// BR-01: VisitDate + StartTime > now (UTC); range > 15 phút; không overlap.
/// BR-02: Closed là terminal.
/// Hệ thống tự sinh ca mặc định T2-T6 (8h-12h, 13h-17h) khi Doctor mở trang lần đầu.
/// </summary>
[ApiController]
[Route("api/v1/schedule-slots")]
[Authorize(Roles = "DOCTOR")]
[Produces("application/json")]
public sealed class ScheduleSlotsController : ControllerBase
{
    private readonly IScheduleSlotService _slots;

    public ScheduleSlotsController(IScheduleSlotService slots)
    {
        _slots = slots;
    }

    /// <summary>JWT NameIdentifier claim chứa UserId.</summary>
    private Guid CurrentDoctorId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing NameIdentifier claim."));

    /// <summary>GET /api/v1/schedule-slots — Danh sách slot của Doctor đang đăng nhập.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] SlotStatus? status = null,
        CancellationToken ct = default)
    {
        // R5: Doctor chỉ xem được lịch của chính mình, bỏ qua doctorId query.
        var slots = await _slots.ListSlotsAsync(
            fromDate, toDate, CurrentDoctorId, status, ct);
        return Ok(ApiResponse<IReadOnlyList<ScheduleSlotResponse>>.Ok(slots));
    }

    /// <summary>GET /api/v1/schedule-slots/{id}</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var slot = await _slots.GetSlotAsync(id, ct);
        if (slot is null)
            return NotFound(ApiResponse<object>.Fail(404, $"Slot '{id}' not found."));
        if (slot.DoctorId != CurrentDoctorId)
            return StatusCode(403, ApiResponse<object>.Fail(403, "Not your slot."));
        return Ok(ApiResponse<ScheduleSlotResponse>.Ok(slot));
    }

    /// <summary>POST /api/v1/schedule-slots — Tạo slot cho chính mình.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateScheduleSlotRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var slot = await _slots.CreateSlotAsync(CurrentDoctorId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = slot.SlotId },
                ApiResponse<ScheduleSlotResponse>.Ok(slot));
        }
        catch (ValidationException ex)
        {
            var firstError = ex.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed.";
            return BadRequest(ApiResponse<object>.Fail(400, $"Validation failed: {firstError}"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }

    /// <summary>PUT /api/v1/schedule-slots/{id} — Sửa giờ slot (tách ca).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateScheduleSlotRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Pre-check quyền sở hữu.
            var existing = await _slots.GetSlotAsync(id, ct);
            if (existing is null)
                return NotFound(ApiResponse<object>.Fail(404, $"Slot '{id}' not found."));
            if (existing.DoctorId != CurrentDoctorId)
                return StatusCode(403, ApiResponse<object>.Fail(403, "Not your slot."));

            var slot = await _slots.UpdateSlotAsync(id, request, ct);
            return Ok(ApiResponse<ScheduleSlotResponse>.Ok(slot));
        }
        catch (ValidationException ex)
        {
            var firstError = ex.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed.";
            return BadRequest(ApiResponse<object>.Fail(400, $"Validation failed: {firstError}"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }

    /// <summary>PUT /api/v1/schedule-slots/{id}/close — Đóng slot (xin nghỉ/bận).</summary>
    [HttpPut("{id:guid}/close")]
    [ProducesResponseType(typeof(ApiResponse<CloseSlotImpactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(
        Guid id,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            var existing = await _slots.GetSlotAsync(id, ct);
            if (existing is null)
                return NotFound(ApiResponse<object>.Fail(404, $"Slot '{id}' not found."));
            if (existing.DoctorId != CurrentDoctorId)
                return StatusCode(403, ApiResponse<object>.Fail(403, "Not your slot."));

            var impact = await _slots.CloseSlotAsync(id, force, ct);
            if (impact.AffectedBookingsCount > 0 && !force)
            {
                return Conflict(ApiResponse<CloseSlotImpactResponse>.Fail(
                    409,
                    $"Slot '{id}' has {impact.AffectedBookingsCount} active booking(s). " +
                    "Pass ?force=true to confirm closing anyway."));
            }
            return Ok(ApiResponse<CloseSlotImpactResponse>.Ok(impact));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(ApiResponse<object>.Fail(404, ex.Message));
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/v1/schedule-slots/ensure-default — Tự sinh ca mặc định T2-T6 (8h-12h, 13h-17h)
    /// cho tuần bắt đầu weekStart. Idempotent. Doctor gọi khi mở trang lần đầu.
    /// </summary>
    [HttpPost("ensure-default")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnsureDefault(
        [FromQuery] DateOnly weekStart,
        CancellationToken ct = default)
    {
        try
        {
            await _slots.EnsureDefaultSlotsAsync(CurrentDoctorId, weekStart, ct);
            return Ok(ApiResponse<object>.Ok(null, "Default slots ensured."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }
}