using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Feedback service - Patient gửi feedback, Admin xem.
/// </summary>
public sealed class FeedbackService : IFeedbackService
{
    private readonly IFeedbackRepository _repo;

    public FeedbackService(IFeedbackRepository repo)
    {
        _repo = repo;
    }

    public async Task<FeedbackResponse> SubmitAsync(SubmitFeedbackRequest request, Guid patientProfileId, CancellationToken ct = default)
    {
        var feedback = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = patientProfileId,
            Rating = request.Rating,
            Content = request.Content,
            SubmittedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(feedback, ct);

        return new FeedbackResponse
        {
            Id = feedback.FeedbackId,
            Rating = feedback.Rating,
            Content = feedback.Content,
            SubmittedAt = feedback.SubmittedAt,
            PatientName = string.Empty,
        };
    }

    public async Task<IReadOnlyList<FeedbackResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var feedbacks = await _repo.GetAllAsync(ct);

        return feedbacks
            .Select(f => new FeedbackResponse
            {
                Id = f.FeedbackId,
                Rating = f.Rating,
                Content = f.Content,
                SubmittedAt = f.SubmittedAt,
                PatientName = f.PatientProfile?.User?.FullName ?? string.Empty,
            })
            .ToList();
    }
}
