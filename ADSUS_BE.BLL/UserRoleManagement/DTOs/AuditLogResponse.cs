namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Một dòng nhật ký thao tác, đưa lên màn hình quản trị.
///
/// KHÔNG có ngày sinh và không có dữ liệu y tế nào — xem AccountAuditTrail (UC-04 BR-01).
/// </summary>
public class AuditLogResponse
{
    public Guid LogId { get; set; }

    /// <summary>Người thực hiện thao tác.</summary>
    public Guid ActorId { get; set; }

    public string ActorName { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    /// <summary>
    /// Mã hành động, viết hoa gạch dưới (CREATE_ACCOUNT, LOCK_ACCOUNT...).
    /// Giao diện tự dịch sang tiếng Việt — cùng cách làm với vai trò và trạng thái.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Mô tả tự do: đối tượng bị tác động và thay đổi gì.</summary>
    public string? Detail { get; set; }

    /// <summary>Thời điểm thực hiện, giờ UTC.</summary>
    public DateTime PerformedAt { get; set; }
}
