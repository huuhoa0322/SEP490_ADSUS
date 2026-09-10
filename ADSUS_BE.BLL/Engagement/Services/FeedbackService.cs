using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
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
            CaseId = null,
            Rating = request.Rating,
            Content = request.Content,
            SubmittedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(feedback, ct);

        return new FeedbackResponse
        {
            Id = feedback.FeedbackId,
            CaseId = Guid.Empty,
            PatientProfileId = patientProfileId,
            Rating = feedback.Rating,
            Content = feedback.Content,
            SubmittedAt = feedback.SubmittedAt,
            PatientName = string.Empty,
            PatientPhone = string.Empty,
            DoctorId = Guid.Empty,
            DoctorName = "Không xác định",
        };
    }

    public async Task<IReadOnlyList<FeedbackResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var feedbacks = await _repo.GetAllAsync(ct);

        return feedbacks
            .Select(MapToFeedbackResponse)
            .ToList();
    }

    public async Task<PagedResult<FeedbackResponse>> GetPagedAsync(
        string? keyword,
        short? rating,
        int page = 1,
        int pageSize = 15,
        CancellationToken ct = default)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize is < 1 or > 100 ? 15 : pageSize;

        var (items, totalCount) = await _repo.GetPagedAsync(keyword, rating, effectivePage, effectivePageSize, ct);

        var dtos = items.Select(MapToFeedbackResponse).ToList();
        var totalPages = effectivePageSize > 0 ? (int)Math.Ceiling((double)totalCount / effectivePageSize) : 0;

        return new PagedResult<FeedbackResponse>(dtos, effectivePage, effectivePageSize, totalCount, totalPages);
    }

    private static FeedbackResponse MapToFeedbackResponse(ServiceFeedback f)
    {
        return new FeedbackResponse
        {
            Id = f.FeedbackId,
            CaseId = f.CaseId ?? Guid.Empty,
            PatientProfileId = f.PatientProfileId,
            PatientName = f.PatientProfile?.User?.FullName ?? string.Empty,
            PatientPhone = f.PatientProfile?.User?.Phone ?? string.Empty,
            DoctorId = f.Case?.DoctorId ?? (f.Case?.Doctor?.UserId ?? Guid.Empty),
            DoctorName = !string.IsNullOrWhiteSpace(f.Case?.Doctor?.FullName) ? f.Case.Doctor.FullName : "Không xác định",
            Rating = f.Rating,
            Content = f.Content,
            SubmittedAt = f.SubmittedAt,
        };
    }

    public async Task<CaseFeedbackResponse> SubmitCaseFeedbackAsync(
        SubmitCaseFeedbackRequest request,
        Guid patientProfileId,
        Guid caseId,
        CancellationToken ct = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
            throw new ArgumentException("Đánh giá phải từ 1 đến 5 sao.");

        if (request.Content?.Length > 2000)
            throw new ArgumentException("Nội dung phản hồi không được quá 2000 ký tự.");

        var existing = await _repo.GetByCaseIdAsync(caseId, ct);
        if (existing != null)
            throw new ConflictException("Ca khám này đã có phản hồi.");

        var feedback = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = patientProfileId,
            CaseId = caseId,
            Rating = request.Rating,
            Content = request.Content,
            SubmittedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(feedback, ct);

        return new CaseFeedbackResponse
        {
            Id = feedback.FeedbackId,
            Rating = feedback.Rating,
            Content = feedback.Content,
            SubmittedAt = feedback.SubmittedAt,
        };
    }

    public async Task<CaseFeedbackResponse?> GetCaseFeedbackAsync(
        Guid caseId,
        Guid patientProfileId,
        CancellationToken ct = default)
    {
        var feedback = await _repo.GetByCaseIdAsync(caseId, ct);

        if (feedback == null)
            return null;

        // Security: patient only sees their own feedback
        if (feedback.PatientProfileId != patientProfileId)
            return null;

        return new CaseFeedbackResponse
        {
            Id = feedback.FeedbackId,
            Rating = feedback.Rating,
            Content = feedback.Content,
            SubmittedAt = feedback.SubmittedAt,
        };
    }
}
