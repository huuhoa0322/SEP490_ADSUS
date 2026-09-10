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
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new FeedbackRepository(ctx);

        var result = await sut.GetByCaseIdAsync(caseId, TestContext.Current.CancellationToken);

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

        var result = await sut.GetByCaseIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPagedAsync_WithDeepIncludes_LoadsPatientUserAndCaseDoctor()
    {
        using var ctx = CreateContext();

        var doctorUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Dr. Stephen Strange",
            Phone = "0901111111",
            PasswordHash = "x",
            Role = UserRole.Doctor,
        };

        var patientUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Peter Parker",
            Phone = "0902222222",
            PasswordHash = "x",
            Role = UserRole.Patient,
        };

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUser.UserId,
            User = patientUser,
        };

        var caseEntity = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = doctorUser.UserId,
            Doctor = doctorUser,
            PatientProfileId = profile.PatientProfileId,
        };

        var feedback = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = caseEntity.CaseId,
            Case = caseEntity,
            Rating = 5,
            Content = "Rất tuyệt vời",
            SubmittedAt = DateTime.UtcNow,
        };

        ctx.Users.AddRange(doctorUser, patientUser);
        ctx.PatientProfiles.Add(profile);
        ctx.Cases.Add(caseEntity);
        ctx.ServiceFeedbacks.Add(feedback);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new FeedbackRepository(ctx);

        // Act
        var (items, total) = await sut.GetPagedAsync(null, null, 1, 15, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.NotNull(item.PatientProfile);
        Assert.NotNull(item.PatientProfile.User);
        Assert.Equal("Peter Parker", item.PatientProfile.User.FullName);
        Assert.NotNull(item.Case);
        Assert.NotNull(item.Case.Doctor);
        Assert.Equal("Dr. Stephen Strange", item.Case.Doctor.FullName);
    }

    [Fact]
    public async Task GetPagedAsync_KeywordSearch_MatchesPatientDoctorCase()
    {
        using var ctx = CreateContext();

        var doctor1 = new User { UserId = Guid.NewGuid(), FullName = "Dr. Alice", Phone = "0111", PasswordHash = "x", Role = UserRole.Doctor };
        var doctor2 = new User { UserId = Guid.NewGuid(), FullName = "Dr. Bob", Phone = "0222", PasswordHash = "x", Role = UserRole.Doctor };

        var patient1 = new User { UserId = Guid.NewGuid(), FullName = "Charlie Brown", Phone = "0333", PasswordHash = "x", Role = UserRole.Patient };
        var patient2 = new User { UserId = Guid.NewGuid(), FullName = "Diana Prince", Phone = "0444", PasswordHash = "x", Role = UserRole.Patient };

        var profile1 = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient1.UserId, User = patient1 };
        var profile2 = new PatientProfile { PatientProfileId = Guid.NewGuid(), UserId = patient2.UserId, User = patient2 };

        var case1 = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor1.UserId, Doctor = doctor1, PatientProfileId = profile1.PatientProfileId };
        var case2 = new Case { CaseId = Guid.NewGuid(), DoctorId = doctor2.UserId, Doctor = doctor2, PatientProfileId = profile2.PatientProfileId };

        var fb1 = new ServiceFeedback { FeedbackId = Guid.NewGuid(), PatientProfileId = profile1.PatientProfileId, PatientProfile = profile1, CaseId = case1.CaseId, Case = case1, Rating = 5, Content = "Great care", SubmittedAt = DateTime.UtcNow.AddMinutes(-5) };
        var fb2 = new ServiceFeedback { FeedbackId = Guid.NewGuid(), PatientProfileId = profile2.PatientProfileId, PatientProfile = profile2, CaseId = case2.CaseId, Case = case2, Rating = 4, Content = "Good doctor", SubmittedAt = DateTime.UtcNow };

        ctx.Users.AddRange(doctor1, doctor2, patient1, patient2);
        ctx.PatientProfiles.AddRange(profile1, profile2);
        ctx.Cases.AddRange(case1, case2);
        ctx.ServiceFeedbacks.AddRange(fb1, fb2);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new FeedbackRepository(ctx);

        // Act 1: Search by patient name "Charlie"
        var (resPatient, totalPatient) = await sut.GetPagedAsync("Charlie", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalPatient);
        Assert.Equal(fb1.FeedbackId, resPatient[0].FeedbackId);

        // Act 2: Search by doctor name "Bob"
        var (resDoctor, totalDoctor) = await sut.GetPagedAsync("Bob", null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalDoctor);
        Assert.Equal(fb2.FeedbackId, resDoctor[0].FeedbackId);

        // Act 3: Search by case ID
        var (resCase, totalCase) = await sut.GetPagedAsync(case1.CaseId.ToString(), null, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalCase);
        Assert.Equal(fb1.FeedbackId, resCase[0].FeedbackId);
    }

    private static ServiceFeedback CreateFeedbackWithRelations(
        AppDbContext ctx,
        short rating = 5,
        string? content = null,
        DateTime? submittedAt = null)
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Doctor Test",
            Phone = "0900000000",
            PasswordHash = "x",
            Role = UserRole.Doctor,
        };
        var patient = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Patient Test",
            Phone = "0900000001",
            PasswordHash = "x",
            Role = UserRole.Patient,
        };
        var profile = new PatientProfile
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
            PatientProfileId = profile.PatientProfileId,
        };
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            CaseId = caseEntity.CaseId,
            Case = caseEntity,
            Rating = rating,
            Content = content ?? "Sample content",
            SubmittedAt = submittedAt ?? DateTime.UtcNow,
        };
        ctx.Users.AddRange(doctor, patient);
        ctx.PatientProfiles.Add(profile);
        ctx.Cases.Add(caseEntity);
        ctx.ServiceFeedbacks.Add(fb);
        return fb;
    }

    [Fact]
    public async Task GetPagedAsync_RatingFilter_FiltersByRating()
    {
        using var ctx = CreateContext();

        var fb1 = CreateFeedbackWithRelations(ctx, rating: 5);
        var fb2 = CreateFeedbackWithRelations(ctx, rating: 3);

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new FeedbackRepository(ctx);

        // Act: filter for rating 5
        var (items5, total5) = await sut.GetPagedAsync(null, 5, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total5);
        Assert.Equal(fb1.FeedbackId, items5[0].FeedbackId);

        // Act: filter for rating 3
        var (items3, total3) = await sut.GetPagedAsync(null, 3, 1, 15, TestContext.Current.CancellationToken);
        Assert.Equal(1, total3);
        Assert.Equal(fb2.FeedbackId, items3[0].FeedbackId);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsCorrectSlice()
    {
        using var ctx = CreateContext();

        var baseTime = DateTime.UtcNow.AddHours(-30);
        for (int i = 0; i < 25; i++)
        {
            CreateFeedbackWithRelations(ctx, rating: 5, content: $"Feedback {i}", submittedAt: baseTime.AddHours(i));
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new FeedbackRepository(ctx);

        // Act: Page 1, Size 10
        var (itemsPage1, total1) = await sut.GetPagedAsync(null, null, 1, 10, TestContext.Current.CancellationToken);
        Assert.Equal(25, total1);
        Assert.Equal(10, itemsPage1.Count);
        Assert.Equal("Feedback 24", itemsPage1[0].Content); // most recent first

        // Act: Page 3, Size 10 (remaining 5)
        var (itemsPage3, total3) = await sut.GetPagedAsync(null, null, 3, 10, TestContext.Current.CancellationToken);
        Assert.Equal(25, total3);
        Assert.Equal(5, itemsPage3.Count);
    }
}
