using System.Security.Claims;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/prescriptions")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptionService;

    public PrescriptionsController(IPrescriptionService prescriptionService)
    {
        _prescriptionService = prescriptionService;
    }

    /// <summary>UC-18 — Bác sĩ kê đơn thuốc.</summary>
    [HttpPost]
    [Authorize(Roles = "DOCTOR")]
    public async Task<ActionResult<PrescriptionResponse>> Create(
        [FromBody] CreatePrescriptionRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _prescriptionService.CreateAsync(userId, request, ct);
        return Created($"/api/v1/prescriptions/{result.PrescriptionId}", result);
    }

    /// <summary>UC-17 — Lấy chi tiết đơn thuốc.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "DOCTOR,PATIENT")]
    public async Task<ActionResult<PrescriptionResponse>> GetById(
        Guid id,
        CancellationToken ct)
    {
        // Doctor can view any; Patient can only view their own (enforced in service).
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        // TODO(capstone-extension): implement GetByIdAsync in IPrescriptionService
        return Ok((object?)null);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string GetByIdRoute => nameof(GetById);
}