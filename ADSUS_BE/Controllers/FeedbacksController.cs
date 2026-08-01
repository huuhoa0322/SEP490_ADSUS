using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public FeedbacksController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>
    /// POST /api/v1/me/feedbacks — Patient gửi feedback.
    /// Validation: Rating 1-5, Content 1-2000 ký tự.
    /// </summary>
    [HttpPost("api/v1/me/feedbacks")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(ApiResponse<FeedbackResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFeedbackRequest request,
        [FromServices] IServiceProvider sp,
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

        // TODO: Get patientProfileId from JWT via IUserContext or PatientProfile lookup
        // For now, we'll need to look up the PatientProfile from the authenticated user's ID
        var patientProfileId = await GetPatientProfileIdAsync(sp, ct);
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
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FeedbackResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _feedbackService.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<FeedbackResponse>>.Ok(result));
    }

    private async Task<Guid?> GetPatientProfileIdAsync(IServiceProvider sp, CancellationToken ct)
    {
        // Get current user ID from JWT
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        // Look up PatientProfile by UserId
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ADSUS_BE.DAL.Data.AppDbContext>();
        var patientProfile = await db.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return patientProfile?.PatientProfileId;
    }
}
