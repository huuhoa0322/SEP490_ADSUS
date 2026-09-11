using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// Group 4: 15 test cases for Auto-Trigger Logic
/// Covers 4.1.1 - 4.3.4 in scratch/test_cases.md
/// </summary>
public class AutoTriggerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static ConfirmAnalysisRequest MakeValidConfirmAnalysisRequest()
    {
        return new ConfirmAnalysisRequest
        {
            OriginalImageStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            OriginalImageFileName = "test.png",
            OriginalImageContentType = "image/png",
            BurntImageStream = new MemoryStream(new byte[] { 4, 5, 6 }),
            BurntImageFileName = "test_burnt.png",
            BurntImageContentType = "image/png",
            AiPredictionsJson = "[]",
            DoctorAnnotationsJson = "[]",
            Note = "Ultrasound note"
        };
    }

    #region 4.1 Case Creation Triggers GENERAL_EXAM (5 test cases)

    private static (User doctor, PatientProfile profile) CreateDoctorAndPatientProfile()
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "BS. Lê Minh Hoàng",
            Phone = "0913456789",
            PasswordHash = "hashed",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var patientUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Nguyễn Thị Hoa",
            Phone = "0981111001",
            PasswordHash = "hashed",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            DateOfBirth = new DateOnly(1992, 5, 14),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patientUser.UserId,
            User = patientUser,
            CreatedBy = doctor.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return (doctor, profile);
    }

    private static void SetupMockCases(Mock<ICaseRepository> mockCases, User doctor, PatientProfile profile)
    {
        mockCases.Setup(c => c.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken ct) => new Case
            {
                CaseId = id,
                DoctorId = doctor.UserId,
                PatientProfileId = profile.PatientProfileId,
                Doctor = doctor,
                PatientProfile = profile,
                Status = CaseStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UltrasoundImages = new List<UltrasoundImage>(),
                CaseSymptoms = new List<CaseSymptom>()
            });
    }

    [Fact]
    public async Task TC_4_1_1_Trigger_DoctorCreateCase_AutoAddsGeneralExam()
    {
        // Arrange
        using var context = CreateContext();
        var mockCases = new Mock<ICaseRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();
        var mockClinicService = new Mock<ICaseClinicServiceService>();

        var (doctor, profile) = CreateDoctorAndPatientProfile();
        SetupMockCases(mockCases, doctor, profile);

        mockUsers.Setup(u => u.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);
        mockProfiles.Setup(p => p.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            mockClinicService.Object,
            null,
            context);

        var request = new CreateCaseRequest(
            profile.PatientProfileId,
            doctor.UserId,
            "Khám định kỳ",
            null,
            new List<UploadedFile>());

        // Act
        await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        mockClinicService.Verify(s => s.AddServiceToCaseByCodeAsync(
            It.IsAny<Guid>(),
            "GENERAL_EXAM",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC_4_1_2_Trigger_PatientBooking_AutoAddsGeneralExam()
    {
        // Arrange
        using var context = CreateContext();
        var mockCases = new Mock<ICaseRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();
        var mockClinicService = new Mock<ICaseClinicServiceService>();

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            mockClinicService.Object,
            null,
            context);

        var patientProfileId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        // Act
        var caseId = await service.CreateFromBookingAsync(
            patientProfileId,
            doctorId,
            new DateOnly(2026, 9, 11),
            new List<SymptomInput>(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, caseId);
        mockClinicService.Verify(s => s.AddServiceToCaseByCodeAsync(
            caseId,
            "GENERAL_EXAM",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC_4_1_3_Trigger_GeneralExamInactive_GracefullySkipped()
    {
        // Arrange
        using var context = CreateContext();
        var mockCases = new Mock<ICaseRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();

        var (doctor, profile) = CreateDoctorAndPatientProfile();
        SetupMockCases(mockCases, doctor, profile);

        mockUsers.Setup(u => u.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);
        mockProfiles.Setup(p => p.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        // Seed inactive GENERAL_EXAM
        context.ClinicServices.Add(new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "GENERAL_EXAM",
            Name = "Khám thường",
            Price = 100000,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var realClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            realClinicService,
            null,
            context);

        var request = new CreateCaseRequest(profile.PatientProfileId, doctor.UserId, "Khám bệnh", null, new List<UploadedFile>());

        // Act (should not throw)
        var response = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        var services = await context.CaseClinicServices.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(services);
    }

    [Fact]
    public async Task TC_4_1_4_Trigger_GeneralExamMissing_GracefullySkipped()
    {
        // Arrange
        using var context = CreateContext();
        var mockCases = new Mock<ICaseRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();

        var (doctor, profile) = CreateDoctorAndPatientProfile();
        SetupMockCases(mockCases, doctor, profile);

        mockUsers.Setup(u => u.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);
        mockProfiles.Setup(p => p.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var realClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            realClinicService,
            null,
            context);

        var request = new CreateCaseRequest(profile.PatientProfileId, doctor.UserId, "Khám bệnh", null, new List<UploadedFile>());

        // Act - No seed for GENERAL_EXAM in DB
        var response = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        var services = await context.CaseClinicServices.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(services);
    }

    [Fact]
    public async Task TC_4_1_5_Trigger_GeneralExamFaultIsolation_CaseCreatedEvenIfAutoAddThrows()
    {
        // Arrange
        using var context = CreateContext();
        var mockCases = new Mock<ICaseRepository>();
        var mockProfiles = new Mock<IPatientProfileRepository>();
        var mockUsers = new Mock<IUserRepository>();
        var mockClinicService = new Mock<ICaseClinicServiceService>();

        var (doctor, profile) = CreateDoctorAndPatientProfile();
        SetupMockCases(mockCases, doctor, profile);

        mockUsers.Setup(u => u.GetByIdAsync(doctor.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(doctor);
        mockProfiles.Setup(p => p.GetByIdAsync(profile.PatientProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        // Setup mock to throw an exception
        mockClinicService
            .Setup(s => s.AddServiceToCaseByCodeAsync(It.IsAny<Guid>(), "GENERAL_EXAM", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection error in auto-add"));

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            mockProfiles.Object,
            mockUsers.Object,
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            mockClinicService.Object,
            null,
            context);

        var request = new CreateCaseRequest(profile.PatientProfileId, doctor.UserId, "Khám định kỳ", null, new List<UploadedFile>());

        // Act - should NOT throw because of fault isolation try-catch
        var response = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        mockCases.Verify(c => c.CreateWithImagesAsync(It.IsAny<Case>(), It.IsAny<IReadOnlyList<UltrasoundImage>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 4.2 ConfirmAnalysis Triggers ULTRASOUND_EXAM (7 test cases)

    private static (CaseDiagnosisService service, Case medicalCase, AppDbContext context) SetupDiagnosisService(
        AppDbContext context,
        ICaseClinicServiceService clinicServiceService)
    {
        var caseId = Guid.NewGuid();
        var medicalCase = new Case
        {
            CaseId = caseId,
            DoctorId = Guid.NewGuid(),
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Cases.Add(medicalCase);
        context.SaveChanges();

        var storageMock = new Mock<IFileStorageService>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var aiModelVersionRepoMock = new Mock<IAiModelVersionRepository>();
        var configMock = new Mock<IConfiguration>();

        var activeModel = new AiModelVersion
        {
            ModelVersionId = Guid.NewGuid(),
            VersionCode = "v1.0",
            Status = ModelVersionStatus.Active,
            RegisteredBy = Guid.NewGuid(),
            RegisteredAt = DateTime.UtcNow,
            HfRepoId = "repo/test",
            HfFilename = "model.pt"
        };
        aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(activeModel);

        var service = new CaseDiagnosisService(
            context,
            storageMock.Object,
            httpClientFactoryMock.Object,
            aiModelVersionRepoMock.Object,
            new UltrasoundImageRepository(context),
            new AiPredictionRepository(context),
            new DoctorAnnotationRepository(context),
            new CaseRepository(context),
            configMock.Object,
            NullLogger<CaseDiagnosisService>.Instance,
            clinicServiceService);

        return (service, medicalCase, context);
    }

    [Fact]
    public async Task TC_4_2_1_Trigger_ConfirmAnalysisSuccess_AutoAddsUltrasoundExam()
    {
        // Arrange
        using var context = CreateContext();
        var mockClinicService = new Mock<ICaseClinicServiceService>();
        var (service, medicalCase, _) = SetupDiagnosisService(context, mockClinicService.Object);

        var request = MakeValidConfirmAnalysisRequest();

        // Act
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, request, TestContext.Current.CancellationToken);

        // Assert
        mockClinicService.Verify(s => s.AddServiceToCaseByCodeAsync(
            medicalCase.CaseId,
            "ULTRASOUND_EXAM",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TC_4_2_2_Trigger_ConfirmAnalysis_MultipleCalls_IsIdempotent()
    {
        // Arrange
        using var context = CreateContext();
        var ultrasoundService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "ULTRASOUND_EXAM",
            Name = "Khám siêu âm",
            Price = 200000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(ultrasoundService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var realCaseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        var (service, medicalCase, _) = SetupDiagnosisService(context, realCaseClinicService);

        // Act - Call 3 times
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);

        // Assert
        var attached = await context.CaseClinicServices
            .Where(cs => cs.CaseId == medicalCase.CaseId && cs.ClinicServiceId == ultrasoundService.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(attached);
    }

    [Fact]
    public async Task TC_4_2_3_Trigger_UltrasoundExamInactive_GracefullySkipped()
    {
        // Arrange
        using var context = CreateContext();
        var ultrasoundService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "ULTRASOUND_EXAM",
            Name = "Khám siêu âm",
            Price = 200000,
            IsActive = false, // INACTIVE
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(ultrasoundService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var realCaseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        var (service, medicalCase, _) = SetupDiagnosisService(context, realCaseClinicService);

        // Act - should not throw
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);

        // Assert
        var attached = await context.CaseClinicServices
            .Where(cs => cs.CaseId == medicalCase.CaseId)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(attached);
    }

    [Fact]
    public async Task TC_4_2_4_Trigger_AnalysisFailure_AbortsBeforeAutoTrigger()
    {
        // Arrange
        using var context = CreateContext();
        var mockClinicService = new Mock<ICaseClinicServiceService>();

        var caseId = Guid.NewGuid();
        // Case is Booked (not checked-in), so EnsureCaseCheckedInAsync will throw
        context.Cases.Add(new Case
        {
            CaseId = caseId,
            DoctorId = Guid.NewGuid(),
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.Booked, // Invalid for analysis
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (service, _, _) = SetupDiagnosisService(context, mockClinicService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.ConfirmAnalysisAsync(caseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken));

        // Ensure auto-trigger was never called
        mockClinicService.Verify(s => s.AddServiceToCaseByCodeAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TC_4_2_5_Trigger_ConfirmAnalysisPostRx_AutoAppendsUltrasoundToPendingInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var ultrasoundService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "ULTRASOUND_EXAM",
            Name = "Khám siêu âm",
            Price = 200000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(ultrasoundService);

        var realCaseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        var (service, medicalCase, _) = SetupDiagnosisService(context, realCaseClinicService);

        // Pre-create PENDING invoice for this case (simulating pre-existing Rx)
        var pendingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(pendingInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Confirm analysis
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);

        // Assert - PENDING invoice total increased by 200k
        var updatedInvoice = await context.Invoices.FirstAsync(i => i.Id == pendingInvoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(250000, updatedInvoice.TotalAmount); // 50k + 200k

        var items = await context.InvoiceItems.Where(i => i.InvoiceId == pendingInvoice.Id).ToListAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single(items);
        Assert.Equal(InvoiceItemType.Service, item.ItemType);
        Assert.Equal(200000, item.TotalPrice);
    }

    [Fact]
    public async Task TC_4_2_6_Trigger_UltrasoundFaultIsolation_AnalysisReturnsOkEvenIfAutoAddThrows()
    {
        // Arrange
        using var context = CreateContext();
        var mockClinicService = new Mock<ICaseClinicServiceService>();
        mockClinicService
            .Setup(s => s.AddServiceToCaseByCodeAsync(It.IsAny<Guid>(), "ULTRASOUND_EXAM", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error during ultrasound service attachment"));

        var (service, medicalCase, _) = SetupDiagnosisService(context, mockClinicService.Object);

        // Act - should NOT throw because fault isolation catches and logs
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);

        // Assert - images were still committed
        var imageCount = await context.UltrasoundImages.CountAsync(img => img.CaseId == medicalCase.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(1, imageCount);
    }

    [Fact]
    public async Task TC_4_2_7_Trigger_UltrasoundRetrigger_AfterDoctorDeletesExam_ReattachesOnNextConfirm()
    {
        // Arrange
        using var context = CreateContext();
        var ultrasoundService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "ULTRASOUND_EXAM",
            Name = "Khám siêu âm",
            Price = 200000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(ultrasoundService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var realCaseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        var (service, medicalCase, _) = SetupDiagnosisService(context, realCaseClinicService);

        // Act 1: Initial confirm adds ULTRASOUND_EXAM
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);
        var firstAttached = await context.CaseClinicServices.FirstAsync(cs => cs.CaseId == medicalCase.CaseId, TestContext.Current.CancellationToken);
        Assert.NotNull(firstAttached);

        // Act 2: Doctor deletes the service
        await realCaseClinicService.RemoveServiceFromCaseAsync(medicalCase.CaseId, firstAttached.Id, TestContext.Current.CancellationToken);
        Assert.False(await context.CaseClinicServices.AnyAsync(cs => cs.CaseId == medicalCase.CaseId, TestContext.Current.CancellationToken));

        // Act 3: Re-confirming analysis re-attaches ULTRASOUND_EXAM
        await service.ConfirmAnalysisAsync(medicalCase.CaseId, MakeValidConfirmAnalysisRequest(), TestContext.Current.CancellationToken);

        // Assert
        var reattached = await context.CaseClinicServices.FirstOrDefaultAsync(cs => cs.CaseId == medicalCase.CaseId, TestContext.Current.CancellationToken);
        Assert.NotNull(reattached);
        Assert.Equal(ultrasoundService.Id, reattached.ClinicServiceId);
    }

    #endregion

    #region 4.3 Case END Without Prescription Triggers Invoice (4 test cases)

    [Fact]
    public async Task TC_4_3_1_Trigger_CaseEndWithoutRx_GeneratesInvoiceWhenServicesExist()
    {
        // Arrange
        using var context = CreateContext();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var medicalCase = new Case
        {
            CaseId = caseId,
            DoctorId = doctorId,
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Cases.Add(medicalCase);

        // Seed a service for this case
        context.CaseClinicServices.Add(new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            ClinicServiceId = Guid.NewGuid(),
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mockCases = new Mock<ICaseRepository>();
        mockCases.Setup(c => c.GetForUpdateAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);
        mockCases.Setup(c => c.GetDetailAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);

        var mockInvoiceService = new Mock<IInvoiceService>();

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            Mock.Of<IUserRepository>(),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            Mock.Of<ICaseClinicServiceService>(),
            mockInvoiceService.Object,
            context);

        // Act
        var result = await service.EndWithoutPrescriptionAsync(caseId, doctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CaseStatus.End, medicalCase.Status);
        mockInvoiceService.Verify(inv => inv.GenerateInvoiceForCaseAsync(caseId), Times.Once);
    }

    [Fact]
    public async Task TC_4_3_2_Trigger_CaseEnd_DuplicateInvoiceGuarded_WhenInvoiceAlreadyExists()
    {
        // Arrange
        using var context = CreateContext();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var medicalCase = new Case
        {
            CaseId = caseId,
            DoctorId = doctorId,
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Cases.Add(medicalCase);

        // Seed service and already existing PENDING invoice
        context.CaseClinicServices.Add(new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            ClinicServiceId = Guid.NewGuid(),
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        });
        context.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mockCases = new Mock<ICaseRepository>();
        mockCases.Setup(c => c.GetForUpdateAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);
        mockCases.Setup(c => c.GetDetailAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);

        var mockInvoiceService = new Mock<IInvoiceService>();

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            Mock.Of<IUserRepository>(),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            Mock.Of<ICaseClinicServiceService>(),
            mockInvoiceService.Object,
            context);

        // Act
        await service.EndWithoutPrescriptionAsync(caseId, doctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CaseStatus.End, medicalCase.Status);
        // Should NOT call GenerateInvoiceForCaseAsync because invoice already exists
        mockInvoiceService.Verify(inv => inv.GenerateInvoiceForCaseAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TC_4_3_3_Trigger_CaseEndEmptyCase_GuardsAgainstCallingGenerateInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var medicalCase = new Case
        {
            CaseId = caseId,
            DoctorId = doctorId,
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Cases.Add(medicalCase);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // No services, no prescription, no invoice

        var mockCases = new Mock<ICaseRepository>();
        mockCases.Setup(c => c.GetForUpdateAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);
        mockCases.Setup(c => c.GetDetailAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);

        var mockInvoiceService = new Mock<IInvoiceService>();

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            Mock.Of<IUserRepository>(),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            Mock.Of<ICaseClinicServiceService>(),
            mockInvoiceService.Object,
            context);

        // Act
        var result = await service.EndWithoutPrescriptionAsync(caseId, doctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CaseStatus.End, medicalCase.Status);
        mockInvoiceService.Verify(inv => inv.GenerateInvoiceForCaseAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TC_4_3_4_Trigger_CaseEndFaultIsolation_CaseEndsEvenIfInvoiceGenerationThrows()
    {
        // Arrange
        using var context = CreateContext();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var medicalCase = new Case
        {
            CaseId = caseId,
            DoctorId = doctorId,
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Cases.Add(medicalCase);

        // Seed a service
        context.CaseClinicServices.Add(new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            ClinicServiceId = Guid.NewGuid(),
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mockCases = new Mock<ICaseRepository>();
        mockCases.Setup(c => c.GetForUpdateAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);
        mockCases.Setup(c => c.GetDetailAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(medicalCase);

        var mockInvoiceService = new Mock<IInvoiceService>();
        mockInvoiceService
            .Setup(inv => inv.GenerateInvoiceForCaseAsync(caseId))
            .ThrowsAsync(new InvalidOperationException("DB error during invoice generation"));

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            Mock.Of<IUserRepository>(),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            Mock.Of<ICaseClinicServiceService>(),
            mockInvoiceService.Object,
            context);

        // Act - should NOT throw because of fault isolation try-catch
        var result = await service.EndWithoutPrescriptionAsync(caseId, doctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CaseStatus.End, medicalCase.Status);
    }

    #endregion
}
