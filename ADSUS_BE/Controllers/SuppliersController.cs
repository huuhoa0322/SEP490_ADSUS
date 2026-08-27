using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

namespace ADSUS_BE.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
[Authorize(Roles = "ADMIN")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _supplierService.GetSuppliersAsync(page, pageSize, search, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierById(Guid id, CancellationToken ct = default)
    {
        var result = await _supplierService.GetSupplierByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request, CancellationToken ct = default)
    {
        var result = await _supplierService.CreateSupplierAsync(request, ct);
        return CreatedAtAction(nameof(GetSupplierById), new { id = result.SupplierId }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var result = await _supplierService.UpdateSupplierAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateSupplierStatus(Guid id, [FromQuery] bool isActive, CancellationToken ct = default)
    {
        await _supplierService.UpdateSupplierStatusAsync(id, isActive, ct);
        return NoContent();
    }
}
