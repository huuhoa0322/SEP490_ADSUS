using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ADSUS_BE.UnitTests.Engagement;

/// <summary>
/// Tests cho ChatDataAggregator — RAG data aggregator cho Module 10 Chat (FT-39).
///
/// Test strategy:
/// - allergies/diseases: InMemory EF Core (direct AppDbContext queries)
/// - prescriptions/intakes/appointments/cases/healthLogs/blogs: Mock repositories
///
/// Phase 2: Intent detection → selective query. Tests verify correct sources are queried.
/// Per CLAUDE.md testing.md: TDD bắt buộc, mỗi test 1 assertion chính.
/// </summary>
public class ChatDataAggregatorTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ChatDataAggregator _sut;

    // Helper: build IntentResult from intent enum only (DataSource.None).
    private static IntentResult Ir(ChatIntent intent)
        => new() { Intent = intent, TriggeredSources = DataSource.None };

    // Helper: build IntentResult with specific sources.
    private static IntentResult Ir(ChatIntent intent, DataSource sources)
        => new() { Intent = intent, TriggeredSources = sources };

    public ChatDataAggregatorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        var prescriptionRepo = new Mock<IPrescriptionRepository>();
        var intakeLogRepo = new Mock<IMedicationIntakeLogRepository>();
        var appointmentRepo = new Mock<IAppointmentRepository>();
        var caseRepo = new Mock<ICaseRepository>();
        var healthLogRepo = new Mock<IHealthLogRepository>();
        var blogPostRepo = new Mock<IBlogPostRepository>();

        // Wire default empty returns
        prescriptionRepo.Setup(r => r.ListByPatientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Prescription>());
        intakeLogRepo.Setup(r => r.ListUpcomingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MedicationIntakeLog>());
        appointmentRepo.Setup(r => r.ListByPatientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        caseRepo.Setup(r => r.SearchByPatientAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<CaseStatus>?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Case>(), 0));
        healthLogRepo.Setup(r => r.GetLatestByPatientAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HealthLog>());
        blogPostRepo.Setup(r => r.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlogPost>());

        _sut = new ChatDataAggregator(
            _db,
            prescriptionRepo.Object,
            intakeLogRepo.Object,
            appointmentRepo.Object,
            caseRepo.Object,
            healthLogRepo.Object,
            blogPostRepo.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Setup helpers ────────────────────────────────────────────────────────

    private User NewUser(string fullName = "Nguyễn Văn A", DateOnly? dob = null)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = fullName,
            DateOfBirth = dob,
            Phone = "0900000000",
            PasswordHash = "dummy-hash-for-test",
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        return user;
    }

    private PatientProfile NewPatientProfile(User user)
    {
        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = user.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.PatientProfiles.Add(profile);
        _db.SaveChanges();
        return profile;
    }

    private void NewAllergy(PatientProfile profile, string allergyName)
    {
        var allergyType = new MedicalAllergyType { Id = Guid.NewGuid(), Name = allergyName };
        _db.MedicalAllergyTypes.Add(allergyType);
        _db.PatientAllergies.Add(new PatientAllergy
        {
            Id = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            AllergyTypeId = allergyType.Id,
            AllergyType = allergyType,
        });
        _db.SaveChanges();
    }

    private void NewDisease(PatientProfile profile, string diseaseName)
    {
        var disease = new MedicalDisease { Id = Guid.NewGuid(), Name = diseaseName };
        _db.MedicalDiseases.Add(disease);
        _db.PatientDiseases.Add(new PatientDisease
        {
            Id = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            DiseaseId = disease.Id,
            Disease = disease,
        });
        _db.SaveChanges();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_UnknownUserId_ReturnsNull()
    {
        var result = await _sut.BuildContextAsync(Guid.NewGuid(), Ir(ChatIntent.General));
        Assert.Null(result);
    }

    [Fact]
    public async Task BuildContextAsync_KnownUser_ReturnsBasicInfoWithAge()
    {
        // Arrange
        var user = NewUser("Trần Thị B", new DateOnly(1992, 5, 15));
        NewPatientProfile(user);

        // Act
        var result = await _sut.BuildContextAsync(user.UserId, Ir(ChatIntent.General));

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.BasicInfo);
        Assert.Equal("Trần Thị B", result.BasicInfo.FullName);
        Assert.Equal(new DateOnly(1992, 5, 15), result.BasicInfo.DateOfBirth);
        // Age: 2026 - 1992 = 34; birthday May 15 has passed in 2026 (before Aug 27) → 34
        Assert.Equal(34, result.BasicInfo.Age);
    }

    [Fact]
    public async Task BuildContextAsync_NoDob_ReturnsNullAge()
    {
        var user = NewUser("Lê Văn C", dob: null);
        NewPatientProfile(user);

        var result = await _sut.BuildContextAsync(user.UserId, Ir(ChatIntent.General));

        Assert.NotNull(result);
        Assert.Null(result!.BasicInfo!.Age);
    }

    [Fact]
    public async Task BuildContextAsync_WithAllergies_ReturnsAllergySection()
    {
        var user = NewUser();
        var profile = NewPatientProfile(user);
        NewAllergy(profile, "Penicillin");
        NewAllergy(profile, "Hải sản");

        var result = await _sut.BuildContextAsync(
            user.UserId,
            Ir(ChatIntent.Allergy, DataSource.Allergies));

        Assert.NotNull(result);
        Assert.NotNull(result!.Allergies);
        Assert.Equal(2, result.Allergies.Count);
        Assert.Contains(result.Allergies, a => a.AllergyTypeName == "Penicillin");
        Assert.Contains(result.Allergies, a => a.AllergyTypeName == "Hải sản");
    }

    [Fact]
    public async Task BuildContextAsync_WithDiseases_ReturnsDiseaseSection()
    {
        var user = NewUser();
        var profile = NewPatientProfile(user);
        NewDisease(profile, "Tiểu đường type 2");
        NewDisease(profile, "Tăng huyết áp");

        var result = await _sut.BuildContextAsync(
            user.UserId,
            Ir(ChatIntent.Disease, DataSource.Diseases));

        Assert.NotNull(result);
        Assert.NotNull(result!.Diseases);
        Assert.Equal(2, result.Diseases.Count);
        Assert.Contains(result.Diseases, d => d.DiseaseName == "Tiểu đường type 2");
    }

    [Fact]
    public async Task BuildContextAsync_NoAllergiesOrDiseases_ReturnsEmptyLists()
    {
        var user = NewUser();
        NewPatientProfile(user);

        var result = await _sut.BuildContextAsync(user.UserId, Ir(ChatIntent.General));

        Assert.NotNull(result);
        // Selective query: Allergies/Diseases not triggered → null (not queried)
        Assert.Null(result!.Allergies);
        Assert.Null(result.Diseases);
    }

    [Fact]
    public async Task BuildContextAsync_GeneralIntent_ReturnsOnlyBasicInfo()
    {
        var user = NewUser();
        NewPatientProfile(user);

        // General intent → DataSource.None → only BasicInfo populated
        var result = await _sut.BuildContextAsync(user.UserId, Ir(ChatIntent.General));

        Assert.NotNull(result);
        Assert.NotNull(result!.BasicInfo);
        // Selective: only BasicInfo is guaranteed non-null; others depend on TriggeredSources
        Assert.Null(result.ActivePrescriptions);
        Assert.Null(result.TodayIntakes);
        Assert.Null(result.UpcomingAppointments);
        Assert.Null(result.RecentCases);
        Assert.Null(result.Allergies);
        Assert.Null(result.Diseases);
        Assert.Null(result.RecentHealthLogs);
        Assert.Null(result.RecentBlogs);
    }

    // ── Phase 2: Selective query tests ───────────────────────────────────────

    [Fact]
    public async Task BuildContextAsync_AllergiesOnly_DoesNotQueryPrescriptions()
    {
        var user = NewUser();
        var profile = NewPatientProfile(user);
        NewAllergy(profile, "Penicillin");

        // Request only Allergies source
        var result = await _sut.BuildContextAsync(
            user.UserId,
            Ir(ChatIntent.Allergy, DataSource.Allergies));

        Assert.NotNull(result!.Allergies);
        Assert.Single(result.Allergies);
        // Prescription source not triggered → null (not queried)
        Assert.Null(result.ActivePrescriptions);
    }
}
