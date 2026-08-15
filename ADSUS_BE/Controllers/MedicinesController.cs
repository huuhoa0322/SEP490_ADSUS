using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    /// Tìm kiếm danh mục thuốc để gợi ý (Autocomplete).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "DOCTOR")]
    [ProducesResponseType(typeof(IEnumerable<MedicineResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMedicines([FromQuery] string? search = "", [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var result = await _medicineService.SearchMedicinesAsync(search ?? "", limit, ct);
        return Ok(result);
    }
}
