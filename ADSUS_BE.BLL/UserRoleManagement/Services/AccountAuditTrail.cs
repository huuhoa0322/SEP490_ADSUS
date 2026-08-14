using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.UserRoleManagement.Services;

/// <summary>
/// Ghi nhật ký các thao tác quản trị tài khoản (UC-04) vào bảng <c>audit_log</c>.
///
/// Vì sao cần: bảng này có sẵn trong lược đồ và Module 6 (quản lý mô hình AI) đã ghi vào,
/// nhưng UC-04 thì không ghi dòng nào — tức là khoá, vô hiệu hoá hay đổi vai trò một tài
/// khoản đều không để lại dấu vết gì. Với hệ thống y tế thì đó là chỗ hổng: mất tài khoản
/// hay bị đổi quyền mà không truy được ai làm, lúc nào.
///
/// ĐIỂM QUAN TRỌNG NHẤT — ghi cùng một lượt lưu với thay đổi chính:
/// lớp này chỉ <c>Add</c> chứ KHÔNG tự gọi <c>SaveChanges</c>. Cả hai repository dùng chung
/// một <c>AppDbContext</c>, nên một lệnh SaveChanges của bên gọi commit cả hai trong cùng
/// giao dịch. Tách ra lưu hai lần thì có lúc tài khoản đổi mà nhật ký không ghi được — mà
/// nhật ký chỉ có giá trị khi nó không bao giờ sót.
///
/// KHÔNG BAO GIỜ đưa ngày sinh vào phần mô tả: UC-04 BR-01 cấm Admin xem ngày sinh của bệnh
/// nhân, chặn ở API rồi mà rò qua nhật ký thì cũng như không. Số điện thoại và họ tên thì
/// được — Admin vẫn thấy chúng trên SCR-06.
/// </summary>
public class AccountAuditTrail
{
    private readonly IAuditLogRepository _auditLogs;

    public AccountAuditTrail(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    /// <summary>Tên hành động, viết hoa gạch dưới — cùng quy ước với Module 6.</summary>
    public const string CreateAccount = "CREATE_ACCOUNT";
    public const string UpdateAccount = "UPDATE_ACCOUNT";
    public const string DeactivateAccount = "DEACTIVATE_ACCOUNT";
    public const string AdminResetPassword = "ADMIN_RESET_PASSWORD";
    public const string SelfServiceResetPassword = "SELF_RESET_PASSWORD";

    /// <summary>
    /// Xếp một dòng nhật ký vào hàng chờ. Bên gọi phải gọi SaveChanges sau đó — xem chú thích
    /// của lớp về lý do không tự lưu ở đây.
    /// </summary>
    public Task RecordAsync(
        Guid actorId,
        string action,
        User target,
        string? extra = null,
        CancellationToken cancellationToken = default)
    {
        // Ghi cả họ tên lẫn số điện thoại: chỉ có id thì đọc lại nhật ký sau vài tháng không
        // ai hình dung được đó là ai, mà tài khoản thì có thể đã đổi tên từ lâu.
        var detail = $"{target.FullName} ({target.Phone}, {target.Role.ToString().ToUpperInvariant()})";

        if (!string.IsNullOrWhiteSpace(extra))
        {
            detail += $" — {extra}";
        }

        return _auditLogs.AddAsync(
            new AuditLog
            {
                LogId = Guid.NewGuid(),
                ActorId = actorId,
                Action = action,
                Detail = detail,
                PerformedAt = DateTime.UtcNow,
            },
            cancellationToken);
    }
}
