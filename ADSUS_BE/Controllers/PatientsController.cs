using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-09 — danh sách bệnh nhân của Bác sĩ/Điều dưỡng (Web SCR-09). Chỉ đọc (BR-02).
///
/// Tách khỏi /api/v1/admin/users có chủ đích: ở đây là góc nhìn lâm sàng, không có email
/// hay trạng thái tài khoản — những thứ đó thuộc màn hình quản trị của Admin.
/// </summary>
[ApiController]
[Route("api/v1/patients")]
[Authorize(Roles = "DOCTOR,NURSE")]
[Produces("application/json")]
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientProfileService _profiles;

    public PatientsController(IPatientProfileService profiles) => _profiles = profiles;

    /// <summary>
    /// Tìm theo họ tên hoặc số điện thoại, lọc theo trạng thái lần khám gần nhất (UC-09).
    /// </summary>
    /// <param name="search">Khớp chuỗi con, không phân biệt hoa thường, trên họ tên hoặc số điện thoại.</param>
    /// <param name="visitStatus">All (mặc định) | Pending | Confirmed.</param>
    /// <param name="hasProfile">
    /// Bỏ trống = tất cả bệnh nhân. true = chỉ người đã có hồ sơ nền. false = chỉ người
    /// CHƯA có hồ sơ nền — giao diện dùng giá trị này để chọn tài khoản cho luồng tạo hồ sơ
    /// nền (#17).
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] string? visitStatus,
        [FromQuery] bool? hasProfile,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (visitStatus is not null
            && !string.Equals(visitStatus, "All", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visitStatus, "Pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visitStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "visitStatus must be All, Pending or Confirmed."));
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var filter = string.Equals(visitStatus, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : visitStatus;

        var result = await _profiles.SearchPatientsAsync(search, filter, hasProfile, page, pageSize, ct);

        return Ok(ApiResponse<PagedResult<PatientSummaryResponse>>.Ok(result, "Patients retrieved successfully"));
    }
}
