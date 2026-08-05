using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using FluentValidation;
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
    private readonly IPatientAccountService _accounts;
    private readonly IValidator<CreatePatientAccountRequest> _createAccountValidator;
    private readonly IValidator<UpdatePatientAccountRequest> _updateAccountValidator;

    public PatientsController(
        IPatientProfileService profiles,
        IPatientAccountService accounts,
        IValidator<CreatePatientAccountRequest> createAccountValidator,
        IValidator<UpdatePatientAccountRequest> updateAccountValidator)
    {
        _profiles = profiles;
        _accounts = accounts;
        _createAccountValidator = createAccountValidator;
        _updateAccountValidator = updateAccountValidator;
    }

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

    /// <summary>
    /// UC-06 AF-01 (quyết định ghi đè 04/08/2026) — Điều dưỡng tạo tài khoản Bệnh nhân mới
    /// ngay tại luồng tiếp nhận, thay vì phải nhờ Admin (UC-04).
    ///
    /// CHỈ NURSE. Class đã có [Authorize(Roles="DOCTOR,NURSE")]; thuộc tính ở method là phép
    /// AND với nó, nên hiệu lực rút còn đúng NURSE. Bác sĩ nhận 403 — đúng BR-03.
    ///
    /// Endpoint nằm ngoài API Catalog v1.1, đã ghi vào Flags Summary.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "NURSE")]
    [ProducesResponseType(typeof(ApiResponse<PatientAccountResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreatePatientAccountRequest request,
        CancellationToken ct)
    {
        var validation = await _createAccountValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _accounts.CreateAsync(request, GetActingUserId(), ct);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<PatientAccountResponse>.Ok(result, "Patient account created successfully"));
    }

    /// <summary>
    /// UC-06 AF-02 — Điều dưỡng sửa lỗi nhập liệu trên tài khoản Bệnh nhân.
    /// CHỈ NURSE (BR-03), CHỈ 4 trường liên hệ (BR-04) — role và status vẫn là việc của Admin.
    /// </summary>
    [HttpPut("{userId:guid}")]
    [Authorize(Roles = "NURSE")]
    [ProducesResponseType(typeof(ApiResponse<PatientAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAccountContact(
        Guid userId,
        [FromBody] UpdatePatientAccountRequest request,
        CancellationToken ct)
    {
        var validation = await _updateAccountValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _accounts.UpdateContactAsync(userId, request, GetActingUserId(), ct);
        return Ok(ApiResponse<PatientAccountResponse>.Ok(result, "Patient account updated successfully"));
    }

    /// <summary>
    /// UC-06 AF-03 — Điều dưỡng cấp lại mật khẩu cho Bệnh nhân.
    ///
    /// Mật khẩu tạm CHỈ đi qua email; API không trả nó về và giao diện không bao giờ hiển thị
    /// (BR-05, PRD §6.2). CHỈ NURSE (BR-03).
    /// </summary>
    [HttpPut("{userId:guid}/reset-password")]
    [Authorize(Roles = "NURSE")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetAccountPassword(Guid userId, CancellationToken ct)
    {
        await _accounts.ResetPasswordAsync(userId, GetActingUserId(), ct);
        return Ok(ApiResponse<object>.Ok(null!, "Temporary password sent to the patient's email"));
    }

    /// <summary>
    /// Id người đang thao tác, lấy từ token — KHÔNG bao giờ nhận từ request, nếu không thì
    /// ai cũng ghi tên người khác vào Audit Log được.
    /// </summary>
    private Guid GetActingUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid access token.");
}
