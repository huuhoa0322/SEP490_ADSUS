using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// UC-03 FT-06 — cấp lại mật khẩu.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private readonly IUserRepository _users;
    private readonly IEmailService _email;

    public PasswordResetService(IUserRepository users, IEmailService email)
    {
        _users = users;
        _email = email;
    }

    public async Task RequestSelfServiceResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var phone = request.PhoneNumber.Trim();
        var email = request.Email.Trim();

        var user = await _users.GetByPhoneAsync(phone, cancellationToken);

        // Sinh mật khẩu và băm nó LUÔN LUÔN, kể cả khi không tìm thấy tài khoản.
        //
        // BCrypt cố tình chạy chậm (~100ms). Nếu chỉ băm khi tìm thấy người dùng thì lời gọi
        // với số điện thoại không tồn tại sẽ trả về nhanh hơn hẳn — kẻ tấn công bấm giờ là
        // biết số nào có tài khoản, dù câu trả lời chữ nghĩa vẫn y hệt nhau. Cùng thủ thuật
        // đang dùng ở AuthService.LoginAsync cho GB-06.
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var hash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

        // BR-01 — phải khớp CẢ số điện thoại LẪN email.
        // AF-01 — tài khoản đã khoá hoặc vô hiệu hoá thì cũng không cấp lại.
        var matched = user is not null
                      && user.Status == UserStatus.Active
                      && user.Email is not null
                      && string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);

        if (!matched) return;

        user!.PasswordHash = hash;

        // BR-04 — dù đi đường nào thì cũng phải đổi mật khẩu ở lần đăng nhập kế tiếp (UC-25).
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        await _email.SendTemporaryPasswordAsync(
            user.Email!, user.FullName, temporaryPassword, cancellationToken);
    }

    public async Task<AccountOperationResult> AdminResetAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        // Admin tự đặt lại mật khẩu của chính mình thì đã có chức năng đổi mật khẩu (UC-25),
        // không cần đi vòng qua đây.
        if (userId == actingAdminId) return AccountOperationResult.CannotTargetSelf;

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return AccountOperationResult.NotFound;

        if (user.Status == UserStatus.Deactivated) return AccountOperationResult.AccountIsDeactivated;

        // BR-03 — mật khẩu tạm CHỈ đi qua email, không bao giờ hiện trên màn hình Admin.
        // Không có email thì không có đường giao, nên phải báo lỗi thay vì đặt lại rồi để
        // mật khẩu mới rơi vào hư không và khoá luôn chủ tài khoản ở ngoài.
        if (string.IsNullOrWhiteSpace(user.Email)) return AccountOperationResult.AccountHasNoEmail;

        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        await _email.SendTemporaryPasswordAsync(
            user.Email, user.FullName, temporaryPassword, cancellationToken);

        return AccountOperationResult.Success;
    }
}
