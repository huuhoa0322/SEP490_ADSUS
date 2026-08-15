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
    /// Tìm ki?m danh m?c thu?c d? g?i ý (Autocomplete).
    /// Dùng cho Bác si khi kê don.
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
    /// L?y danh sách thu?c phân trang (Admin).
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
    /// Thêm thu?c m?i (Admin).
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
    /// C?p nh?t tên thu?c (Admin).
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
    /// Xóa m?m thu?c (Admin).
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
    /// Kích ho?t l?i thu?c (Admin).
    /// </summary>
    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActivateMedicine(Guid id, CancellationToken ct = default)
    {
        await _medicineService.ActivateMedicineAsync(id, ct);
        return NoContent();
    }
}
