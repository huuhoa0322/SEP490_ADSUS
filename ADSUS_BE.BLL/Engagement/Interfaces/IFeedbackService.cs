using ADSUS_BE.BLL.Engagement.DTOs;

namespace ADSUS_BE.BLL.Engagement.Interfaces;

/// <summary>
/// Service cho ServiceFeedback (Patient gửi feedback, Admin xem).
/// </summary>
public interface IFeedbackService
{
    /// <summary>Patient gửi feedback.</summary>
    Task<FeedbackResponse> SubmitAsync(SubmitFeedbackRequest request, Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Admin xem tất cả feedback.</summary>
    Task<IReadOnlyList<FeedbackResponse>> GetAllAsync(CancellationToken ct = default);
}
