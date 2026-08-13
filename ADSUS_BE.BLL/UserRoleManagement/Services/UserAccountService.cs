using System.Globalization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Mappers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using PagedResult = ADSUS_BE.BLL.UserRoleManagement.DTOs.PagedResult<ADSUS_BE.BLL.UserRoleManagement.DTOs.UserAccountResponse>;

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
    private readonly AccountAuditTrail _audit;
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(
        IUserRepository users, AccountAuditTrail audit, ILogger<UserAccountService> logger)
    {
        _users = users;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResult> SearchAsync(
        string? keyword,
        string? role,
        string? status,
        int page,
        int pageSize,
        Guid actingAdminId,
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

        return new PagedResult
        {
            Items = items.Select(u => UserAccountMapper.ToResponse(u, actingAdminId)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<UserAccountResponse?> GetByIdAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        // Chỉ đọc để đổ vào form sửa (SCR-07), không lưu gì ở đây — dùng bản AsNoTracking
        // (P11 review Module 2, 12/08/2026).
        var user = await _users.GetByIdReadOnlyAsync(userId, cancellationToken);
        return user is null ? null : UserAccountMapper.ToResponse(user, actingAdminId);
    }

    public async Task<(AccountOperationResult Result, UserAccountResponse? Account, string? TemporaryPassword)> CreateAsync(
        CreateUserAccountRequest request,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var role = EnumExtensions.ParseUserRole(request.Role);
        if (role is null || !AssignableRoles.Contains(role.Value))
        {
            return (AccountOperationResult.InvalidRole, null, null);
        }

        var phone = request.PhoneNumber.Trim();

        // BR-02 — số điện thoại là định danh đăng nhập duy nhất.
        if (await _users.PhoneExistsAsync(phone, cancellationToken))
        {
            return (AccountOperationResult.PhoneAlreadyUsed, null, null);
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && await _users.IsEmailUsedAsync(email, cancellationToken))
        {
            return (AccountOperationResult.EmailAlreadyUsed, null, null);
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

        // Nhật ký đi cùng một lượt lưu với chính bản ghi tài khoản — xem AccountAuditTrail.
        await _audit.RecordAsync(
            actingAdminId, AccountAuditTrail.CreateAccount, user,
            cancellationToken: cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);

        // Sửa 12/08/2026 — không còn gửi email nữa, thống nhất với UC-03 AF-02/UC-06
        // AF-01/AF-03: mật khẩu tạm trả plaintext MỘT LẦN ngay tại đây để Admin đọc trực
        // tiếp cho chủ tài khoản, không phụ thuộc tài khoản có khai email hay không. Email
        // giờ CHỈ còn dùng cho tự phục vụ quên mật khẩu (RequestSelfServiceResetAsync,
        // UC-03) — không còn vai trò gì ở luồng tạo tài khoản.
        //
        // response (UserAccountResponse) KHÔNG chứa mật khẩu tạm dưới bất kỳ hình thức nào
        // (PRD §6.2) — giá trị plaintext chỉ ra khỏi lớp này qua phần tử thứ ba của tuple,
        // đúng một lần. Tài khoản vừa tạo chắc chắn không phải Admin đang thao tác, nên cờ
        // IsCurrentUser luôn là false ở đây.
        var response = UserAccountMapper.ToResponse(user, Guid.Empty);

        _logger.LogInformation(
            "Admin {ActingAdminId} created account {UserId} with role {Role}",
            actingAdminId, user.UserId, role.Value);

        return (AccountOperationResult.Success, response, temporaryPassword);
    }

    public async Task<AccountOperationResult> UpdateAsync(
        Guid userId,
        UpdateUserAccountRequest request,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var role = EnumExtensions.ParseUserRole(request.Role);
        if (role is null)
        {
            return AccountOperationResult.InvalidRole;
        }

        // Sửa-rồi-lưu — dùng GetForUpdateAsync (P11 review Module 2, 12/08/2026).
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);
        if (user is null) return AccountOperationResult.NotFound;

        // Vai trò ADMIN bị đóng băng ở CẢ HAI CHIỀU.
        //
        // UC-04 chỉ cho gán [Doctor, Nurse, Patient] và ghi rõ "Admin accounts are not created
        // on this screen". Hệ quả nếu không chặn: ô vai trò trên form không có lựa chọn ADMIN
        // nên khi mở tài khoản Admin ra sửa, nó rơi về giá trị đầu danh sách — chỉ cần bấm Lưu
        // để đổi cái tên là mất luôn quyền quản trị, không có cảnh báo nào. Mà mất Admin cuối
        // cùng thì không còn ai tạo lại được, kể cả chính người vừa bấm.
        //
        // Chiều ngược lại cũng chặn: không được nâng người khác lên Admin qua màn này.
        var dangLaAdmin = user.Role == UserRole.Admin;
        var muonThanhAdmin = role.Value == UserRole.Admin;

        if (dangLaAdmin != muonThanhAdmin)
        {
            return AccountOperationResult.CannotChangeAdminRole;
        }

        // Ngoài ADMIN thì chỉ nhận ba vai trò gán được.
        if (role.Value != UserRole.Admin && !AssignableRoles.Contains(role.Value))
        {
            return AccountOperationResult.InvalidRole;
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null
            && await _users.IsEmailUsedByAnotherUserAsync(userId, email, cancellationToken))
        {
            return AccountOperationResult.EmailAlreadyUsed;
        }

        // Giữ lại vai trò cũ TRƯỚC khi ghi đè, để nhật ký nói được là đã đổi từ gì sang gì.
        // Đây là thay đổi đáng theo dõi nhất ở màn này: đổi vai trò là đổi quyền truy cập.
        var vaiTroCu = user.Role;

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Role = role.Value;
        // BR-01 — tài khoản BỆNH NHÂN thì KHÔNG ĐỤNG vào ngày sinh, giữ nguyên giá trị đang có.
        //
        // Trước đây chỗ này gán null, với lý do "xoá đi thì Admin không đọc được dữ liệu y
        // tế". Lý do đó sai: ToResponse đã lọc bỏ ngày sinh của PATIENT rồi, Admin không đọc
        // được dù cột có dữ liệu.
        //
        // Và từ khi UC-06 cho Điều dưỡng khai ngày sinh bệnh nhân, gán null trở thành PHÁ DỮ
        // LIỆU: Admin chỉ sửa cái tên cho đúng chính tả là ngày sinh Điều dưỡng nhập bị xoá
        // sạch — mà Admin không nhìn thấy ô đó nên không hề biết mình vừa làm gì.
        //
        // Giữ nguyên vẫn thoả BR-01 trọn vẹn: Admin không đọc được (ToResponse lọc) và không
        // ghi được (dòng này bỏ qua mọi giá trị client gửi lên).
        if (role.Value != UserRole.Patient)
        {
            user.DateOfBirth = ParseDateOrNull(request.DateOfBirth);
        }
        user.UpdatedAt = DateTime.UtcNow;

        // KHÔNG đụng tới Phone (BR-02) và Status (đi qua endpoint riêng).

        var doiVaiTro = vaiTroCu != role.Value
            ? $"đổi vai trò {vaiTroCu.ToString().ToUpperInvariant()} → {role.Value.ToString().ToUpperInvariant()}"
            : "sửa thông tin";

        await _audit.RecordAsync(
            actingAdminId, AccountAuditTrail.UpdateAccount, user, doiVaiTro, cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {ActingAdminId} updated account {UserId} ({Detail})",
            actingAdminId, user.UserId, doiVaiTro);

        return AccountOperationResult.Success;
    }

    public async Task<AccountOperationResult> DeactivateAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        if (userId == actingAdminId) return AccountOperationResult.CannotTargetSelf;

        // Sửa-rồi-lưu — dùng GetForUpdateAsync (P11 review Module 2, 12/08/2026).
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);
        if (user is null) return AccountOperationResult.NotFound;

        // BR-05 — chỉ đổi trạng thái, TUYỆT ĐỐI không xoá bản ghi. Dữ liệu y tế gắn với tài
        // khoản này vẫn phải truy cập được sau khi vô hiệu hoá.
        var trangThaiCu = user.Status;

        user.Status = UserStatus.Deactivated;
        user.UpdatedAt = DateTime.UtcNow;

        // Thao tác một chiều, không có đường hoàn tác (BR-05) — càng phải ghi lại ai đã bấm.
        await _audit.RecordAsync(
            actingAdminId,
            AccountAuditTrail.DeactivateAccount,
            user,
            $"vô hiệu hoá vĩnh viễn, trạng thái trước đó {trangThaiCu.ToString().ToUpperInvariant()}",
            cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {ActingAdminId} deactivated account {UserId} (was {PreviousStatus})",
            actingAdminId, user.UserId, trangThaiCu);

        return AccountOperationResult.Success;
    }

    // ---- helpers ----

    private static DateOnly? ParseDateOrNull(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
}
