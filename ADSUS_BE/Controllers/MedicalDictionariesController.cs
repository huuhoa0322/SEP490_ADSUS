using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/medical-dictionaries")]
[Authorize]
[Produces("application/json")]
public sealed class MedicalDictionariesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MedicalDictionariesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("diseases")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalDisease>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDiseases(CancellationToken ct)
    {
        var result = await _db.MedicalDiseases
            .AsNoTracking()
            .OrderBy(x => x.IsOther)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<MedicalDisease>>.Ok(result));
    }

    [HttpGet("allergy-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalAllergyType>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllergyTypes(CancellationToken ct)
    {
        var result = await _db.MedicalAllergyTypes
            .AsNoTracking()
            .OrderBy(x => x.IsOther)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<MedicalAllergyType>>.Ok(result));
    }
}
