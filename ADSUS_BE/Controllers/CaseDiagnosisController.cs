using System;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

public class AnalyzeImageRequest
{
    public Guid ModelVersionId { get; set; }
    public IFormFile Image { get; set; } = null!;
}

public class ConfirmAnalysisApiRequest
{
    public IFormFile OriginalImage { get; set; } = null!;
    public IFormFile BurntImage { get; set; } = null!;
    public string AiPredictionsJson { get; set; } = "[]";
    public string DoctorAnnotationsJson { get; set; } = "[]";
    public Guid ModelVersionId { get; set; }
    public string? Note { get; set; }
}

[ApiController]
[Route("api/v1/cases")]
[Authorize(Roles = "DOCTOR")]
[Produces("application/json")]
public sealed class CaseDiagnosisController : ControllerBase
{
    private readonly ICaseDiagnosisService _diagnosisService;

    public CaseDiagnosisController(ICaseDiagnosisService diagnosisService)
    {
        _diagnosisService = diagnosisService;
    }

    [HttpPost("{caseId:guid}/analyze")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AnalyzeImage(
        Guid caseId,
        [FromForm] AnalyzeImageRequest request,
        CancellationToken ct)
    {
        if (request.Image == null) return BadRequest("Image is required");
        using var stream = request.Image.OpenReadStream();
        var result = await _diagnosisService.AnalyzeImageAsync(caseId, request.ModelVersionId, stream, request.Image.FileName, request.Image.ContentType, ct);
        return Ok(ApiResponse<object>.Ok(result, "Analysis complete"));
    }

    [HttpPost("{caseId:guid}/images/confirm")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ConfirmAnalysis(
        Guid caseId,
        [FromForm] ConfirmAnalysisApiRequest request,
        CancellationToken ct)
    {
        if (request.OriginalImage == null || request.BurntImage == null)
            return BadRequest("Both original and burnt images are required");

        using var origStream = request.OriginalImage.OpenReadStream();
        using var burntStream = request.BurntImage.OpenReadStream();

        var bllRequest = new ConfirmAnalysisRequest
        {
            OriginalImageStream = origStream,
            OriginalImageContentType = request.OriginalImage.ContentType,
            OriginalImageFileName = request.OriginalImage.FileName,
            BurntImageStream = burntStream,
            BurntImageContentType = request.BurntImage.ContentType,
            BurntImageFileName = request.BurntImage.FileName,
            AiPredictionsJson = request.AiPredictionsJson,
            DoctorAnnotationsJson = request.DoctorAnnotationsJson,
            ModelVersionId = request.ModelVersionId,
            Note = request.Note
        };

        await _diagnosisService.ConfirmAnalysisAsync(caseId, bllRequest, ct);
        return Ok(ApiResponse<object>.Ok(null, "Image and annotations saved successfully"));
    }
}
