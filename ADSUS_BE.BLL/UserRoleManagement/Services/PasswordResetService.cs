using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// UC-03 FT-06 — cấp lại mật khẩu.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private readonly IUserRepository _users;
    private readonly IEmailService _email;
    private readonly AccountAuditTrail _audit;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly Func<Func<Task>, Task> _dispatchBackground;

    public PasswordResetService(
        IUserRepository users,
        IEmailService email,
        AccountAuditTrail audit,
        IServiceScopeFactory scopeFactory,
        ILogger<PasswordResetService> logger,
        Func<Func<Task>, Task>? dispatchBackground = null)
    {
        _users = users;
        _email = email;
        _audit = audit;
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Điểm nối (seam) để unit test chạy được xác định: mặc định (production thật) là
        // "bắn rồi bỏ đó" — lên lịch bằng Task.Run và trả lời ngay, không đợi việc chạy xong.
        // Bài test truyền vào 1 hàm khác (VD "work => work()") để việc nền chạy NGAY và chờ
        // được trong cùng lệnh await, nếu không mọi assertion đọc trạng thái ngay sau khi gọi
        // RequestSelfServiceResetAsync sẽ thành race condition — có lúc qua có lúc trượt.
        _dispatchBackground = dispatchBackground ?? (work =>
        {
            _ = Task.Run(work);
            return Task.CompletedTask;
        });
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

        // GỬI NỀN, KHÔNG BẮT ENDPOINT ĐỢI (thêm 28/08/2026).
        //
        // Trước đây hàm này await thẳng SendTemporaryPasswordAsync rồi mới trả lời, nên khi
        // dịch vụ gửi mail chậm (đo thật trên Render: có lúc gần 2.5 phút, dù cuối cùng vẫn
        // gửi được) thì cả request HTTP bị treo theo — timeout phía client (FE giới hạn 60s)
        // kích hoạt trước khi backend kịp trả lời, người dùng thấy "mất kết nối" dù backend
        // vẫn đang chạy bình thường. Endpoint này vốn dĩ luôn trả về đúng 1 câu chung
        // (BR/AF-01, chống dò tài khoản) — không có lý do gì bắt người dùng đợi kết quả gửi
        // thư thật trước khi thấy câu đó.
        //
        // Dùng IServiceScopeFactory chứ không dùng thẳng _users/_email/_audit đang có: scope
        // của request này (và AppDbContext bên trong nó) sẽ bị hủy ngay sau khi phương thức
        // này return, trong khi tác vụ nền vẫn còn chạy — chạm vào DbContext đã dispose sẽ
        // ném ObjectDisposedException. Vì cùng lý do đó, CancellationToken.None được dùng ở
        // dưới thay vì cancellationToken của tham số: token đó gắn với request HTTP, sẽ bị
        // hủy ngay khi response trả về — dùng nó ở đây sẽ hủy luôn việc gửi thư trước khi kịp
        // chạy xong.
        await _dispatchBackground(() => CompleteSelfServiceResetInBackgroundAsync(
            user!.UserId, user.FullName, user.Email!, temporaryPassword, hash));
    }

    /// <summary>
    /// Phần việc chậm (gửi thư, rồi mới đổi mật khẩu) của <see cref="RequestSelfServiceResetAsync"/>,
    /// chạy nền sau khi request HTTP đã trả lời. Tự mở 1 scope DI riêng — xem lý do ở nơi gọi.
    /// </summary>
    private async Task CompleteSelfServiceResetInBackgroundAsync(
        Guid userId, string fullName, string toEmail, string temporaryPassword, string hash)
    {
        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var audit = scope.ServiceProvider.GetRequiredService<AccountAuditTrail>();

        try
        {
            // GỬI THƯ TRƯỚC, LƯU SAU — thứ tự này quan trọng.
            //
            // Làm ngược lại (lưu rồi mới gửi) thì khi máy chủ mail trục trặc, mật khẩu cũ đã bị
            // thay mất trong khi mật khẩu mới không tới tay ai: chủ tài khoản bị nhốt ở ngoài
            // đúng lúc họ đang cần vào. Gửi trước thì thư hỏng chỉ có nghĩa là không có gì thay
            // đổi cả, họ thử lại là xong.
            var daGui = await emailService.SendTemporaryPasswordAsync(
                toEmail, fullName, temporaryPassword, CancellationToken.None);

            // AF-01 — vẫn không được phát ra tín hiệu nào khác nhau ra ngoài. Vì đã chạy nền,
            // không còn HTTP response nào để phân nhánh nữa — chi tiết lỗi chỉ còn nằm trong
            // log của server.
            if (!daGui) return;

            // Đọc lại user trong scope MỚI này — entity cũ (đọc ở scope của request) thuộc
            // DbContext đã bị dispose, sửa-rồi-lưu nó ở đây sẽ ném ObjectDisposedException.
            var user = await users.GetForUpdateAsync(userId, CancellationToken.None);
            if (user is null) return; // Hiếm: tài khoản bị xoá đúng lúc thư đang trên đường đi.

            user.PasswordHash = hash;

            // BR-04 — dù đi đường nào thì cũng phải đổi mật khẩu ở lần đăng nhập kế tiếp (UC-25).
            user.MustChangePassword = true;
            user.UpdatedAt = DateTime.UtcNow;

            // Người thực hiện chính là chủ tài khoản. Ghi lại để sau này còn đối chiếu được:
            // một tài khoản bị đặt lại mật khẩu liên tục là dấu hiệu có người đang thử chiếm.
            //
            // Chỉ ghi khi ĐÃ khớp và ĐÃ gửi được thư, nên nhật ký không biến thành chỗ dò xem số
            // điện thoại nào có tài khoản (AF-01).
            await audit.RecordAsync(
                user.UserId, AccountAuditTrail.SelfServiceResetPassword, user,
                "người dùng tự yêu cầu cấp lại mật khẩu", CancellationToken.None);

            await users.SaveChangesAsync(CancellationToken.None);

            // KHÔNG log mật khẩu — chỉ log việc đã xảy ra, đúng giới hạn của IEmailService.
            _logger.LogInformation(
                "Account {UserId} completed a self-service password reset", user.UserId);
        }
        catch (Exception ex)
        {
            // Tác vụ nền không có ai await nó cả — không bắt ở đây thì exception rơi vào
            // UnobservedTaskException, im lặng biến mất, không ai biết vì sao mật khẩu không
            // đổi dù thư có vẻ đã gửi.
            _logger.LogError(
                ex, "Background self-service password reset that lam that bai cho tai khoan {UserId}.", userId);
        }
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

        // Sửa-rồi-lưu — dùng GetForUpdateAsync (P11 review Module 2, 12/08/2026).
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);
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

        await _audit.RecordAsync(
            actingAdminId, AccountAuditTrail.AdminResetPassword, user,
            "quản trị viên cấp lại mật khẩu hộ", cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);

        // KHÔNG log mật khẩu — chỉ log việc đã xảy ra.
        _logger.LogInformation(
            "Admin {ActingAdminId} reset the password for account {UserId}", actingAdminId, userId);

        return new AdminResetOutcome(AccountOperationResult.Success, temporaryPassword);
    }
}
