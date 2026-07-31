using System.Security.Claims;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

/// <summary>
/// Hồ sơ cá nhân của chính người đang đăng nhập.
///
/// Mọi endpoint ở đây chỉ tác động lên tài khoản của người gọi, lấy từ claim trong JWT —
/// không nhận userId từ request. Nhờ vậy không ai gọi được để sửa hồ sơ người khác.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IProfileService _profile;
    private readonly IValidator<UpdateProfileRequest> _updateProfileValidator;

    public UsersController(
        IProfileService profile,
        IValidator<UpdateProfileRequest> updateProfileValidator)
    {
        _profile = profile;
        _updateProfileValidator = updateProfileValidator;
    }

    /// <summary>
    /// UC-10 bước 2 — lấy hồ sơ cá nhân để hiển thị lên SCR-03.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOwnProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<UserProfileResponse>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var profile = await _profile.GetOwnProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Unauthorized(ApiResponse<UserProfileResponse>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        return Ok(ApiResponse<UserProfileResponse>.Ok(profile, "Profile loaded."));
    }

    /// <summary>
    /// UC-10 — cập nhật hồ sơ cá nhân. Chỉ họ tên, email, ngày sinh.
    /// Số điện thoại không nằm trong request nên không thể bị đổi (BR-02).
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateOwnProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateProfileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var result = await _profile.UpdateOwnProfileAsync(userId, request, cancellationToken);
        return MapResult(result, "Profile updated successfully.");
    }

    /// <summary>
    /// UC-02 — bật hoặc tắt đăng nhập sinh trắc học.
    ///
    /// BR-01 (phải đăng nhập bằng mật khẩu thành công ít nhất một lần trước) được thoả mãn
    /// nhờ chính [Authorize]: không có token thì không gọi được endpoint này, mà muốn có
    /// token thì phải đăng nhập bằng mật khẩu.
    /// </summary>
    [HttpPut("me/biometric")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetBiometric(
        [FromBody] UpdateBiometricRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var result = await _profile.SetBiometricEnabledAsync(userId, request.Enabled, cancellationToken);
        return MapResult(
            result,
            request.Enabled ? "Biometric sign-in enabled." : "Biometric sign-in disabled.");
    }

    // ---- helpers ----

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private IActionResult MapResult(ProfileOperationResult result, string successMessage) =>
        result switch
        {
            ProfileOperationResult.Success =>
                Ok(ApiResponse<object>.Ok(null!, successMessage)),

            ProfileOperationResult.EmailAlreadyUsed =>
                BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest, "This email is already used by another account.")),

            ProfileOperationResult.AccountNotActive =>
                StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                    StatusCodes.Status403Forbidden, "This account is no longer active.")),

            _ => Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token.")),
        };
}
