using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho FeedbackRepository (FT-37: GetByCaseIdAsync).
/// </summary>
public class FeedbackRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static ServiceFeedback NewFeedback(
        Guid feedbackId,
        short rating = 5,
        string? content = null,
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
    public async Task GetByCaseIdAsync_ExistingCase_ReturnsFeedback()
    {
        using var ctx = CreateContext();
        var patientProfile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            User = new User
            {
                FullName = "Test Patient",
                Phone = "0911111111",
                PasswordHash = "x",
            },
        };
        ctx.PatientProfiles.Add(patientProfile);

        var caseId = Guid.NewGuid();
        var feedback = NewFeedback(
            Guid.NewGuid(),
            rating: 5,
            content: "Bác sĩ rất tận tâm",
            patientProfileId: patientProfile.PatientProfileId,
            caseId: caseId);
        ctx.ServiceFeedbacks.Add(feedback);
        await ctx.SaveChangesAsync();

        var sut = new FeedbackRepository(ctx);

        var result = await sut.GetByCaseIdAsync(caseId);

        Assert.NotNull(result);
        Assert.Equal(caseId, result.CaseId);
        Assert.Equal(5, result.Rating);
        Assert.NotNull(result.PatientProfile);
    }

    [Fact]
    public async Task GetByCaseIdAsync_NoFeedback_ReturnsNull()
    {
        using var ctx = CreateContext();
        var sut = new FeedbackRepository(ctx);

        var result = await sut.GetByCaseIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
