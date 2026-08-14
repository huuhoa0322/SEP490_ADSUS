using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// SCR-19 — reminder settings của bệnh nhân.
/// Chỉ Patient mới truy cập được.
/// </summary>
[ApiController]
[Route("api/v1/me/reminder-preference")]
[Authorize(Roles = "PATIENT")]
public class ReminderPreferencesController : ControllerBase
{
    private readonly IReminderPreferenceService _service;

    public ReminderPreferencesController(IReminderPreferenceService service)
    {
        _service = service;
    }

    /// <summary>Lấy preference hiện tại của bệnh nhân (trả default nếu chưa có).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _service.GetAsync(userId, ct);
        return Ok(ApiResponse<ReminderPreferenceResponse>.Ok(result));
    }

    /// <summary>Tạo hoặc cập nhật preference của bệnh nhân.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert(
        [FromBody] UpdateReminderPreferenceRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _service.UpsertAsync(userId, request, ct);
        return Ok(ApiResponse<ReminderPreferenceResponse>.Ok(result, "Đã lưu cài đặt nhắc nhở."));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
