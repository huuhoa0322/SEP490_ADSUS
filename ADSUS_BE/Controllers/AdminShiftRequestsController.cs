using System.Security.Claims;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/admin/shift-requests")]
[Authorize(Roles = "ADMIN")]
[Produces("application/json")]
public sealed class AdminShiftRequestsController : ControllerBase
{
    private readonly IShiftRequestService _service;

    public AdminShiftRequestsController(IShiftRequestService service)
    {
        _service = service;
    }

    private Guid CurrentAdminId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing NameIdentifier claim."));

    /// <summary>Admin xem danh sách yêu cầu nghỉ/tăng ca của các bác sĩ.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShiftRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAllRequests(
        [FromQuery] ShiftRequestStatus? status = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ListAllRequestsAsync(status, doctorId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<ShiftRequestResponse>>.Ok(result));
    }

    /// <summary>Admin duyệt hoặc từ chối yêu cầu.</summary>
    [HttpPut("{id:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<ShiftRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewRequest(
        Guid id,
        [FromBody] ReviewShiftRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _service.ReviewRequestAsync(id, CurrentAdminId, request, ct);
            return Ok(ApiResponse<ShiftRequestResponse>.Ok(result));
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
