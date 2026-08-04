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
/// Response cho feedback list (Admin).
/// </summary>
public sealed class FeedbackResponse
{
    public Guid Id { get; init; }
    public short Rating { get; init; }
    public string? Content { get; init; }
    public DateTime SubmittedAt { get; init; }
    public string PatientName { get; init; } = string.Empty;
}
