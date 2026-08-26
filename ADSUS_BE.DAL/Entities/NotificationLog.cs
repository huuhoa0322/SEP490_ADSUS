using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class NotificationLog
{
    public Guid LogId { get; set; }

    public Guid UserId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Body { get; set; }

    public string? Payload { get; set; }

    public string? DeepLink { get; set; }

    public string? FcmMessageId { get; set; }

    public DateTime SentAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int? RetryCount { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string Type { get; set; } = null!;

    public string? Metadata { get; set; }

    public virtual User User { get; set; } = null!;
}
