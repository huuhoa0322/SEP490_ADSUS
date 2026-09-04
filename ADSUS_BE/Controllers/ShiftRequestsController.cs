using System.Security.Claims;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/shift-requests")]
[Authorize(Roles = "DOCTOR")]
[Produces("application/json")]
public sealed class ShiftRequestsController : ControllerBase
{
    private readonly IShiftRequestService _service;

    public ShiftRequestsController(IShiftRequestService service)
    {
        _service = service;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing NameIdentifier claim."));

    /// <summary>Doctor gửi yêu cầu nghỉ/tăng ca.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ShiftRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateShiftRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var result = await _service.CreateRequestAsync(CurrentUserId, request, ct);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<ShiftRequestResponse>.Ok(result, code: 201));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("Bạn đã gửi yêu cầu cho ca này rồi"))
            {
                return Conflict(ApiResponse<object>.Fail(409, ex.Message));
            }
            return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
        }
    }

    /// <summary>Doctor xem danh sách yêu cầu của mình.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShiftRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyRequests(
        [FromQuery] ShiftRequestStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ListMyRequestsAsync(CurrentUserId, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<ShiftRequestResponse>>.Ok(result));
    }

    /// <summary>Doctor lấy dữ liệu hiển thị lịch tháng.</summary>
    [HttpGet("month-summary")]
    [ProducesResponseType(typeof(ApiResponse<List<DayShiftSummary>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthSummary(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var result = await _service.GetMonthSummaryAsync(CurrentUserId, year, month, ct);
        return Ok(ApiResponse<List<DayShiftSummary>>.Ok(result));
    }
}
