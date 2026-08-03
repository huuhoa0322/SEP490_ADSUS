using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// Module 7 — Prescription & Adherence (UC-11 + UC-18).
/// - GET endpoints: Doctor + Nurse (UC-11 BR-02).
/// - POST /prescriptions: Doctor ONLY (UC-18, không mở rộng cho Nurse).
/// - GET /medicines: Doctor ONLY (chỉ phục vụ kê đơn).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(Roles = "DOCTOR,NURSE")]
[Produces("application/json")]
public sealed class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptions;
    private readonly IMedicineService _medicines;

    public PrescriptionsController(
        IPrescriptionService prescriptions,
        IMedicineService medicines)
    {
        _prescriptions = prescriptions;
        _medicines = medicines;
    }

    /// <summary>
    /// UC-11: danh sách đơn thuốc của 1 bệnh nhân. Có filter status, date range, phân trang.
    /// </summary>
    [HttpGet("patient-profiles/{patientProfileId:guid}/prescriptions")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByPatient(
        Guid patientProfileId,
        [FromQuery] string? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new PrescriptionListQuery(
            patientProfileId,
            status,
            from,
            to,
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100));

        var data = await _prescriptions.ListByPatientAsync(query, ct);
        return Ok(ApiResponse<PrescriptionListResponse>.Ok(data));
    }

    /// <summary>UC-11: chi tiết 1 đơn + adherence.</summary>
    [HttpGet("prescriptions/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        try
        {
            var data = await _prescriptions.GetDetailAsync(id, ct);
            return Ok(ApiResponse<PrescriptionDetailResponse>.Ok(data));
        }
        catch (PrescriptionNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
    }

    /// <summary>UC-11: timeline liều thuốc của 1 đơn.</summary>
    [HttpGet("prescriptions/{id:guid}/intake-logs")]
    [ProducesResponseType(typeof(ApiResponse<IntakeLogListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIntakeLogs(Guid id, CancellationToken ct)
    {
        try
        {
            var data = await _prescriptions.GetIntakeLogsAsync(id, ct);
            return Ok(ApiResponse<IntakeLogListResponse>.Ok(data));
        }
        catch (PrescriptionNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
    }

    /// <summary>UC-18: bác sĩ kê đơn cho 1 Case đã Confirmed. DOCTOR only.</summary>
    [HttpPost("prescriptions")]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePrescriptionRequest request,
        CancellationToken ct)
    {
        try
        {
            var data = await _prescriptions.CreateAsync(request, User, ct);
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<PrescriptionDetailResponse>.Ok(data, "Đã kê đơn thuốc."));
        }
        catch (ValidationException ex)
        {
            var msg = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object?>.Fail(400, msg));
        }
        catch (CaseNotFoundException ex)
        {
            return NotFound(ApiResponse<object?>.Fail(404, ex.Message));
        }
        catch (CaseNotConfirmedException ex)
        {
            return Conflict(ApiResponse<object?>.Fail(409, ex.Message));
        }
        catch (ActivePrescriptionExistsException ex)
        {
            return Conflict(ApiResponse<object?>.Fail(409, ex.Message));
        }
        catch (DoctorNotFoundException ex)
        {
            return Unauthorized(ApiResponse<object?>.Fail(401, ex.Message));
        }
    }

    /// <summary>UC-18 BR-01: autocomplete thuốc cho bác sĩ khi kê đơn. DOCTOR only.</summary>
    [HttpGet("medicines")]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicineListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMedicines(
        [FromQuery] string? keyword,
        CancellationToken ct)
    {
        var data = await _medicines.SearchAsync(keyword ?? string.Empty, ct);
        return Ok(ApiResponse<IReadOnlyList<MedicineListItem>>.Ok(data));
    }
}
