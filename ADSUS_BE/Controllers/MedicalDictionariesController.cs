using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/medical-dictionaries")]
[Authorize]
[Produces("application/json")]
public sealed class MedicalDictionariesController : ControllerBase
{
    private readonly IMedicalDictionaryService _medicalDictionaryService;

    public MedicalDictionariesController(IMedicalDictionaryService medicalDictionaryService)
    {
        _medicalDictionaryService = medicalDictionaryService;
    }

    [HttpGet("diseases")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalDiseaseResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDiseases(CancellationToken ct)
    {
        var result = await _medicalDictionaryService.GetDiseasesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<MedicalDiseaseResponse>>.Ok(result));
    }

    [HttpGet("allergy-types")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MedicalAllergyTypeResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllergyTypes(CancellationToken ct)
    {
        var result = await _medicalDictionaryService.GetAllergyTypesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<MedicalAllergyTypeResponse>>.Ok(result));
    }
}
