using System.Security.Claims;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IPasswordResetService _passwordReset;
    private readonly IPatientSelfRegistrationService _selfRegistration;
    private readonly IPasswordResetOtpService _passwordResetOtp;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<CompleteRegistrationRequest> _completeRegistrationValidator;
    private readonly IValidator<CompletePasswordResetWithFirebaseRequest> _completePasswordResetWithFirebaseValidator;

    public AuthController(
        IAuthService auth,
        IPasswordResetService passwordReset,
        IPatientSelfRegistrationService selfRegistration,
        IPasswordResetOtpService passwordResetOtp,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<CompleteRegistrationRequest> completeRegistrationValidator,
        IValidator<CompletePasswordResetWithFirebaseRequest> completePasswordResetWithFirebaseValidator)
    {
        _auth = auth;
        _passwordReset = passwordReset;
        _selfRegistration = selfRegistration;
        _passwordResetOtp = passwordResetOtp;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _completeRegistrationValidator = completeRegistrationValidator;
        _completePasswordResetWithFirebaseValidator = completePasswordResetWithFirebaseValidator;
    }

    /// <summary>
    /// UC-03 FT-06 — người dùng tự yêu cầu cấp lại mật khẩu từ màn đăng nhập.
    ///
    /// LUÔN trả về 200 kèm ĐÚNG MỘT CÂU, bất kể số điện thoại có tồn tại không, email có
    /// khớp không, hay tài khoản đã bị khoá (AF-01). Trả lời khác đi là biến endpoint này
    /// thành công cụ dò xem số nào đã có tài khoản trong hệ thống.
    ///
    /// Service trả về void nên ở đây không có gì để phân nhánh — luật được ép bằng kiểu dữ
    /// liệu chứ không dựa vào việc người viết nhớ hay quên.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    // Mỗi lời gọi trúng là đổi mật khẩu của người ta và gửi một lá thư. Không chặn thì
    // ai cũng quấy rối được chủ tài khoản, dù không hề đăng nhập được.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            // Chỉ báo lỗi về HÌNH DẠNG dữ liệu (bỏ trống, email sai định dạng). Không nói gì
            // về việc tài khoản có tồn tại hay không.
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        await _passwordReset.RequestSelfServiceResetAsync(request, cancellationToken);

        return Ok(ApiResponse<object>.Ok(
            null!,
            "If the information is correct, a new password has been sent to your email."));
    }

    /// <summary>
    /// UC-01 — sign in with phone number and password.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    // Chặn dò mật khẩu theo địa chỉ IP. Đây KHÔNG phải BR-04 (tự khoá tài khoản sau N lần
    // sai) — luật đó bảo vệ một tài khoản, còn cái này chặn kẻ dò lần lượt qua hàng nghìn
    // số điện thoại khác nhau. Cần cả hai; xem chú thích trong AuthService.LoginAsync.
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status429TooManyRequests)]
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
    /// Refresh tokens - exchanges a valid refresh token for new access + refresh tokens.
    /// Supports SignalR persistent connections without re-login.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.RefreshTokensAsync(request.RefreshToken, cancellationToken);

        if (result is null)
        {
            return Unauthorized(ApiResponse<RefreshTokenResponse>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid or expired refresh token."));
        }

        return Ok(ApiResponse<RefreshTokenResponse>.Ok(result, "Tokens refreshed successfully."));
    }

    /// <summary>
    /// Logout - revokes all refresh tokens for the current user.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            await _auth.RevokeAllRefreshTokensAsync(userId, cancellationToken);
            Console.WriteLine($"[Auth] User {userId} logged out, all refresh tokens revoked");
        }

        return Ok(ApiResponse<object>.Ok(null!, "Logged out successfully."));
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

    /// <summary>Bệnh nhân tự đăng ký — xác thực số điện thoại qua Firebase Phone Auth (SDK
    /// chạy trên Mobile), backend chỉ verify token rồi tạo tài khoản Patient và tự động
    /// đăng nhập.</summary>
    [HttpPost("register/complete")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteRegistration(
        [FromBody] CompleteRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _completeRegistrationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<LoginResponse>.Fail(StatusCodes.Status400BadRequest, message));
        }

        // BusinessException (token sai/hết hạn) và ConflictException (số vừa bị đăng ký) được
        // GlobalExceptionHandler dịch sang 422/409 tương ứng (xem GlobalExceptionHandler.cs) —
        // không try/catch ở đây. 400 ở trên chỉ dành cho lỗi hình dạng dữ liệu (FluentValidation).
        var result = await _selfRegistration.CompleteRegistrationAsync(request, cancellationToken);

        return Ok(ApiResponse<LoginResponse>.Ok(result, "Registration successful."));
    }

    /// <summary>UC-03 — quên mật khẩu qua Firebase Phone Auth (chỉ Patient, SDK chạy trên
    /// Mobile). Backend chỉ verify Firebase ID Token, đổi mật khẩu và tự động đăng nhập.
    /// Báo RÕ 404 nếu số chưa có tài khoản Patient Active (xem Global Constraints).</summary>
    [HttpPost("forgot-password/complete-with-firebase")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompletePasswordResetWithFirebase(
        [FromBody] CompletePasswordResetWithFirebaseRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _completePasswordResetWithFirebaseValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<LoginResponse>.Fail(StatusCodes.Status400BadRequest, message));
        }

        var result = await _passwordResetOtp.CompleteAsync(request, cancellationToken);

        // Báo RÕ 404 — quyết định có chủ đích (xem Global Constraints trong plan gốc), tương tự
        // bản OTP cũ nhưng giờ nằm ở bước duy nhất.
        return result is null
            ? NotFound(ApiResponse<LoginResponse>.Fail(StatusCodes.Status404NotFound, "This phone number is not registered."))
            : Ok(ApiResponse<LoginResponse>.Ok(result, "Password reset successful."));
    }
}
