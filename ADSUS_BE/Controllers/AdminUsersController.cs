using System.Security.Claims;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PagedResult = ADSUS_BE.BLL.UserRoleManagement.DTOs.PagedResult<ADSUS_BE.BLL.UserRoleManagement.DTOs.UserAccountResponse>;

namespace ADSUS_BE.Controllers;

/// <summary>
/// UC-04 — Admin quản lý tài khoản đăng nhập (SCR-06 danh sách, SCR-07 tạo/sửa).
///
/// CHỈ ADMIN. Bảng quyền PRD §3.2 ghi "Create", "Lock / Deactivate" và "Assign role &amp;
/// permission" đều là Full cho Admin và No cho Doctor/Nurse/Patient.
///
/// Đây cũng là nơi đầu tiên trong dự án chặn theo vai trò. Chặn ở đây mới là chặn thật —
/// giao diện ẩn nút đi chỉ là cho gọn mắt, ai cũng gọi thẳng API được.
///
/// Đường dẫn tách riêng khỏi /api/v1/users (hồ sơ của chính mình) để không lẫn: ở đó người
/// dùng thao tác lên tài khoản của họ, ở đây Admin thao tác lên tài khoản người khác.
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "ADMIN")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserAccountService _accounts;
    private readonly IPasswordResetService _passwordReset;
    private readonly IValidator<CreateUserAccountRequest> _createValidator;
    private readonly IValidator<UpdateUserAccountRequest> _updateValidator;

    public AdminUsersController(
        IUserAccountService accounts,
        IPasswordResetService passwordReset,
        IValidator<CreateUserAccountRequest> createValidator,
        IValidator<UpdateUserAccountRequest> updateValidator)
    {
        _accounts = accounts;
        _passwordReset = passwordReset;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// SCR-06 — danh sách tài khoản, tìm theo tên hoặc số điện thoại, lọc theo vai trò và trạng thái.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        TryGetActingAdminId(out var adminId);

        var result = await _accounts.SearchAsync(
            keyword, role, status, page, pageSize, adminId, cancellationToken);

        return Ok(ApiResponse<PagedResult>.Ok(result, "User list loaded."));
    }

    /// <summary>SCR-07 — lấy một tài khoản để đổ vào form sửa.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid userId, CancellationToken cancellationToken)
    {
        TryGetActingAdminId(out var adminId);

        var account = await _accounts.GetByIdAsync(userId, adminId, cancellationToken);

        return account is null
            ? NotFound(ApiResponse<UserAccountResponse>.Fail(
                StatusCodes.Status404NotFound, "Account not found."))
            : Ok(ApiResponse<UserAccountResponse>.Ok(account, "Account loaded."));
    }

    /// <summary>
    /// FT-07 — tạo tài khoản mới. Mật khẩu tạm do hệ thống sinh, trả về plaintext MỘT LẦN
    /// trong phản hồi để Admin đọc trực tiếp cho chủ tài khoản — không còn gửi qua email (sửa
    /// 12/08/2026, thống nhất với UC-03 AF-02/UC-06 AF-01/AF-03).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatedUserAccountResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CreatedUserAccountResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserAccountRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<CreatedUserAccountResponse>.Fail(
                StatusCodes.Status400BadRequest, message));
        }

        if (!TryGetActingAdminId(out var adminId))
        {
            return Unauthorized(ApiResponse<CreatedUserAccountResponse>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var (result, account, temporaryPassword) =
            await _accounts.CreateAsync(request, adminId, cancellationToken);

        if (result != AccountOperationResult.Success)
        {
            return MapFailure<CreatedUserAccountResponse>(result);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { userId = account!.UserId },
            ApiResponse<CreatedUserAccountResponse>.Ok(
                new CreatedUserAccountResponse { Account = account, TemporaryPassword = temporaryPassword! },
                "Account created. Temporary password generated — communicate it to the account holder directly."));
    }

    /// <summary>FT-09 — sửa thông tin và phân lại vai trò.</summary>
    [HttpPut("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid userId,
        [FromBody] UpdateUserAccountRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            return BadRequest(ApiResponse<object>.Fail(StatusCodes.Status400BadRequest, message));
        }

        if (!TryGetActingAdminId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var result = await _accounts.UpdateAsync(userId, request, adminId, cancellationToken);

        return result == AccountOperationResult.Success
            ? Ok(ApiResponse<object>.Ok(null!, "Account updated."))
            : MapFailure<object>(result);
    }

    /// <summary>
    /// FT-08 — vô hiệu hoá tài khoản.
    /// Giao diện phải hỏi xác nhận trước khi gọi tới đây.
    /// </summary>
    [HttpPut("{userId:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetActingAdminId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var result = await _accounts.DeactivateAsync(userId, adminId, cancellationToken);

        return result == AccountOperationResult.Success
            ? Ok(ApiResponse<object>.Ok(null!, "Account deactivated permanently."))
            : MapFailure<object>(result);
    }

    /// <summary>
    /// Khôi phục hoạt động tài khoản bị vô hiệu hóa.
    /// </summary>
    [HttpPut("{userId:guid}/reactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetActingAdminId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var result = await _accounts.ReactivateAsync(adminId, userId, "Reactivated by admin via UI", cancellationToken);

        return result == AccountOperationResult.Success
            ? Ok(ApiResponse<object>.Ok(null!, "Account reactivated successfully."))
            : MapFailure<object>(result);
    }

    /// <summary>
    /// UC-03 AF-02 — Admin cấp lại mật khẩu hộ, dùng khi chủ tài khoản không vào được email.
    ///
    /// BR-03 sửa lại 06/08/2026, mở rộng lần 2 — KHÔNG còn phân biệt có/không có email nữa,
    /// KHÔNG BAO GIỜ gửi email ở đường này nữa. Phản hồi luôn mang plaintext (xem
    /// AdminResetOutcome) — trang quản trị (FE, `features/user-role-management/user-list.tsx`)
    /// đã có UI hiện trường này (dialog "Mật khẩu tạm"), dùng chung cho mọi luồng Admin
    /// reset-password, không riêng UC-06.
    /// </summary>
    [HttpPut("{userId:guid}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetActingAdminId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                StatusCodes.Status401Unauthorized, "Invalid access token."));
        }

        var outcome = await _passwordReset.AdminResetAsync(userId, adminId, cancellationToken);

        if (outcome.Result != AccountOperationResult.Success)
        {
            return MapFailure<object>(outcome.Result);
        }

        // outcome.TemporaryPassword luôn có giá trị khi Result là Success — xem AdminResetAsync.
        return Ok(ApiResponse<string>.Ok(
            outcome.TemporaryPassword!, "Temporary password generated — communicate it to the account holder directly."));
    }

    // ---- helpers ----

    /// <summary>
    /// Id của Admin đang thao tác, lấy từ claim trong token — KHÔNG bao giờ nhận từ request,
    /// nếu không thì ai cũng giả danh người khác được.
    /// </summary>
    private bool TryGetActingAdminId(out Guid adminId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);

    private IActionResult MapFailure<T>(AccountOperationResult result) => result switch
    {
        AccountOperationResult.NotFound =>
            NotFound(ApiResponse<T>.Fail(StatusCodes.Status404NotFound, "Account not found.")),

        AccountOperationResult.PhoneAlreadyUsed =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest, "This phone number is already used by another account.")),

        AccountOperationResult.EmailAlreadyUsed =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest, "This email is already used by another account.")),

        AccountOperationResult.InvalidRole =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest, "Role must be one of DOCTOR, NURSE or PATIENT.")),

        AccountOperationResult.CannotTargetSelf =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest, "You cannot lock or deactivate your own account.")),

        AccountOperationResult.AccountIsDeactivated =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest, "This account has been deactivated and cannot be changed.")),

        AccountOperationResult.AccountHasNoEmail =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest,
                "This account has no email address, so a temporary password cannot be delivered.")),

        AccountOperationResult.CannotChangeAdminRole =>
            BadRequest(ApiResponse<T>.Fail(
                StatusCodes.Status400BadRequest,
                "An administrator's role cannot be changed here, and no account can be promoted "
                + "to administrator on this screen.")),

        // 502 chứ không phải 400: dữ liệu Admin gửi lên không sai chỗ nào, hỏng là ở máy chủ
        // mail phía sau. Trả 400 thì Admin ngồi sửa lại form mãi không ra.
        AccountOperationResult.EmailNotSent =>
            StatusCode(StatusCodes.Status502BadGateway, ApiResponse<T>.Fail(
                StatusCodes.Status502BadGateway,
                "The temporary password could not be emailed, so the current password was left "
                + "unchanged. Please try again later.")),

        _ => BadRequest(ApiResponse<T>.Fail(StatusCodes.Status400BadRequest, "Operation failed.")),
    };
}
