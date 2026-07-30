using System.Globalization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// UC-04 — Admin quản lý tài khoản đăng nhập.
///
/// Hai luật xuyên suốt cả lớp này:
///   BR-01 — ngày sinh của tài khoản PATIENT bị chặn cả hai chiều: không đọc ra, không ghi vào.
///   BR-05 — không bao giờ xoá bản ghi; vô hiệu hoá chỉ đổi trạng thái.
/// </summary>
public class UserAccountService : IUserAccountService
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int MaxPageSize = 100;

    /// <summary>
    /// Vai trò được phép tạo/gán qua màn này.
    /// UC-04: tài khoản Admin cấp lúc dựng hệ thống, không tạo ở đây.
    /// </summary>
    private static readonly UserRole[] AssignableRoles =
        { UserRole.Doctor, UserRole.Nurse, UserRole.Patient };

    private readonly IUserRepository _users;
    private readonly IEmailService _email;

    public UserAccountService(IUserRepository users, IEmailService email)
    {
        _users = users;
        _email = email;
    }

    public async Task<PagedResult<UserAccountResponse>> SearchAsync(
        string? keyword,
        string? role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Chặn số vô lý từ client. pageSize quá lớn là kéo cả bảng users về một lần.
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? 20 : pageSize;

        var (items, total) = await _users.SearchAsync(
            keyword,
            EnumExtensions.ParseUserRole(role),
            EnumExtensions.ParseUserStatus(status),
            page,
            pageSize,
            cancellationToken);

        return new PagedResult<UserAccountResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<UserAccountResponse?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    public async Task<(AccountOperationResult Result, UserAccountResponse? Account)> CreateAsync(
        CreateUserAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = EnumExtensions.ParseUserRole(request.Role);
        if (role is null || !AssignableRoles.Contains(role.Value))
        {
            return (AccountOperationResult.InvalidRole, null);
        }

        var phone = request.PhoneNumber.Trim();

        // BR-02 — số điện thoại là định danh đăng nhập duy nhất.
        if (await _users.PhoneExistsAsync(phone, cancellationToken))
        {
            return (AccountOperationResult.PhoneAlreadyUsed, null);
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && await _users.IsEmailUsedAsync(email, cancellationToken))
        {
            return (AccountOperationResult.EmailAlreadyUsed, null);
        }

        // BR-03 — mật khẩu tạm do hệ thống sinh, lưu dạng băm, và buộc đổi ở lần đăng nhập đầu.
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Phone = phone,
            FullName = request.FullName.Trim(),
            Email = email,
            Role = role.Value,
            Status = UserStatus.Active,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            MustChangePassword = true,
            BiometricEnabled = false,
            // BR-01 — vai trò PATIENT thì bỏ qua ngày sinh dù client có gửi lên.
            DateOfBirth = role.Value == UserRole.Patient ? null : ParseDateOrNull(request.DateOfBirth),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        // Gửi email SAU khi lưu thành công, và cố ý KHÔNG để lỗi gửi mail làm hỏng cả thao
        // tác: tài khoản đã tồn tại rồi, huỷ vì máy chủ mail trục trặc thì Admin phải tạo
        // lại từ đầu mà số điện thoại thì đã bị chiếm. Gửi lại được qua chức năng cấp lại
        // mật khẩu (UC-03).
        if (email is not null)
        {
            await _email.SendTemporaryPasswordAsync(
                email, user.FullName, temporaryPassword, cancellationToken);
        }

        // Giá trị trả về KHÔNG chứa mật khẩu tạm — PRD §6.2, không ai được thấy nó dạng đọc được.
        return (AccountOperationResult.Success, ToResponse(user));
    }

    public async Task<AccountOperationResult> UpdateAsync(
        Guid userId,
        UpdateUserAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = EnumExtensions.ParseUserRole(request.Role);
        if (role is null || !AssignableRoles.Contains(role.Value))
        {
            return AccountOperationResult.InvalidRole;
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return AccountOperationResult.NotFound;

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null
            && await _users.IsEmailUsedByAnotherUserAsync(userId, email, cancellationToken))
        {
            return AccountOperationResult.EmailAlreadyUsed;
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Role = role.Value;
        user.DateOfBirth = role.Value == UserRole.Patient ? null : ParseDateOrNull(request.DateOfBirth);
        user.UpdatedAt = DateTime.UtcNow;

        // KHÔNG đụng tới Phone (BR-02) và Status (đi qua endpoint riêng).

        await _users.SaveChangesAsync(cancellationToken);

        return AccountOperationResult.Success;
    }

    public async Task<AccountOperationResult> SetLockedAsync(
        Guid userId,
        bool locked,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        if (userId == actingAdminId) return AccountOperationResult.CannotTargetSelf;

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return AccountOperationResult.NotFound;

        // BR-05 — đã vô hiệu hoá thì không quay lại được, kể cả sang Locked hay Active.
        if (user.Status == UserStatus.Deactivated) return AccountOperationResult.AccountIsDeactivated;

        // BR-04 — chuyển Active ⇄ Locked hoàn toàn thủ công, không có job tự mở khoá.
        user.Status = locked ? UserStatus.Locked : UserStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        return AccountOperationResult.Success;
    }

    public async Task<AccountOperationResult> DeactivateAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        if (userId == actingAdminId) return AccountOperationResult.CannotTargetSelf;

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return AccountOperationResult.NotFound;

        // BR-05 — chỉ đổi trạng thái, TUYỆT ĐỐI không xoá bản ghi. Dữ liệu y tế gắn với tài
        // khoản này vẫn phải truy cập được sau khi vô hiệu hoá.
        user.Status = UserStatus.Deactivated;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        return AccountOperationResult.Success;
    }

    // ---- helpers ----

    private static UserAccountResponse ToResponse(User user) => new()
    {
        UserId = user.UserId,
        PhoneNumber = user.Phone,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToApiString(),
        Status = user.Status.ToApiString(),
        // BR-01 — không trả ngày sinh của bệnh nhân cho giao diện quản trị.
        DateOfBirth = user.Role == UserRole.Patient
            ? null
            : user.DateOfBirth?.ToString(DateFormat, CultureInfo.InvariantCulture),
        MustChangePassword = user.MustChangePassword,
        CreatedAt = user.CreatedAt,
    };

    private static DateOnly? ParseDateOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
}
