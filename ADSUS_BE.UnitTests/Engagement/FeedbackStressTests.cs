using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Adversarial stress tests for Feedback Relational Paged Query (Milestone 1).
/// Tests boundary ratings, injection vectors, null entity graphs, and exception safety.
/// </summary>
public class FeedbackStressTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FeedbackRepository _repo;
    private readonly FeedbackService _service;
    private readonly FeedbacksController _controller;
    private readonly Mock<IPatientProfileRepository> _patientProfilesMock = new();

    public FeedbackStressTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repo = new FeedbackRepository(_context);
        _service = new FeedbackService(_repo);
        _controller = new FeedbacksController(_service, _patientProfilesMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(User Doctor, User Patient, PatientProfile Profile, Case Case)> SeedBaseEntitiesAsync()
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Dr. Stress Feedback",
            Phone = "0900000001",
            Email = "doctor@stress.com",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
        };

        var patient = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Patient Stress Feedback",
            Phone = "0900000002",
            Email = "patient@stress.com",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
        };

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patient.UserId,
            User = patient,
            CreatedBy = patient.UserId,
        };

        var kase = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            PatientProfileId = profile.PatientProfileId,
        };

        _context.Users.AddRange(doctor, patient);
        _context.PatientProfiles.Add(profile);
        _context.Cases.Add(kase);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (doctor, patient, profile, kase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task GetPagedAsync_PageLessThanOrEqualToZero_ClampedToPageOne(int invalidPage)
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 5,
            Content = "Great service",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            rating: null,
            page: invalidPage,
            pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Page);
        Assert.Single(result.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task GetPagedAsync_PageSizeLessThanOrEqualToZero_ClampedToDefault15(int invalidPageSize)
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 5,
            Content = "Great service",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            rating: null,
            page: 1,
            pageSize: invalidPageSize,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(15, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_PageSizeExtreme9999_ClampedToDefault15()
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 5,
            Content = "Great service",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            rating: null,
            page: 1,
            pageSize: 9999,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(15, result.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondTotalPages_ReturnsEmptyItems()
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 5,
            Content = "Great service",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            rating: null,
            page: 99999,
            pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE \"ServiceFeedbacks\"; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("admin'--")]
    [InlineData("!@#$%^&*()_+-=[]{}|;':\",.<>/?")]
    [InlineData("🏥 ⭐⭐⭐⭐⭐")]
    public async Task GetPagedAsync_MaliciousKeyword_HandledSafelyWithoutCrash(string maliciousKeyword)
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 4,
            Content = "Normal content",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: maliciousKeyword,
            rating: null,
            page: 1,
            pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Theory]
    [InlineData(0, 1)] // 0 rating is treated as no-filter (returns all)
    [InlineData(-1, 1)] // negative rating is treated as no-filter (returns all)
    [InlineData(1, 0)] // valid boundary rating 1 -> none match 5
    [InlineData(5, 1)] // valid boundary rating 5 -> matches 1
    [InlineData(6, 0)] // out-of-range rating 6 -> 0 match
    [InlineData(99, 0)] // extreme rating 99 -> 0 match
    public async Task GetPagedAsync_RatingBoundaries_BehaveCorrectly(short ratingInput, int expectedMatchCount)
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 5,
            Content = "5-star care",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            rating: ratingInput,
            page: 1,
            pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(expectedMatchCount, result.TotalItems);
    }

    [Fact]
    public async Task GetPagedAsync_NullOptionalContent_DoesNotThrowNullReference()
    {
        var (doctor, patient, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 3,
            Content = null, // NULL Content
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetPagedAsync(
            keyword: null,
            rating: null,
            page: 1,
            pageSize: 15,
            ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        var item = Assert.Single(result.Items);
        Assert.Equal("Patient Stress Feedback", item.PatientName);
        Assert.Equal("0900000002", item.PatientPhone);
        Assert.Equal("Dr. Stress Feedback", item.DoctorName);
        Assert.Equal(doctor.UserId, item.DoctorId);
        Assert.Null(item.Content);
    }

    [Fact]
    public async Task GetAll_ControllerEndpoint_BoundaryParameters_ReturnsOk()
    {
        var (_, _, profile, kase) = await SeedBaseEntitiesAsync();
        var fb = new ServiceFeedback
        {
            FeedbackId = Guid.NewGuid(),
            CaseId = kase.CaseId,
            Case = kase,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Rating = 4,
            Content = "Controller test",
            SubmittedAt = DateTime.UtcNow,
        };
        _context.ServiceFeedbacks.Add(fb);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var actionResult = await _controller.GetAll(
            search: "<script>",
            keyword: null,
            rating: -1,
            minRating: null,
            page: 0,
            pageSize: 0,
            ct: TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var envelope = Assert.IsType<ApiResponse<PagedResult<FeedbackResponse>>>(okResult.Value);
        Assert.Equal(200, envelope.Code);
        Assert.NotNull(envelope.Data);
        Assert.Equal(1, envelope.Data.Page);
        Assert.Equal(15, envelope.Data.PageSize);
    }
}
