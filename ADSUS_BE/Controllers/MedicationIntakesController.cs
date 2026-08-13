using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/me/medication-intakes")]
[Authorize(Roles = "PATIENT")]
public class MedicationIntakesController : ControllerBase
{
    private readonly IMedicationIntakeService _intakeService;

    public MedicationIntakesController(IMedicationIntakeService intakeService)
    {
        _intakeService = intakeService;
    }

    /// <summary>UC-11 — Danh sách liều thuốc sắp tới của bệnh nhân.</summary>
    [HttpGet]
    public async Task<IActionResult> ListUpcoming(
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _intakeService.ListUpcomingAsync(userId, ct);
        return Ok(ApiResponse<IReadOnlyList<IntakeLogResponse>>.Ok(result));
    }

    /// <summary>UC-11 — Danh sách liều thuốc của 1 đơn cụ thể.</summary>
    [HttpGet("prescription/{prescriptionId:guid}")]
    public async Task<IActionResult> ListByPrescription(
        Guid prescriptionId,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _intakeService.ListByPrescriptionAsync(userId, prescriptionId, ct);
        return Ok(ApiResponse<IReadOnlyList<IntakeLogResponse>>.Ok(result));
    }

    /// <summary>
    /// UC-17 — Xác nhận đã uống.
    /// GB-01: trạng thái một chiều PENDING → TAKEN.
    /// Idempotent: nếu đã TAKEN trả 200 (không lỗi).
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmTaken(
        Guid id,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await _intakeService.ConfirmTakenAsync(userId, id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Xác nhận đã uống thuốc thành công."));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}