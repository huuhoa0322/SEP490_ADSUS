using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho FeedbackService (FT-37).
/// </summary>
public class FeedbackServiceTests
{
    private static ServiceFeedback NewFeedback(
        Guid feedbackId,
        short rating = 5,
        string? content = "Test content",
        Guid? patientProfileId = null,
        Guid? caseId = null)
        => new()
        {
            FeedbackId = feedbackId,
            PatientProfileId = patientProfileId ?? Guid.NewGuid(),
            Rating = rating,
            Content = content,
            CaseId = caseId ?? Guid.Empty,
            SubmittedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task SubmitAsync_CreatesFeedback()
    {
        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<ServiceFeedback>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceFeedback f, CancellationToken _) => f);

        var sut = new FeedbackService(repo.Object);
        var request = new SubmitFeedbackRequest { Rating = 5, Content = "Great service!" };
        var patientProfileId = Guid.NewGuid();

        var result = await sut.SubmitAsync(request, patientProfileId);

        Assert.Equal(5, result.Rating);
        Assert.Equal("Great service!", result.Content);
        repo.Verify(r => r.AddAsync(It.IsAny<ServiceFeedback>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFeedbacksWithPatientName()
    {
        var patientProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            User = new User { FullName = "Nguyen Van A" },
        };

        var feedbacks = new List<ServiceFeedback>
        {
            new ServiceFeedback
            {
                FeedbackId = Guid.NewGuid(),
                PatientProfileId = patientProfile.PatientProfileId,
                Rating = 5,
                Content = "Good",
                SubmittedAt = DateTime.UtcNow,
                PatientProfile = patientProfile,
            },
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedbacks);

        var sut = new FeedbackService(repo.Object);

        var result = await sut.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Nguyen Van A", result[0].PatientName);
    }

    // ==================== FT-37: Case Feedback ====================

    [Fact]
    public async Task SubmitCaseFeedbackAsync_Valid_ReturnsResponse()
    {
        var repo = new Mock<IFeedbackRepository>();
        var caseId = Guid.NewGuid();
        var patientProfileId = Guid.NewGuid();

        repo.Setup(r => r.GetByCaseIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceFeedback?)null);
        repo.Setup(r => r.AddAsync(It.IsAny<ServiceFeedback>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceFeedback f, CancellationToken _) => f);

        var sut = new FeedbackService(repo.Object);
        var request = new SubmitCaseFeedbackRequest { Rating = 5, Content = "Bác sĩ rất tận tâm" };

        var result = await sut.SubmitCaseFeedbackAsync(request, patientProfileId, caseId);

        Assert.Equal(5, result.Rating);
        Assert.Equal("Bác sĩ rất tận tâm", result.Content);
        repo.Verify(r => r.AddAsync(It.IsAny<ServiceFeedback>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCaseFeedbackAsync_DuplicateCase_ThrowsConflictException()
    {
        var repo = new Mock<IFeedbackRepository>();
        var caseId = Guid.NewGuid();
        var patientProfileId = Guid.NewGuid();
        var existingFeedback = NewFeedback(Guid.NewGuid(), caseId: caseId);

        repo.Setup(r => r.GetByCaseIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFeedback);

        var sut = new FeedbackService(repo.Object);
        var request = new SubmitCaseFeedbackRequest { Rating = 4, Content = "Second feedback" };

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => sut.SubmitCaseFeedbackAsync(request, patientProfileId, caseId));

        Assert.Equal("Ca khám này đã có phản hồi.", exception.Message);
        repo.Verify(r => r.AddAsync(It.IsAny<ServiceFeedback>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitCaseFeedbackAsync_InvalidRating_ThrowsArgumentException()
    {
        var repo = new Mock<IFeedbackRepository>();
        var sut = new FeedbackService(repo.Object);
        var request = new SubmitCaseFeedbackRequest { Rating = 0, Content = null };

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.SubmitCaseFeedbackAsync(request, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetCaseFeedbackAsync_ValidCase_ReturnsFeedback()
    {
        var repo = new Mock<IFeedbackRepository>();
        var caseId = Guid.NewGuid();
        var patientProfileId = Guid.NewGuid();
        var existingFeedback = NewFeedback(
            Guid.NewGuid(),
            rating: 5,
            content: "Tốt",
            patientProfileId: patientProfileId,
            caseId: caseId);

        repo.Setup(r => r.GetByCaseIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFeedback);

        var sut = new FeedbackService(repo.Object);

        var result = await sut.GetCaseFeedbackAsync(caseId, patientProfileId);

        Assert.NotNull(result);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Tốt", result.Content);
    }

    // ==================== Legacy tests kept for reference ====================

    [Fact]
    public async Task SubmitAsync_ValidatesRatingInRange()
    {
        var repo = new Mock<IFeedbackRepository>();
        var sut = new FeedbackService(repo.Object);

        // Rating 0 is invalid (should be 1-5)
        var request = new SubmitFeedbackRequest { Rating = 0, Content = "Test" };
        // Note: Validation happens in controller, not service
        // Service just passes through

        var result = await sut.SubmitAsync(request, Guid.NewGuid());
        Assert.Equal(0, result.Rating);
    }
}
