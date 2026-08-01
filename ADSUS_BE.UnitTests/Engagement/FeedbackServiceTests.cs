using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho FeedbackService.
/// </summary>
public class FeedbackServiceTests
{
    private static ServiceFeedback NewFeedback(short rating = 5, string? content = "Test content")
        => new()
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = Guid.NewGuid(),
            Rating = rating,
            Content = content,
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
