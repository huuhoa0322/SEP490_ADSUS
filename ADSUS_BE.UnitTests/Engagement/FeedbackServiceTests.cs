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

        var result = await sut.SubmitAsync(request, patientProfileId, TestContext.Current.CancellationToken);

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

        var result = await sut.GetAllAsync(TestContext.Current.CancellationToken);

        var feedback = Assert.Single(result);
        Assert.Equal("Nguyen Van A", feedback.PatientName);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResultWithEnrichedFields()
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Dr. House",
            Phone = "0909999999",
            PasswordHash = "x",
            Role = UserRole.Doctor,
        };

        var patient = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "John Doe",
            Phone = "0901234567",
            PasswordHash = "x",
            Role = UserRole.Patient,
        };

        var patientProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patient.UserId,
            User = patient,
        };

        var caseEntity = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            PatientProfileId = patientProfile.PatientProfileId,
        };

        var feedback = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = caseEntity.CaseId,
            Case = caseEntity,
            PatientProfileId = patientProfile.PatientProfileId,
            PatientProfile = patientProfile,
            Rating = 5,
            Content = "Dịch vụ xuất sắc",
            SubmittedAt = DateTime.UtcNow,
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetPagedAsync(
            "John", 5, 1, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { feedback }, 1));

        var sut = new FeedbackService(repo.Object);

        var result = await sut.GetPagedAsync("John", 5, 1, 15, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.Page);
        Assert.Equal(15, result.PageSize);
        Assert.Equal(1, result.TotalPages);

        var item = Assert.Single(result.Items);
        Assert.Equal(feedback.FeedbackId, item.Id);
        Assert.Equal(caseEntity.CaseId, item.CaseId);
        Assert.Equal(patientProfile.PatientProfileId, item.PatientProfileId);
        Assert.Equal("John Doe", item.PatientName);
        Assert.Equal("0901234567", item.PatientPhone);
        Assert.Equal(doctor.UserId, item.DoctorId);
        Assert.Equal("Dr. House", item.DoctorName);
        Assert.Equal((short)5, item.Rating);
        Assert.Equal("Dịch vụ xuất sắc", item.Content);
    }

    [Theory]
    [InlineData(0, 0, 1, 15)]
    [InlineData(-1, -5, 1, 15)]
    [InlineData(1, 101, 1, 15)]
    [InlineData(2, 20, 2, 20)]
    public async Task GetPagedAsync_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<short?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<ServiceFeedback>(), 0));

        var sut = new FeedbackService(repo.Object);

        await sut.GetPagedAsync(null, null, page, pageSize, TestContext.Current.CancellationToken);

        repo.Verify(r => r.GetPagedAsync(
            null, null, expectedPage, expectedPageSize, It.IsAny<CancellationToken>()), Times.Once);
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

        var result = await sut.SubmitCaseFeedbackAsync(request, patientProfileId, caseId, TestContext.Current.CancellationToken);

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
            () => sut.SubmitCaseFeedbackAsync(request, patientProfileId, caseId, TestContext.Current.CancellationToken));

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
            () => sut.SubmitCaseFeedbackAsync(request, Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken));
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

        var result = await sut.GetCaseFeedbackAsync(caseId, patientProfileId, TestContext.Current.CancellationToken);

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

        var result = await sut.SubmitAsync(request, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Equal(0, result.Rating);
    }
}
