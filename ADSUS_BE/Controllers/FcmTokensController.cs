using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/fcm-token")]
[Authorize]
public class FcmTokensController : ControllerBase
{
    private readonly IFcmTokenService _fcmTokenService;

    public FcmTokensController(IFcmTokenService fcmTokenService)
    {
        _fcmTokenService = fcmTokenService;
    }

    [HttpPut]
    public async Task<IActionResult> RegisterToken(
        [FromBody] RegisterFcmTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        if (string.IsNullOrWhiteSpace(request.FcmToken))
        {
            return BadRequest(ApiResponse<object>.Fail(
                StatusCodes.Status400BadRequest, "FCM token is required."));
        }

        await _fcmTokenService.RegisterTokenAsync(
            userId,
            request.FcmToken,
            request.DeviceType ?? "android",
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(null!, "FCM token registered successfully."));
    }

    [HttpDelete]
    public async Task<IActionResult> UnregisterToken(
        [FromBody] UnregisterFcmTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        if (!string.IsNullOrWhiteSpace(request.FcmToken))
        {
            await _fcmTokenService.UnregisterTokenAsync(userId, request.FcmToken, cancellationToken);
        }
        else
        {
            await _fcmTokenService.UnregisterAllTokensAsync(userId, cancellationToken);
        }

        return Ok(ApiResponse<object>.Ok(null!, "FCM token unregistered successfully."));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public record RegisterFcmTokenRequest(string FcmToken, string? DeviceType = "android");
public record UnregisterFcmTokenRequest(string? FcmToken = null);
