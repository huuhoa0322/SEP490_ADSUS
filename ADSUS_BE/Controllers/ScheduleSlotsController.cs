using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Exceptions;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/schedule-slots")]
[Authorize(Roles = "DOCTOR,NURSE")]
[Produces("application/json")]
public sealed class ScheduleSlotsController : ControllerBase
{
    private readonly IScheduleSlotService _service;

    public ScheduleSlotsController(IScheduleSlotService service) => _service = service;

    /// <summary>#46 — tạo slot (UC-15 BR-01).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateScheduleSlotRequest req, CancellationToken ct)
    {
        try
        {
            var data = await _service.CreateAsync(req, ct);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<ScheduleSlotResponse>.Ok(data, "Schedule slot created successfully"));
        }
        catch (ValidationException ex)
        {
            var msg = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object?>.Fail(400, msg));
        }
        catch (DoctorNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
        catch (SlotOverlapException ex)
        {
            return UnprocessableEntity(ApiResponse<object?>.Fail(422, ex.Message));
        }
    }

    /// <summary>#47 — list slots có filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ScheduleSlotResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? doctorId,
        [FromQuery] DateOnly? slotDate,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var data = await _service.SearchAsync(doctorId, slotDate, status,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
        return Ok(ApiResponse<PagedResult<ScheduleSlotResponse>>.Ok(data));
    }

    /// <summary>Bonus — detail 1 slot.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        try
        {
            var data = await _service.GetByIdAsync(id, ct);
            return Ok(ApiResponse<ScheduleSlotResponse>.Ok(data));
        }
        catch (ScheduleSlotNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
    }

    /// <summary>#48 — close slot (UC-15 BR-02).</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ScheduleSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateScheduleSlotStatusRequest req, CancellationToken ct)
    {
        try
        {
            var data = await _service.UpdateStatusAsync(id, req, ct);
            return Ok(ApiResponse<ScheduleSlotResponse>.Ok(data, "Schedule slot closed"));
        }
        catch (ValidationException ex)
        {
            var msg = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object?>.Fail(400, msg));
        }
        catch (ScheduleSlotNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
        catch (SlotAlreadyClosedException ex)
        {
            return UnprocessableEntity(ApiResponse<object?>.Fail(422, ex.Message));
        }
    }

    /// <summary>#49 — appointments trong 1 slot (UC-15 AF-02).</summary>
    [HttpGet("{id:guid}/appointments")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAppointments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var data = await _service.ListAppointmentsBySlotAsync(id,
                Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            return Ok(ApiResponse<PagedResult<AppointmentSummaryResponse>>.Ok(data));
        }
        catch (ScheduleSlotNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
    }
}