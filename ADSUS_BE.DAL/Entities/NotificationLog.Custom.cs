using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột Status (notification_status) mà scaffold không nhận diện được.
/// </summary>
public partial class NotificationLog
{
    /// <summary>
    /// Trạng thái gửi notification: SENT, DELIVERED, FAILED, READ, UNREAD.
    /// </summary>
    [Column("status")]
    public NotificationStatus Status { get; set; } = NotificationStatus.Sent;
}
