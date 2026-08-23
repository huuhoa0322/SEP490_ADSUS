using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/symptoms")]
[Authorize]
public class SymptomsController : ControllerBase
{
    private readonly ISymptomService _symptomService;

    public SymptomsController(ISymptomService symptomService)
    {
        _symptomService = symptomService;
    }

    /// <summary>
    /// Lấy danh mục các nhóm triệu chứng và triệu chứng chi tiết.
    /// Cho phép mọi role đã đăng nhập (Bác sĩ, Điều dưỡng) để render UI.
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SymptomCategoryResponse>>>> GetCategories(CancellationToken ct)
    {
        var result = await _symptomService.GetCategoriesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<SymptomCategoryResponse>>.Ok(result));
    }
}
