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
    private readonly AccountAuditTrail _audit;

    public PasswordResetService(IUserRepository users, IEmailService email, AccountAuditTrail audit)
    {
        _users = users;
        _email = email;
        _audit = audit;
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

        // GỬI THƯ TRƯỚC, LƯU SAU — thứ tự này quan trọng.
        //
        // Làm ngược lại (lưu rồi mới gửi) thì khi máy chủ mail trục trặc, mật khẩu cũ đã bị
        // thay mất trong khi mật khẩu mới không tới tay ai: chủ tài khoản bị nhốt ở ngoài
        // đúng lúc họ đang cần vào. Gửi trước thì thư hỏng chỉ có nghĩa là không có gì thay
        // đổi cả, họ thử lại là xong.
        var daGui = await _email.SendTemporaryPasswordAsync(
            user!.Email!, user.FullName, temporaryPassword, cancellationToken);

        // AF-01 — vẫn không được phát ra tín hiệu nào khác nhau. Phương thức trả về void nên
        // ở đây không có gì rò rỉ ra ngoài được; chi tiết lỗi đã nằm trong log của server.
        if (!daGui) return;

        user.PasswordHash = hash;

        // BR-04 — dù đi đường nào thì cũng phải đổi mật khẩu ở lần đăng nhập kế tiếp (UC-25).
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        // Người thực hiện chính là chủ tài khoản. Ghi lại để sau này còn đối chiếu được:
        // một tài khoản bị đặt lại mật khẩu liên tục là dấu hiệu có người đang thử chiếm.
        //
        // Chỉ ghi khi ĐÃ khớp và ĐÃ gửi được thư, nên nhật ký không biến thành chỗ dò xem số
        // điện thoại nào có tài khoản (AF-01).
        await _audit.RecordAsync(
            user.UserId, AccountAuditTrail.SelfServiceResetPassword, user,
            "người dùng tự yêu cầu cấp lại mật khẩu", cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);
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
        var hash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

        // Gửi thư trước, lưu sau — cùng lý do như ở đường tự phục vụ bên trên: thư không tới
        // nơi mà mật khẩu cũ đã bị thay thì chủ tài khoản mất luôn đường vào.
        var daGui = await _email.SendTemporaryPasswordAsync(
            user.Email, user.FullName, temporaryPassword, cancellationToken);

        if (!daGui) return AccountOperationResult.EmailNotSent;

        user.PasswordHash = hash;
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _audit.RecordAsync(
            actingAdminId, AccountAuditTrail.AdminResetPassword, user,
            "quản trị viên cấp lại mật khẩu hộ", cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);

        return AccountOperationResult.Success;
    }
}
