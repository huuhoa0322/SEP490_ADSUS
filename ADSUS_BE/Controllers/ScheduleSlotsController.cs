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
/// Allowed roles: Doctor, Nurse (per UC-15 §"Allowed Roles").
/// BR-01: slot không trong quá khứ; range > 15 phút; không overlap cùng Doctor.
/// BR-02: Closed là terminal.
/// </summary>
[ApiController]
[Route("api/v1/schedule-slots")]
[Authorize(Roles = "DOCTOR,NURSE")]
[Produces("application/json")]
public sealed class ScheduleSlotsController : ControllerBase
{
    private readonly IScheduleSlotService _slots;

    public ScheduleSlotsController(IScheduleSlotService slots)
    {
        _slots = slots;
    }

    /// <summary>
    /// GET /api/v1/schedule-slots — Danh sách slot theo khoảng ngày (default: hôm nay → +30 ngày).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] SlotStatus? status = null,
        CancellationToken ct = default)
    {
        var slots = await _slots.ListSlotsAsync(fromDate, toDate, doctorId, status, ct);
        return Ok(ApiResponse<IReadOnlyList<ScheduleSlotResponse>>.Ok(slots));
    }

    /// <summary>
    /// GET /api/v1/schedule-slots/{id} — Chi tiết 1 slot.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var slot = await _slots.GetSlotAsync(id, ct);
        if (slot is null)
        {
            return NotFound(ApiResponse<object>.Fail(404, $"Slot '{id}' not found."));
        }
        return Ok(ApiResponse<ScheduleSlotResponse>.Ok(slot));
    }

    /// <summary>
    /// POST /api/v1/schedule-slots — Tạo slot mới (Doctor/Nurse).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateScheduleSlotRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var slot = await _slots.CreateSlotAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = slot.SlotId },
                ApiResponse<ScheduleSlotResponse>.Ok(slot));
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            // ApiResponse không có 3 tham số; truyền message kèm key đầu tiên.
            var firstError = ex.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed.";
            return BadRequest(ApiResponse<object>.Fail(400, $"Validation failed: {firstError}"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }

    /// <summary>
    /// PUT /api/v1/schedule-slots/{id}/close — Đóng slot.
    /// UC-15 AF-02: nếu slot có booking Booked, trả 409 với AffectedBookingsCount để FE confirm.
    /// </summary>
    [HttpPut("{id:guid}/close")]
    [ProducesResponseType(typeof(ApiResponse<CloseSlotImpactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(
        Guid id,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            var impact = await _slots.CloseSlotAsync(id, force, ct);
            if (impact.AffectedBookingsCount > 0 && !force)
            {
                return Conflict(ApiResponse<CloseSlotImpactResponse>.Fail(
                    409,
                    $"Slot '{id}' has {impact.AffectedBookingsCount} active booking(s). " +
                    "Pass ?force=true to confirm closing anyway (existing bookings remain Booked)."));
            }
            return Ok(ApiResponse<CloseSlotImpactResponse>.Ok(impact));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(ApiResponse<object>.Fail(404, ex.Message));
            }
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }
}