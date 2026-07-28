using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Phản hồi/đánh giá dịch vụ (FT-36), thang 1–5 sao.
/// </summary>
public partial class ServiceFeedback
{
    public Guid FeedbackId { get; set; }

    public Guid PatientProfileId { get; set; }

    public short Rating { get; set; }

    public string? Content { get; set; }

    public DateTime SubmittedAt { get; set; }

    public virtual PatientProfile PatientProfile { get; set; } = null!;
}
