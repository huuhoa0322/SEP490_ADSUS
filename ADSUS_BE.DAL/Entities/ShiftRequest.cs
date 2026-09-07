using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Yêu cầu nghỉ ca / tăng ca của bác sĩ — Admin duyệt trước khi hệ thống tự đóng/mở slot tương ứng.
/// </summary>
public partial class ShiftRequest
{
    public Guid RequestId { get; set; }

    public Guid UserId { get; set; }

    public DateOnly RequestDate { get; set; }

    public string Reason { get; set; } = null!;

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
