using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MedicinesController : ControllerBase
{
    private readonly IMedicineService _medicineService;

    public MedicinesController(IMedicineService medicineService)
    {
        _medicineService = medicineService;
    }

    /// <summary>
    /// T�m ki?m danh m?c thu?c d? g?i � (Autocomplete).
    /// D�ng cho B�c si khi k� don.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(IEnumerable<MedicineResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMedicines([FromQuery] string? search = "", [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var result = await _medicineService.SearchMedicinesAsync(search ?? "", limit, ct);
        return Ok(result);
    }

    /// <summary>
    /// L?y danh s�ch thu?c ph�n trang (Admin).
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(PagedResult<MedicineResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPagedMedicines([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = "", CancellationToken ct = default)
    {
        var result = await _medicineService.GetPagedAsync(page, pageSize, search, ct);
        return Ok(result);
    }

    /// <summary>
    /// Th�m thu?c m?i (Admin).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(MedicineResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMedicine([FromBody] CreateMedicineRequest request, CancellationToken ct = default)
    {
        var result = await _medicineService.CreateMedicineAsync(request, ct);
        return CreatedAtAction(nameof(GetPagedMedicines), new { id = result.MedicineId }, result);
    }

    /// <summary>
    /// C?p nh?t t�n thu?c (Admin).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(MedicineResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMedicine(Guid id, [FromBody] UpdateMedicineRequest request, CancellationToken ct = default)
    {
        var result = await _medicineService.UpdateMedicineAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// X�a m?m thu?c (Admin).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMedicine(Guid id, CancellationToken ct = default)
    {
        await _medicineService.DeleteMedicineAsync(id, ct);
        return NoContent();
    }
    /// <summary>
    /// K�ch ho?t l?i thu?c (Admin).
    /// </summary>
    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActivateMedicine(Guid id, CancellationToken ct = default)
    {
        await _medicineService.ActivateMedicineAsync(id, ct);
        return NoContent();
    }
    // --- Packaging Endpoints ---

    [HttpGet("units")]
    [Authorize(Roles = "ADMIN,DOCTOR")]
    [ProducesResponseType(typeof(IEnumerable<MedicineUnitResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedicineUnits(CancellationToken ct = default)
    {
        var result = await _medicineService.GetMedicineUnitsAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id}/packagings")]
    [Authorize(Roles = "ADMIN,DOCTOR")]
    [ProducesResponseType(typeof(IEnumerable<MedicinePackagingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPackagingsByMedicineId(Guid id, CancellationToken ct = default)
    {
        var result = await _medicineService.GetPackagingsByMedicineIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("{id}/packagings")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(MedicinePackagingResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddPackaging(Guid id, [FromBody] CreateMedicinePackagingRequest request, CancellationToken ct = default)
    {
        var result = await _medicineService.AddPackagingAsync(id, request, ct);
        return CreatedAtAction(nameof(GetPackagingsByMedicineId), new { id = result.MedicineId }, result);
    }

    [HttpPut("packagings/{packagingId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(MedicinePackagingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePackaging(Guid packagingId, [FromBody] UpdateMedicinePackagingRequest request, CancellationToken ct = default)
    {
        var result = await _medicineService.UpdatePackagingAsync(packagingId, request, ct);
        return Ok(result);
    }

    [HttpDelete("packagings/{packagingId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePackaging(Guid packagingId, CancellationToken ct = default)
    {
        await _medicineService.DeletePackagingAsync(packagingId, ct);
        return NoContent();
    }
}
