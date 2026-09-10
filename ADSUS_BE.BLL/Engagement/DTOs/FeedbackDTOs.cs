namespace ADSUS_BE.BLL.Engagement.DTOs;

/// <summary>
/// Request để gửi feedback (Patient).
/// Rating: 1-5 sao.
/// Content: 1-2000 ký tự.
/// </summary>
public sealed class SubmitFeedbackRequest
{
    public short Rating { get; init; }
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Response cho feedback list (Admin) bao gồm thông tin liên kết ca khám, bác sĩ, bệnh nhân.
/// </summary>
public sealed class FeedbackResponse
{
    public Guid Id { get; init; }
    public Guid CaseId { get; init; }
    public Guid PatientProfileId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string? PatientPhone { get; init; }
    public Guid DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public short Rating { get; init; }
    public string? Content { get; init; }
    public DateTime SubmittedAt { get; init; }
}

/// <summary>
/// Request gửi feedback cho ca khám (FT-37).
/// Rating: 1-5. Content: max 2000 ký tự.
/// </summary>
public sealed class SubmitCaseFeedbackRequest
{
    public short Rating { get; init; }
    public string? Content { get; init; }
}

/// <summary>
/// Response feedback ca khám cho Mobile (FT-37).
/// </summary>
public sealed class CaseFeedbackResponse
{
    public Guid Id { get; init; }
    public short Rating { get; init; }
    public string? Content { get; init; }
    public DateTime SubmittedAt { get; init; }
}
