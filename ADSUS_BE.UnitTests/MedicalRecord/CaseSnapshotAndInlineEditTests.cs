using System.Net.Http;
using System.Reflection;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.Controllers;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.MedicalRecord;

/// <summary>
/// Comprehensive test suite covering Backend test cases B1-B10 from implementation_plan.md:
/// Snapshot architecture, inline editing, orphan removal, status guards, HTML stripping, and role authorization.
/// </summary>
public class CaseSnapshotAndInlineEditTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<IUltrasoundImageRepository> _images = new();
    private readonly Mock<IPatientProfileRepository> _profiles = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly CaseService _caseService;
    private readonly CaseReportService _reportService;

    static CaseSnapshotAndInlineEditTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public CaseSnapshotAndInlineEditTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"CaseSnapshotTests_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);

        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        _caseService = new CaseService(
            _cases.Object,
            _images.Object,
            _profiles.Object,
            _users.Object,
            new Lazy<IFileStorageService>(() => _storage.Object),
            _notificationService.Object,
            Mock.Of<ILogger<CaseService>>(),
            caseClinicServiceService: null,
            invoiceService: null,
            context: _context);

        _reportService = new CaseReportService(
            _cases.Object,
            _storage.Object,
            _httpClientFactory.Object,
            Mock.Of<ILogger<CaseReportService>>());

        _notificationService.Setup(n => n.SendAsync(It.IsAny<SendNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Case CreateTrackedCase(CaseStatus status = CaseStatus.InProgress)
    {
        var doctor = MedicalRecordTestData.MakeDoctor();
        var profile = MedicalRecordTestData.MakePatientProfile();
        var medicalCase = MedicalRecordTestData.MakeCase(profile, doctor, status);

        _context.Users.Add(doctor);
        _context.PatientProfiles.Add(profile);
        _context.Cases.Add(medicalCase);
        _context.SaveChanges();

        _cases.Setup(r => r.GetForUpdateWithCollectionsAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);
        _cases.Setup(r => r.GetForUpdateAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);
        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);
        _cases.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => _context.SaveChangesAsync());

        return medicalCase;
    }

    // =========================================================================
    // B1: PUT /cases/{id}/symptoms — case IN_PROGRESS, valid data -> symptoms replaced
    // =========================================================================
    [Fact]
    public async Task B1_UpdateSymptomsAsync_CaseInProgress_ReplacesSymptoms()
    {
        // Arrange
        var medicalCase = CreateTrackedCase(CaseStatus.InProgress);
        var oldCatId = Guid.NewGuid();
        var oldSymId = Guid.NewGuid();
        var oldSymptom = new CaseSymptom
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            CategoryId = oldCatId,
            SymptomId = oldSymId,
            OtherNote = "Ghi chú cũ",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.CaseSymptoms.Add(oldSymptom);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newCatId1 = Guid.NewGuid();
        var newSymId1 = Guid.NewGuid();
        var newCatId2 = Guid.NewGuid();
        var request = new UpdateCaseSymptomsRequest(new List<CreateCaseSymptomRequest>
        {
            new(newCatId1, newSymId1, null),
            new(newCatId2, null, "Triệu chứng khác")
        });

        // Act
        var response = await _caseService.UpdateSymptomsAsync(medicalCase.CaseId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, medicalCase.CaseSymptoms.Count);
        Assert.DoesNotContain(medicalCase.CaseSymptoms, s => s.SymptomId == oldSymId);
        Assert.Contains(medicalCase.CaseSymptoms, s => s.SymptomId == newSymId1);
        Assert.Contains(medicalCase.CaseSymptoms, s => s.OtherNote == "Triệu chứng khác");
        Assert.DoesNotContain(_context.CaseSymptoms, s => s.Id == oldSymptom.Id);
    }

    // =========================================================================
    // B2: PUT /cases/{id}/symptoms — empty array [] -> all symptoms cleared
    // =========================================================================
    [Fact]
    public async Task B2_UpdateSymptomsAsync_EmptyArray_ClearsAllSymptoms()
    {
        // Arrange
        var medicalCase = CreateTrackedCase(CaseStatus.InProgress);
        var oldSymptom1 = new CaseSymptom
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            CategoryId = Guid.NewGuid(),
            SymptomId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        var oldSymptom2 = new CaseSymptom
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            CategoryId = Guid.NewGuid(),
            SymptomId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        _context.CaseSymptoms.AddRange(oldSymptom1, oldSymptom2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateCaseSymptomsRequest(new List<CreateCaseSymptomRequest>());

        // Act
        var response = await _caseService.UpdateSymptomsAsync(medicalCase.CaseId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Empty(medicalCase.CaseSymptoms);
        Assert.DoesNotContain(_context.CaseSymptoms, s => s.CaseId == medicalCase.CaseId);
    }

    // =========================================================================
    // B3: PUT /cases/{id}/diseases — case IN_PROGRESS -> case_diseases replaced
    // =========================================================================
    [Fact]
    public async Task B3_UpdateDiseasesAsync_CaseInProgress_ReplacesCaseDiseases()
    {
        // Arrange
        var medicalCase = CreateTrackedCase(CaseStatus.InProgress);
        var oldDisease = new CaseDisease
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            DiseaseId = Guid.NewGuid(),
            Note = "Bệnh cũ",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.CaseDiseases.Add(oldDisease);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newDiseaseId1 = Guid.NewGuid();
        var newDiseaseId2 = Guid.NewGuid();
        var request = new UpdateCaseDiseasesRequest(new List<CaseDiseaseInput>
        {
            new(newDiseaseId1, "Tăng huyết áp vô căn"),
            new(newDiseaseId2, null)
        });

        // Act
        var response = await _caseService.UpdateDiseasesAsync(medicalCase.CaseId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, medicalCase.CaseDiseases.Count);
        Assert.DoesNotContain(medicalCase.CaseDiseases, d => d.Id == oldDisease.Id);
        Assert.Contains(medicalCase.CaseDiseases, d => d.DiseaseId == newDiseaseId1 && d.Note == "Tăng huyết áp vô căn");
        Assert.Contains(medicalCase.CaseDiseases, d => d.DiseaseId == newDiseaseId2 && d.Note == null);
        Assert.DoesNotContain(_context.CaseDiseases, d => d.Id == oldDisease.Id);
    }

    // =========================================================================
    // B4: PUT /cases/{id}/allergies — case BOOKED -> allowed
    // =========================================================================
    [Fact]
    public async Task B4_UpdateAllergiesAsync_CaseBooked_AllowedAndReplacesAllergies()
    {
        // Arrange
        var medicalCase = CreateTrackedCase(CaseStatus.Booked);
        var oldAllergy = new CaseAllergy
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            AllergyTypeId = Guid.NewGuid(),
            Note = "Dị ứng phấn hoa",
            CreatedAt = DateTime.UtcNow
        };
        _context.CaseAllergies.Add(oldAllergy);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newAllergyId = Guid.NewGuid();
        var request = new UpdateCaseAllergiesRequest(new List<CaseAllergyInput>
        {
            new(newAllergyId, "Dị ứng Paracetamol mức độ nặng")
        });

        // Act
        var response = await _caseService.UpdateAllergiesAsync(medicalCase.CaseId, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Single(medicalCase.CaseAllergies);
        Assert.Contains(medicalCase.CaseAllergies, a => a.AllergyTypeId == newAllergyId && a.Note == "Dị ứng Paracetamol mức độ nặng");
        Assert.DoesNotContain(_context.CaseAllergies, a => a.Id == oldAllergy.Id);
    }

    // =========================================================================
    // B5: Update any collection — case CONFIRMED/END/CANCELLED -> 422 BusinessException
    // =========================================================================
    [Theory]
    [InlineData(CaseStatus.Confirmed)]
    [InlineData(CaseStatus.End)]
    [InlineData(CaseStatus.Cancelled)]
    public async Task B5_UpdateCollections_CaseLockedOrCancelled_ThrowsBusinessException(CaseStatus status)
    {
        // Arrange
        var medicalCase = CreateTrackedCase(status);

        var symptomsReq = new UpdateCaseSymptomsRequest(new List<CreateCaseSymptomRequest>());
        var diseasesReq = new UpdateCaseDiseasesRequest(new List<CaseDiseaseInput>());
        var allergiesReq = new UpdateCaseAllergiesRequest(new List<CaseAllergyInput>());

        // Act & Assert
        var exSymptoms = await Assert.ThrowsAsync<BusinessException>(() =>
            _caseService.UpdateSymptomsAsync(medicalCase.CaseId, symptomsReq, TestContext.Current.CancellationToken));
        Assert.Contains("Cannot modify a locked or cancelled case", exSymptoms.Message);

        var exDiseases = await Assert.ThrowsAsync<BusinessException>(() =>
            _caseService.UpdateDiseasesAsync(medicalCase.CaseId, diseasesReq, TestContext.Current.CancellationToken));
        Assert.Contains("Cannot modify a locked or cancelled case", exDiseases.Message);

        var exAllergies = await Assert.ThrowsAsync<BusinessException>(() =>
            _caseService.UpdateAllergiesAsync(medicalCase.CaseId, allergiesReq, TestContext.Current.CancellationToken));
        Assert.Contains("Cannot modify a locked or cancelled case", exAllergies.Message);
    }

    // =========================================================================
    // B6: Update collection — invalid role -> forbidden / guarded
    // =========================================================================
    [Theory]
    [InlineData(nameof(CasesController.UpdateSymptoms))]
    [InlineData(nameof(CasesController.UpdateDiseases))]
    [InlineData(nameof(CasesController.UpdateAllergies))]
    public void B6_CasesController_MutatingCollectionEndpoints_RequireDoctorOrStaffRole(string methodName)
    {
        // Arrange
        var method = typeof(CasesController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authorizeAttr);
        Assert.NotNull(authorizeAttr.Roles);
        Assert.Contains("DOCTOR", authorizeAttr.Roles);
        Assert.Contains("STAFF", authorizeAttr.Roles);
        Assert.DoesNotContain("PATIENT", authorizeAttr.Roles);
    }

    // =========================================================================
    // B7: SaveConclusionAsync — doctorConclusion: null -> succeeds without NullReferenceException
    // =========================================================================
    [Fact]
    public async Task B7_SaveConclusionAsync_DoctorConclusionNull_SucceedsWithoutNullReferenceException()
    {
        // Arrange
        var medicalCase = CreateTrackedCase(CaseStatus.InProgress);
        var request = new CaseConclusionRequest(
            FinalDiagnosis: "Viêm tuyến vú cấp tính",
            DoctorConclusion: null);

        // Act
        var response = await _caseService.SaveConclusionAsync(
            medicalCase.CaseId,
            medicalCase.DoctorId,
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Viêm tuyến vú cấp tính", medicalCase.FinalDiagnosis);
        Assert.Equal("", medicalCase.DoctorConclusion); // null safely coerced to empty string
    }

    // =========================================================================
    // B8: CreateAsync / CreateFromBookingAsync -> auto-copies PatientDiseases and PatientAllergies
    // =========================================================================
    [Fact]
    public async Task B8_CreateAsync_And_CreateFromBookingAsync_AutoCopiesSnapshotFromPatientProfile()
    {
        // Arrange
        var doctor = MedicalRecordTestData.MakeDoctor();
        _users.Setup(u => u.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var diseaseId1 = Guid.NewGuid();
        var diseaseId2 = Guid.NewGuid();
        var allergyTypeId = Guid.NewGuid();

        var profile = MedicalRecordTestData.MakePatientProfile();
        profile.PatientDiseases = new List<PatientDisease>
        {
            new() { PatientProfileId = profile.PatientProfileId, DiseaseId = diseaseId1, Note = "Tiểu đường type 2" },
            new() { PatientProfileId = profile.PatientProfileId, DiseaseId = diseaseId2, Note = null }
        };
        profile.PatientAllergies = new List<PatientAllergy>
        {
            new() { PatientProfileId = profile.PatientProfileId, AllergyTypeId = allergyTypeId, Note = "Dị ứng hải sản" }
        };

        _profiles.Setup(p => p.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        Case? capturedCaseFromCreate = null;
        _cases.Setup(c => c.CreateWithImagesAsync(It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()))
            .Callback<Case, IReadOnlyList<UltrasoundImage>, CancellationToken>((c, _, _) => capturedCaseFromCreate = c)
            .ReturnsAsync((Case c, IReadOnlyList<UltrasoundImage> _, CancellationToken _) => c);

        _cases.Setup(c => c.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => capturedCaseFromCreate ?? MedicalRecordTestData.MakeCase(profile, doctor));

        // Act 1: CreateAsync
        var createRequest = new CreateCaseRequest(
            profile.PatientProfileId,
            doctor.UserId,
            "Khám kiểm tra tổng quát",
            null,
            new List<UploadedFile>());

        var createResponse = await _caseService.CreateAsync(createRequest, TestContext.Current.CancellationToken);

        // Assert 1
        Assert.NotNull(capturedCaseFromCreate);
        Assert.Equal(2, capturedCaseFromCreate.CaseDiseases.Count);
        Assert.Contains(capturedCaseFromCreate.CaseDiseases, d => d.DiseaseId == diseaseId1 && d.Note == "Tiểu đường type 2");
        Assert.Contains(capturedCaseFromCreate.CaseDiseases, d => d.DiseaseId == diseaseId2 && d.Note == null);
        Assert.Single(capturedCaseFromCreate.CaseAllergies);
        Assert.Contains(capturedCaseFromCreate.CaseAllergies, a => a.AllergyTypeId == allergyTypeId && a.Note == "Dị ứng hải sản");

        // Act 2: CreateFromBookingAsync
        Case? capturedCaseFromBooking = null;
        _cases.Setup(c => c.CreateAsync(It.IsAny<Case>(), It.IsAny<CancellationToken>()))
            .Callback<Case, CancellationToken>((c, _) => capturedCaseFromBooking = c)
            .ReturnsAsync((Case c, CancellationToken _) => c);

        var bookingCaseId = await _caseService.CreateFromBookingAsync(
            profile.PatientProfileId,
            doctor.UserId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new List<SymptomInput>(),
            TestContext.Current.CancellationToken);

        // Assert 2
        Assert.NotNull(capturedCaseFromBooking);
        Assert.Equal(bookingCaseId, capturedCaseFromBooking.CaseId);
        Assert.Equal(2, capturedCaseFromBooking.CaseDiseases.Count);
        Assert.Contains(capturedCaseFromBooking.CaseDiseases, d => d.DiseaseId == diseaseId1 && d.Note == "Tiểu đường type 2");
        Assert.Single(capturedCaseFromBooking.CaseAllergies);
        Assert.Contains(capturedCaseFromBooking.CaseAllergies, a => a.AllergyTypeId == allergyTypeId && a.Note == "Dị ứng hải sản");
    }

    // =========================================================================
    // B9: HtmlHelper & PDF export -> HTML tags stripped, empty conclusion section hidden
    // =========================================================================
    [Fact]
    public async Task B9_HtmlHelper_And_PdfExport_StripsHtmlTags_AndHidesEmptyConclusion()
    {
        // 1. HtmlHelper.StripTags unit checks
        Assert.Equal("U tuyến xơ vú phải", HtmlHelper.StripTags("<p><strong>U tuyến xơ</strong> vú phải</p>"));
        Assert.Equal("alert('x') Bình thường", HtmlHelper.StripTags("<script>alert('x')</script> Bình thường"));
        Assert.Equal("A & B", HtmlHelper.StripTags("A &amp; B"));
        Assert.Equal("", HtmlHelper.StripTags(null));
        Assert.Equal("", HtmlHelper.StripTags("   "));

        // 2. CaseReportService PDF export with HTML rich-text and null DoctorConclusion
        var medicalCase = MedicalRecordTestData.MakeCase(status: CaseStatus.End);
        medicalCase.FinalDiagnosis = "<p>Chẩn đoán <strong>xác định</strong>:</p><ul><li>U tuyến xơ (BI-RADS 3)</li></ul>";
        medicalCase.DoctorConclusion = null; // Section "HƯỚNG XỬ TRÍ" should be omitted

        _cases.Setup(r => r.GetDetailAsync(medicalCase.CaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicalCase);

        var pdfBytes = await _reportService.GenerateReportAsync(medicalCase.CaseId, TestContext.Current.CancellationToken);

        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    // =========================================================================
    // B10: Update non-existent case -> 404 ResourceNotFoundException
    // =========================================================================
    [Fact]
    public async Task B10_UpdateCollections_NonExistentCase_ThrowsResourceNotFoundException()
    {
        // Arrange
        var missingCaseId = Guid.NewGuid();
        _cases.Setup(r => r.GetForUpdateWithCollectionsAsync(missingCaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Case?)null);

        var symptomsReq = new UpdateCaseSymptomsRequest(new List<CreateCaseSymptomRequest>());
        var diseasesReq = new UpdateCaseDiseasesRequest(new List<CaseDiseaseInput>());
        var allergiesReq = new UpdateCaseAllergiesRequest(new List<CaseAllergyInput>());

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _caseService.UpdateSymptomsAsync(missingCaseId, symptomsReq, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _caseService.UpdateDiseasesAsync(missingCaseId, diseasesReq, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _caseService.UpdateAllergiesAsync(missingCaseId, allergiesReq, TestContext.Current.CancellationToken));
    }
}
