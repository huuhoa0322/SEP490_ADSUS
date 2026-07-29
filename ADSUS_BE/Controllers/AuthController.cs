using System.Security.Claims;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthController(
        IAuthService auth,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _auth = auth;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    /// <summary>
    /// UC-01 — sign in with phone number and password.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<LoginResponse>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _auth.LoginAsync(request, cancellationToken);

        // GB-06: unknown phone number, wrong password, locked account or deactivated account —
        // every one of them returns this exact message. The real cause is never disclosed.
        if (result is null)
        {
            return Unauthorized(ApiResponse<LoginResponse>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid phone number or password."));
        }

        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>
    /// UC-25 — a signed-in user changes their own password.
    /// Every role is allowed, so [Authorize] alone is enough — no role restriction.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            // Signature is valid but the identity claim is missing or malformed.
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var result = await _auth.ChangePasswordAsync(userId, request, cancellationToken);

        return result switch
        {
            ChangePasswordResult.Success =>
                Ok(ApiResponse<object>.Ok(null!, "Password changed successfully.")),

            // AF-01. Naming the cause is fine here — the caller is authenticated and acting
            // on their own account, so nothing is disclosed to anyone else.
            ChangePasswordResult.CurrentPasswordIncorrect =>
                BadRequest(ApiResponse<object>.Fail(
                    StatusCodes.Status400BadRequest, "Current password is incorrect.")),

            ChangePasswordResult.AccountNotActive =>
                StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                    StatusCodes.Status403Forbidden, "This account is no longer active.")),

            _ => Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token.")),
        };
    }
}
