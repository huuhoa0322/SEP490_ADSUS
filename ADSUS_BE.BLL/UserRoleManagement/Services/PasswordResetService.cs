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

        await _users.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminResetOutcome> AdminResetAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default)
    {
        // Admin tự đặt lại mật khẩu của chính mình thì đã có chức năng đổi mật khẩu (UC-25),
        // không cần đi vòng qua đây.
        if (userId == actingAdminId)
            return new AdminResetOutcome(AccountOperationResult.CannotTargetSelf, null);

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return new AdminResetOutcome(AccountOperationResult.NotFound, null);

        if (user.Status == UserStatus.Deactivated)
            return new AdminResetOutcome(AccountOperationResult.AccountIsDeactivated, null);

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var hash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

        // Quyết định ghi đè 06/08/2026, mở rộng lần 2 — KHÔNG còn phân biệt có/không có email
        // nữa. Người thao tác (Admin/Điều dưỡng) luôn thấy mật khẩu tạm ngay tại đây để đọc
        // trực tiếp cho chủ tài khoản, thống nhất hoàn toàn với luồng tạo tài khoản mới
        // (CreateAsync). Không còn nhánh gửi thư ở đây nữa nên không còn rủi ro "gửi trước lưu
        // sau" phải giữ — lưu ngay, không đường lùi cần giữ. Email giờ CHỈ còn dùng cho tự
        // phục vụ quên mật khẩu (RequestSelfServiceResetAsync, UC-03).
        user.PasswordHash = hash;
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);

        return new AdminResetOutcome(AccountOperationResult.Success, temporaryPassword);
    }
}
