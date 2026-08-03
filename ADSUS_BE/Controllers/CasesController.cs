using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-07, UC-08, UC-12 — ca khám và ảnh siêu âm.
/// Bác sĩ/Điều dưỡng xem bản đầy đủ trên Web (SCR-12); Bệnh nhân xem bản đã duyệt trên
/// Mobile (SCR-13/14).
/// </summary>
[ApiController]
[Route("api/v1/cases")]
[Authorize]
[Produces("application/json")]
public sealed class CasesController : ControllerBase
{
    private readonly ICaseService _cases;

    public CasesController(ICaseService cases) => _cases = cases;

    /// <summary>Danh sách ảnh siêu âm thô của một ca (UC-07, UC-08).</summary>
    [HttpGet("{caseId:guid}/ultrasound-images")]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UltrasoundImageResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListImages(Guid caseId, CancellationToken ct)
    {
        var result = await _cases.ListImagesAsync(caseId, ct);
        return Ok(ApiResponse<IReadOnlyList<UltrasoundImageResponse>>.Ok(result));
    }

    /// <summary>
    /// Danh sách lần khám của một bệnh nhân, cho Bác sĩ/Điều dưỡng (Web SCR-12) (UC-08).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "DOCTOR,NURSE")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CaseSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListByPatient(
        [FromQuery] Guid patientProfileId,
        [FromQuery] string? status,
        [FromQuery] string sortOrder = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (patientProfileId == Guid.Empty)
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "patientProfileId is required."));
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var result = await _cases.ListByPatientProfileAsync(
            patientProfileId, status, sortOrder, page, pageSize, ct);

        return Ok(ApiResponse<PagedResult<CaseSummaryResponse>>.Ok(result, "Cases retrieved successfully"));
    }

    /// <summary>
    /// Danh sách lần khám của chính bệnh nhân đang đăng nhập (Mobile SCR-13) (UC-08).
    /// Luôn chỉ trả về ca đã CONFIRMED — không có tham số trạng thái.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CaseSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var result = await _cases.ListMineAsync(GetCallerUserId(), page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<CaseSummaryResponse>>.Ok(result, "Cases retrieved successfully"));
    }

    /// <summary>
    /// Chi tiết một lần khám (UC-08).
    ///
    /// Hình dạng dữ liệu trả về KHÁC NHAU theo vai trò: Bác sĩ/Điều dưỡng nhận
    /// <see cref="CaseResponse"/> đầy đủ; Bệnh nhân nhận <see cref="PatientCaseResponse"/>
    /// rút gọn và chỉ với ca của chính họ đã được duyệt (GB-05). Swagger chỉ hiển thị được
    /// một hình dạng nên phần mô tả này là nơi ghi lại điều đó.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "DOCTOR,NURSE,PATIENT")]
    [ProducesResponseType(typeof(ApiResponse<CaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (User.IsInRole("PATIENT"))
        {
            var patientView = await _cases.GetForPatientAsync(id, GetCallerUserId(), ct);
            return Ok(ApiResponse<PatientCaseResponse>.Ok(patientView));
        }

        var staffView = await _cases.GetForStaffAsync(id, ct);
        return Ok(ApiResponse<CaseResponse>.Ok(staffView));
    }

    private Guid GetCallerUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid access token.");
}
