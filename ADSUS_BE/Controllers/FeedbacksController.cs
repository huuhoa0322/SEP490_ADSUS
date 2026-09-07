using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-22 — Patient gửi feedback, Admin xem feedback.
/// GB-03: KHÔNG có DELETE endpoint.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class FeedbacksController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly IPatientProfileRepository _patientProfiles;

    public FeedbacksController(IFeedbackService feedbackService, IPatientProfileRepository patientProfiles)
    {
        _feedbackService = feedbackService;
        _patientProfiles = patientProfiles;
    }

    /// <summary>
    /// POST /api/v1/me/feedbacks — Patient gửi feedback.
    /// Validation: Rating 1-5, Content 1-2000 ký tự.
    /// </summary>
    [HttpPost("api/v1/me/feedbacks")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken ct)
    {
        // Validate Rating
        if (request.Rating < 1 || request.Rating > 5)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Đánh giá phải từ 1 đến 5 sao."));
        }

        // Validate Content
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Nội dung phản hồi không được trống."));
        }

        if (request.Content.Length > 2000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Nội dung phản hồi không được quá 2000 ký tự."));
        }

        var patientProfileId = await GetPatientProfileIdAsync(ct);
        if (patientProfileId == null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "Không tìm thấy hồ sơ bệnh nhân."));
        }

        var result = await _feedbackService.SubmitAsync(request, patientProfileId.Value, ct);

        return CreatedAtAction(
            nameof(GetAll),
            ApiResponse<FeedbackResponse>.Ok(result, "Phản hồi đã được gửi thành công."));
    }

    /// <summary>
    /// GET /api/v1/admin/feedbacks — Admin xem tất cả feedback.
    /// </summary>
    [HttpGet("api/v1/admin/feedbacks")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FeedbackResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _feedbackService.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<FeedbackResponse>>.Ok(result));
    }

    /// <summary>
    /// POST /api/v1/me/case-feedbacks — Patient gửi feedback cho ca khám (FT-37).
    /// </summary>
    [HttpPost("api/v1/me/case-feedbacks")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<CaseFeedbackResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitCaseFeedback(
        [FromBody] SubmitCaseFeedbackRequest request,
        [FromQuery] Guid caseId,
        CancellationToken ct)
    {
        var patientProfileId = await GetPatientProfileIdAsync(ct);
        if (patientProfileId == null)
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, "Không tìm thấy hồ sơ bệnh nhân."));

        var result = await _feedbackService.SubmitCaseFeedbackAsync(request, patientProfileId.Value, caseId, ct);
        return Created($"/api/v1/me/cases/{caseId}/feedback",
            ApiResponse<CaseFeedbackResponse>.Ok(result, "Phản hồi đã được gửi thành công."));
    }

    /// <summary>
    /// GET /api/v1/me/cases/{caseId}/feedback — Patient xem feedback đã gửi cho ca khám (FT-37).
    /// </summary>
    [HttpGet("api/v1/me/cases/{caseId}/feedback")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<CaseFeedbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCaseFeedback(
        [FromRoute] Guid caseId,
        CancellationToken ct)
    {
        var patientProfileId = await GetPatientProfileIdAsync(ct);
        if (patientProfileId == null)
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, "Không tìm thấy hồ sơ bệnh nhân."));

        var result = await _feedbackService.GetCaseFeedbackAsync(caseId, patientProfileId.Value, ct);
        if (result == null)
            return NotFound();
        return Ok(ApiResponse<CaseFeedbackResponse>.Ok(result));
    }

    private async Task<Guid?> GetPatientProfileIdAsync(CancellationToken ct)
    {
        // Get current user ID from JWT
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        var patientProfile = await _patientProfiles.GetByUserIdAsync(userId, ct);
        return patientProfile?.PatientProfileId;
    }
}
