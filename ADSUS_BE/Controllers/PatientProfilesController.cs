using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-06 — hồ sơ y tế nền của bệnh nhân (Web SCR-10).
/// Cả Bác sĩ và Điều dưỡng đều được xem và sửa (UC-06 Allowed Roles).
/// </summary>
[ApiController]
[Route("api/v1/patient-profiles")]
[Authorize(Roles = "DOCTOR,NURSE")]
[Produces("application/json")]
public sealed class PatientProfilesController : ControllerBase
{
    private readonly IPatientProfileService _profiles;
    private readonly IValidator<CreatePatientProfileRequest> _createValidator;
    private readonly IValidator<UpdatePatientProfileRequest> _updateValidator;

    public PatientProfilesController(
        IPatientProfileService profiles,
        IValidator<CreatePatientProfileRequest> createValidator,
        IValidator<UpdatePatientProfileRequest> updateValidator)
    {
        _profiles = profiles;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Tạo hồ sơ y tế nền, gắn 1–1 với một tài khoản bệnh nhân đã có (UC-06).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePatientProfileRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _profiles.CreateAsync(request, GetActingUserId(), ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.PatientProfileId },
            ApiResponse<PatientProfileResponse>.Ok(result, "Patient profile created successfully"));
    }

    /// <summary>Thay toàn bộ hồ sơ nền — gửi lại cả giá trị không đổi (UC-06).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePatientProfileRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _profiles.UpdateAsync(id, request, ct);
        return Ok(ApiResponse<PatientProfileResponse>.Ok(result, "Patient profile updated successfully"));
    }

    /// <summary>Đọc một hồ sơ nền (UC-06).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PatientProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _profiles.GetByIdAsync(id, ct);
        return Ok(ApiResponse<PatientProfileResponse>.Ok(result));
    }

    /// <summary>
    /// Id người đang thao tác, lấy từ token — KHÔNG bao giờ nhận từ request, nếu không thì
    /// ai cũng ghi tên người khác vào cột "bác sĩ lập hồ sơ" được.
    /// </summary>
    private Guid GetActingUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid access token.");
}
