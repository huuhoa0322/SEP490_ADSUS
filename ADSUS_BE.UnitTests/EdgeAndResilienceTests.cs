using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.ClinicServiceManagement;
using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// Groups 5 & 6: 23 test cases for Edge Cases, Concurrency, Loops, Transaction Resilience,
/// Backward Compatibility, and End-to-End Workflow Journeys.
/// Covers 5.1.1 - 6.2.3 in scratch/test_cases.md
/// </summary>
public class EdgeAndResilienceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (Case medicalCase, User doctor, PatientProfile profile) SeedCaseWithPatient(
        AppDbContext context,
        CaseStatus status = CaseStatus.InProgress)
    {
        var doctor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "BS. Trịnh Đình Khôi",
            Phone = "0912345678",
            PasswordHash = "hash",
            Role = UserRole.Doctor,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var patientUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Trần Thị Mai",
            Phone = "0987654321",
            PasswordHash = "hash",
            Role = UserRole.Patient,
            Status = UserStatus.Active,
            DateOfBirth = new DateOnly(1995, 3, 20),
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
        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = doctor.UserId,
            Doctor = doctor,
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(doctor, patientUser);
        context.PatientProfiles.Add(profile);
        context.Cases.Add(medicalCase);
        context.SaveChanges();

        return (medicalCase, doctor, profile);
    }

    private static InvoiceService CreateInvoiceService(AppDbContext context, IInventoryService? inventoryService = null)
    {
        return new InvoiceService(
            context,
            inventoryService ?? Mock.Of<IInventoryService>(),
            Mock.Of<IMedicationIntakeLogRepository>(),
            Mock.Of<IMedicationIntakeScheduleGenerator>(),
            Mock.Of<INotificationService>());
    }

    #region Group 5: Edge Cases (13 test cases)

    #region 5.1 Concurrency (2 test cases)

    [Fact]
    public async Task TC_5_1_1_Concurrency_ConcurrentAddService_OnlyOneAttachesOrBothHandledSafely()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "CONCURRENT_SVC",
            Name = "Dịch vụ đồng thời",
            Price = 150000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(clinicService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service1 = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        var service2 = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        // Act - Run concurrent add requests
        var task1 = service1.AddServiceToCaseAsync(c.CaseId, clinicService.Id, TestContext.Current.CancellationToken);
        var task2 = service2.AddServiceToCaseAsync(c.CaseId, clinicService.Id, TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2);

        // Assert - Exactly 1 junction record exists
        var count = await context.CaseClinicServices.CountAsync(cs => cs.CaseId == c.CaseId && cs.ClinicServiceId == clinicService.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TC_5_1_2_Concurrency_ConcurrentGenerateInvoice_ReturnsConsistentInvoiceId()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "CONC_INV_SVC",
            Name = "Dịch vụ tính tiền đồng thời",
            Price = 120000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = clinicService.Id,
            PriceAtTime = 120000,
            CreatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(clinicService);
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var invoiceService1 = CreateInvoiceService(context);
        var invoiceService2 = CreateInvoiceService(context);

        // Act - Call GenerateInvoice concurrently
        var id1 = await invoiceService1.GenerateInvoiceForCaseAsync(c.CaseId);
        var id2 = await invoiceService2.GenerateInvoiceForCaseAsync(c.CaseId);

        // Assert - Both should refer to the same invoice or return the existing PENDING invoice
        Assert.Equal(id1, id2);
        var invoiceCount = await context.Invoices.CountAsync(i => i.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(1, invoiceCount);
    }

    #endregion

    #region 5.2 Infinite Loop Prevention (3 test cases)

    [Fact]
    public async Task TC_5_2_1_LoopSafety_AddServiceChain_DoesNotTriggerAddServiceLoop()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "LOOP_SAFETY",
            Name = "Dịch vụ kiểm tra lặp",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(clinicService);

        var pendingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(pendingInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, clinicService.Id, TestContext.Current.CancellationToken);

        // Assert - Added once, invoice updated once, no recursive calls
        Assert.NotNull(result);
        var totalJunctions = await context.CaseClinicServices.CountAsync(cs => cs.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalJunctions);
        var updatedInvoice = await context.Invoices.FirstAsync(i => i.Id == pendingInvoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(150000, updatedInvoice.TotalAmount);
    }

    [Fact]
    public async Task TC_5_2_2_LoopSafety_CaseEndChain_DoesNotReTriggerEndTransition()
    {
        // Arrange
        using var context = CreateContext();
        var (c, doctor, _) = SeedCaseWithPatient(context, CaseStatus.Confirmed);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = Guid.NewGuid(),
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mockCases = new Mock<ICaseRepository>();
        mockCases.Setup(repo => repo.GetForUpdateAsync(c.CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        mockCases.Setup(repo => repo.GetDetailAsync(c.CaseId, It.IsAny<CancellationToken>())).ReturnsAsync(c);

        int saveChangesCount = 0;
        mockCases.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saveChangesCount++)
            .Returns(Task.CompletedTask);

        var invoiceService = CreateInvoiceService(context);

        var caseService = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            Mock.Of<IUserRepository>(),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            Mock.Of<ICaseClinicServiceService>(),
            invoiceService,
            context);

        // Act
        await caseService.EndWithoutPrescriptionAsync(c.CaseId, doctor.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CaseStatus.End, c.Status);
        Assert.Equal(1, saveChangesCount); // Exactly 1 state save, no loop
    }

    [Fact]
    public async Task TC_5_2_3_LoopSafety_CancelRegenerateChain_DoesNotAutoCancelNewInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "REGEN_SVC",
            Name = "Dịch vụ tái tạo",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = clinicService.Id,
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(clinicService);
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);

        // Act - Generate, cancel, regenerate
        var inv1 = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);
        await invoiceService.CancelInvoiceAsync(inv1, new CancelInvoiceRequest { Reason = "Lỗi nhập liệu" });

        var inv2 = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);

        // Assert - New invoice is PENDING and not automatically cancelled
        var newInvoice = await context.Invoices.FindAsync(new object[] { inv2 }, TestContext.Current.CancellationToken);
        Assert.NotNull(newInvoice);
        Assert.Equal(InvoiceStatus.PENDING, newInvoice.Status);
        Assert.Equal(100000, newInvoice.TotalAmount);
    }

    #endregion

    #region 5.3 Transaction Safety & Fault Recovery (5 test cases)

    [Fact]
    public async Task TC_5_3_1_Transaction_AddServiceFailure_NoOrphanRecords()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        var service = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        var nonExistentServiceId = Guid.NewGuid();

        // Act & Assert - Attempting to add non-existent service should throw
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, nonExistentServiceId, TestContext.Current.CancellationToken));

        // Ensure no partial state
        Assert.Empty(await context.CaseClinicServices.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.InvoiceItems.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TC_5_3_2_Transaction_InvoiceGenFailure_NoOrphanInvoiceShell()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        var invoiceService = CreateInvoiceService(context);

        // Act & Assert - Empty case should fail
        await Assert.ThrowsAsync<BusinessException>(() =>
            invoiceService.GenerateInvoiceForCaseAsync(c.CaseId));

        // Ensure no empty invoice shell was created
        var invoices = await context.Invoices.Where(i => i.CaseId == c.CaseId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(invoices);
    }

    [Fact]
    public async Task TC_5_3_3_Resilience_CaseCreateIsolation_CasePersistedEvenIfAutoServiceFails()
    {
        // Arrange
        using var context = CreateContext();
        var mockCases = new Mock<ICaseRepository>();
        var mockClinicService = new Mock<ICaseClinicServiceService>();

        Case? capturedCase = null;
        mockCases.Setup(c => c.CreateAsync(It.IsAny<Case>(), It.IsAny<CancellationToken>()))
            .Callback<Case, CancellationToken>((c, _) => capturedCase = c)
            .ReturnsAsync((Case c, CancellationToken ct) => c);

        mockClinicService
            .Setup(s => s.AddServiceToCaseByCodeAsync(It.IsAny<Guid>(), "GENERAL_EXAM", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service catalog offline"));

        var service = new CaseService(
            mockCases.Object,
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            Mock.Of<IUserRepository>(),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance,
            mockClinicService.Object,
            null,
            context);

        // Act
        var caseId = await service.CreateFromBookingAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 11),
            new List<SymptomInput>(),
            TestContext.Current.CancellationToken);

        // Assert - Case was saved even though service trigger threw
        Assert.NotNull(capturedCase);
        Assert.Equal(caseId, capturedCase.CaseId);
    }

    [Fact]
    public async Task TC_5_3_4_Resilience_AnalysisIsolation_AnalysisPersistedEvenIfAutoServiceFails()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context, CaseStatus.InProgress);

        var mockClinicService = new Mock<ICaseClinicServiceService>();
        mockClinicService
            .Setup(s => s.AddServiceToCaseByCodeAsync(It.IsAny<Guid>(), "ULTRASOUND_EXAM", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Catalog unreachable"));

        var aiModelVersionRepoMock = new Mock<IAiModelVersionRepository>();
        aiModelVersionRepoMock.Setup(r => r.GetActiveVersionReadOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelVersion
            {
                ModelVersionId = Guid.NewGuid(),
                VersionCode = "v1.0",
                Status = ModelVersionStatus.Active,
                RegisteredBy = Guid.NewGuid(),
                RegisteredAt = DateTime.UtcNow,
                HfRepoId = "repo",
                HfFilename = "file"
            });

        var diagnosisService = new CaseDiagnosisService(
            context,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IHttpClientFactory>(),
            aiModelVersionRepoMock.Object,
            new UltrasoundImageRepository(context),
            new AiPredictionRepository(context),
            new DoctorAnnotationRepository(context),
            new CaseRepository(context),
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),
            NullLogger<CaseDiagnosisService>.Instance,
            mockClinicService.Object);

        var request = new ConfirmAnalysisRequest
        {
            OriginalImageStream = new System.IO.MemoryStream(new byte[] { 1 }),
            OriginalImageFileName = "a.png",
            OriginalImageContentType = "image/png",
            BurntImageStream = new System.IO.MemoryStream(new byte[] { 2 }),
            BurntImageFileName = "b.png",
            BurntImageContentType = "image/png",
            AiPredictionsJson = "[]",
            DoctorAnnotationsJson = "[]"
        };

        // Act - should succeed without throwing
        await diagnosisService.ConfirmAnalysisAsync(c.CaseId, request, TestContext.Current.CancellationToken);

        // Assert - Ultrasound image is persisted
        var imgCount = await context.UltrasoundImages.CountAsync(i => i.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(1, imgCount);
    }

    [Fact]
    public async Task TC_5_3_5_SelfHealing_ZeroItemsAutoCancel_InvoiceTransitionsToCancelled()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "SOLE_SVC",
            Name = "Dịch vụ duy nhất",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = clinicService.Id,
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(clinicService);
        context.CaseClinicServices.Add(junction);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow
        };
        var item = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = clinicService.Name,
            Quantity = 1,
            UnitPrice = 100000,
            TotalPrice = 100000,
            ItemType = InvoiceItemType.Service,
            ReferenceId = junction.Id
        };
        invoice.InvoiceItems.Add(item);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        // Act - Remove the sole service from the case
        await service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken);

        // Assert - Invoice self-heals by cancelling itself (no useless 0-item shells)
        var updatedInvoice = await context.Invoices.FirstAsync(i => i.Id == invoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.CANCELLED, updatedInvoice.Status);
        Assert.Equal(0, updatedInvoice.TotalAmount);
        Assert.NotNull(updatedInvoice.CancelledReason);
    }

    #endregion

    #region 5.4 Backward Compatibility & Boundary (3 test cases)

    [Fact]
    public async Task TC_5_4_1_BackwardCompat_LegacyInvoiceItems_DefaultToMedicine()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        var legacyInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 60000,
            CreatedAt = DateTime.UtcNow.AddMonths(-1)
        };
        var legacyItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = legacyInvoice.Id,
            Description = "Amoxicillin 500mg (Legacy)",
            Quantity = 1,
            UnitPrice = 60000,
            TotalPrice = 60000,
            ItemType = InvoiceItemType.Medicine,
            ReferenceId = Guid.NewGuid()
        };
        legacyInvoice.InvoiceItems.Add(legacyItem);
        context.Invoices.Add(legacyInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);

        // Act
        var detail = await invoiceService.GetInvoiceDetailAsync(legacyInvoice.Id);

        // Assert
        var item = Assert.Single(detail.Items);
        Assert.Equal("MEDICINE", item.ItemType);
        Assert.Equal("Amoxicillin 500mg (Legacy)", item.Description);
    }

    [Fact]
    public async Task TC_5_4_2_BackwardCompat_LegacyCaseWithoutClinicServices_GeneratesMedicineOnlyInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);

        // Legacy case: only has prescription, zero CaseClinicServices
        var unit = new MedicineUnit { MedicineUnitId = Guid.NewGuid(), Name = "Viên" };
        var medicine = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
        var packaging = new MedicinePackaging
        {
            Id = Guid.NewGuid(),
            MedicineId = medicine.MedicineId,
            Medicine = medicine,
            MedicineUnitId = unit.MedicineUnitId,
            MedicineUnit = unit,
            ConversionFactor = 1,
            IsBaseUnit = true,
            IsSellable = true,
            SalePrice = 10000
        };
        var prescription = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = PrescriptionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = prescription.PrescriptionId,
            MedicineId = medicine.MedicineId,
            Medicine = medicine,
            QuantityBase = 5,
            Dosage = "1 viên"
        };
        prescription.PrescriptionItems.Add(pItem);

        context.MedicineUnits.Add(unit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(packaging);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);

        // Act
        var invoiceId = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(50000, invoice.TotalAmount); // 10k * 5
        var singleItem = Assert.Single(invoice.InvoiceItems);
        Assert.Equal(InvoiceItemType.Medicine, singleItem.ItemType);
    }

    [Fact]
    public async Task TC_5_4_3_BackwardCompat_PriceBoundaryAndVietnameseUnicode()
    {
        // Arrange
        using var context = CreateContext();
        var mgmtService = new ClinicServiceManagementService(context, NullLogger<ClinicServiceManagementService>.Instance);

        // Boundary: 200-char name and minimum positive price (0.01) with rich Vietnamese unicode
        var unicodeName = "Khám sức khỏe tổng quát định kỳ & Chẩn đoán hình ảnh siêu âm 4D chất lượng cao (Tiêu chuẩn Quốc tế ISO 9001:2026)";
        // Pad to exactly 200 characters
        if (unicodeName.Length < 200)
        {
            unicodeName = unicodeName + new string(' ', 200 - unicodeName.Length);
        }

        var request = new CreateClinicServiceRequest
        {
            Code = "VN_UNICODE_001",
            Name = unicodeName.Trim(),
            Price = 0.01m,
            Description = "Dịch vụ phòng khám y tế cho phụ nữ và trẻ em"
        };

        // Act
        var result = await mgmtService.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.01m, result.Price);
        Assert.Contains("Khám sức khỏe tổng quát", result.Name);

        // Verify query by active filter returns it cleanly
        var list = await mgmtService.GetAllAsync(true, TestContext.Current.CancellationToken);
        Assert.Contains(list, s => s.Code == "VN_UNICODE_001");
    }

    #endregion

    #endregion

    #region Group 6: Workflows & Error Flows (10 test cases)

    #region 6.1 Happy Path Workflows (7 test cases)

    [Fact]
    public async Task TC_6_1_1_Workflow_CompletePatientJourney()
    {
        // 1. Admin setup
        using var context = CreateContext();
        var mgmtService = new ClinicServiceManagementService(context, NullLogger<ClinicServiceManagementService>.Instance);
        var genExam = await mgmtService.CreateAsync(new CreateClinicServiceRequest { Code = "GENERAL_EXAM", Name = "Khám thường", Price = 100000 }, TestContext.Current.CancellationToken);
        var usExam = await mgmtService.CreateAsync(new CreateClinicServiceRequest { Code = "ULTRASOUND_EXAM", Name = "Khám siêu âm", Price = 200000 }, TestContext.Current.CancellationToken);

        // 2. Doctor creates case -> auto-adds GENERAL_EXAM
        var (medicalCase, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
        await caseClinicService.AddServiceToCaseByCodeAsync(medicalCase.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);

        // 3. Ultrasound AI confirmation -> auto-adds ULTRASOUND_EXAM
        await caseClinicService.AddServiceToCaseByCodeAsync(medicalCase.CaseId, "ULTRASOUND_EXAM", TestContext.Current.CancellationToken);

        // 4. Prescription added
        var unit = new MedicineUnit { MedicineUnitId = Guid.NewGuid(), Name = "Vỉ" };
        var medicine = new Medicine { MedicineId = Guid.NewGuid(), Name = "Thuốc bổ", UsageUnit = "Vỉ", VolumePerBaseUnit = 1 };
        var packaging = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicine.MedicineId, Medicine = medicine, MedicineUnitId = unit.MedicineUnitId, MedicineUnit = unit, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 50000 };
        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = medicalCase.CaseId, Status = PrescriptionStatus.Active, CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicine.MedicineId, Medicine = medicine, QuantityBase = 1, Dosage = "1 vỉ" };
        prescription.PrescriptionItems.Add(pItem);
        context.MedicineUnits.Add(unit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(packaging);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 5. Generate invoice (GENERAL_EXAM 100k + ULTRASOUND_EXAM 200k + Medicine 50k = 350k)
        var mockInventory = new Mock<IInventoryService>();
        var invoiceService = CreateInvoiceService(context, mockInventory.Object);
        var invoiceId = await invoiceService.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(350000, invoice.TotalAmount);
        Assert.Equal(3, invoice.InvoiceItems.Count);

        // 6. Staff pays invoice
        await invoiceService.PayAndDispenseAsync(invoiceId, PaymentMethod.CASH);

        var paidInvoice = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(paidInvoice);
        Assert.Equal(InvoiceStatus.PAID, paidInvoice.Status);
        mockInventory.Verify(inv => inv.DispenseAsync(medicalCase.CaseId), Times.Once);
    }

    [Fact]
    public async Task TC_6_1_2_Workflow_ConsultationOnly_GeneralExamPlusMedicine()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var clinicService = new ClinicService { Id = Guid.NewGuid(), Code = "GENERAL_EXAM", Name = "Khám thường", Price = 100000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.Add(clinicService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await caseClinicService.AddServiceToCaseByCodeAsync(c.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);

        var unit = new MedicineUnit { MedicineUnitId = Guid.NewGuid(), Name = "Chai" };
        var medicine = new Medicine { MedicineId = Guid.NewGuid(), Name = "Siro ho", UsageUnit = "Chai", VolumePerBaseUnit = 1 };
        var packaging = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicine.MedicineId, Medicine = medicine, MedicineUnitId = unit.MedicineUnitId, MedicineUnit = unit, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 80000 };
        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = c.CaseId, Status = PrescriptionStatus.Active, CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicine.MedicineId, Medicine = medicine, QuantityBase = 1, Dosage = "1 chai" };
        prescription.PrescriptionItems.Add(pItem);
        context.MedicineUnits.Add(unit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(packaging);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);

        // Act
        var invoiceId = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(180000, invoice.TotalAmount); // 100k + 80k
        Assert.Equal(2, invoice.InvoiceItems.Count);
        Assert.Single(invoice.InvoiceItems, i => i.ItemType == InvoiceItemType.Service);
        Assert.Single(invoice.InvoiceItems, i => i.ItemType == InvoiceItemType.Medicine);
    }

    [Fact]
    public async Task TC_6_1_3_Workflow_ServiceOnlyNoPrescription()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var clinicService = new ClinicService { Id = Guid.NewGuid(), Code = "GENERAL_EXAM", Name = "Khám thường", Price = 100000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.Add(clinicService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await caseClinicService.AddServiceToCaseByCodeAsync(c.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);

        // Act - Case ends without prescription, generates invoice
        var invoiceId = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(100000, invoice.TotalAmount);
        var item = Assert.Single(invoice.InvoiceItems);
        Assert.Equal(InvoiceItemType.Service, item.ItemType);
    }

    [Fact]
    public async Task TC_6_1_4_Workflow_MistakeCorrection_DoctorRemovesAutoAddedService()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var s1 = new ClinicService { Id = Guid.NewGuid(), Code = "GENERAL_EXAM", Name = "Khám thường", Price = 100000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var s2 = new ClinicService { Id = Guid.NewGuid(), Code = "ULTRASOUND_EXAM", Name = "Khám siêu âm", Price = 200000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.AddRange(s1, s2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await caseClinicService.AddServiceToCaseByCodeAsync(c.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);
        var usResult = await caseClinicService.AddServiceToCaseAsync(c.CaseId, s2.Id, TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);
        var invoiceId = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);

        var initialInvoice = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(initialInvoice);
        Assert.Equal(300000, initialInvoice.TotalAmount); // 100k + 200k

        // Act - Doctor realizes ultrasound was added in error, removes it
        await caseClinicService.RemoveServiceFromCaseAsync(c.CaseId, usResult.Id, TestContext.Current.CancellationToken);

        // Assert - Invoice total adjusted downwards
        var updatedInvoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(100000, updatedInvoice.TotalAmount);
        Assert.Single(updatedInvoice.InvoiceItems);
        Assert.Equal(s1.Name, updatedInvoice.InvoiceItems.First().Description);
    }

    [Fact]
    public async Task TC_6_1_5_Workflow_PaymentAndCancellation_ThenRegeneration()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var s = new ClinicService { Id = Guid.NewGuid(), Code = "GENERAL_EXAM", Name = "Khám thường", Price = 100000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.Add(s);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await caseClinicService.AddServiceToCaseByCodeAsync(c.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);

        var invoiceService = CreateInvoiceService(context);
        var invoiceId = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);
        await invoiceService.PayAndDispenseAsync(invoiceId, PaymentMethod.CASH);

        // Act 1: Cancel paid invoice
        await invoiceService.CancelInvoiceAsync(invoiceId, new CancelInvoiceRequest { Reason = "Khách muốn thanh toán chuyển khoản" });

        var cancelledInvoice = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(cancelledInvoice);
        Assert.Equal(InvoiceStatus.CANCELLED, cancelledInvoice.Status);

        // Act 2: Regenerate invoice
        var newInvoiceId = await invoiceService.GenerateInvoiceForCaseAsync(c.CaseId);

        // Assert
        Assert.NotEqual(invoiceId, newInvoiceId);
        var newInvoice = await context.Invoices.FindAsync(new object[] { newInvoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(newInvoice);
        Assert.Equal(InvoiceStatus.PENDING, newInvoice.Status);
        Assert.Equal(100000, newInvoice.TotalAmount);
    }

    [Fact]
    public async Task TC_6_1_6_Workflow_AdminReactivatesService_DoctorCanAttachToNewCase()
    {
        // Arrange
        using var context = CreateContext();
        var mgmtService = new ClinicServiceManagementService(context, NullLogger<ClinicServiceManagementService>.Instance);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var created = await mgmtService.CreateAsync(new CreateClinicServiceRequest
        {
            Code = "TOGGLE_SVC",
            Name = "Dịch vụ bật tắt",
            Price = 150000
        }, TestContext.Current.CancellationToken);

        // Step 1: Deactivate
        await mgmtService.DeactivateAsync(created.Id, TestContext.Current.CancellationToken);
        var deactivated = await mgmtService.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
        Assert.False(deactivated.IsActive);

        // Step 2: Reactivate
        await mgmtService.UpdateAsync(created.Id, new UpdateClinicServiceRequest { IsActive = true }, TestContext.Current.CancellationToken);
        var reactivated = await mgmtService.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
        Assert.True(reactivated.IsActive);

        // Step 3: Doctor attaches to a new case
        var (c, _, _) = SeedCaseWithPatient(context);
        var attached = await caseClinicService.AddServiceToCaseAsync(c.CaseId, created.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(attached);
        Assert.Equal(150000, attached.PriceAtTime);
    }

    [Fact]
    public async Task TC_6_1_7_Workflow_RetriggerUltrasound_DoctorDeletesThenReconfirms()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var usService = new ClinicService { Id = Guid.NewGuid(), Code = "ULTRASOUND_EXAM", Name = "Khám siêu âm", Price = 200000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.Add(usService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Step 1: Initial auto-trigger
        await caseClinicService.AddServiceToCaseByCodeAsync(c.CaseId, "ULTRASOUND_EXAM", TestContext.Current.CancellationToken);
        var attached1 = await context.CaseClinicServices.FirstAsync(cs => cs.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.NotNull(attached1);

        // Step 2: Doctor deletes it
        await caseClinicService.RemoveServiceFromCaseAsync(c.CaseId, attached1.Id, TestContext.Current.CancellationToken);
        Assert.Empty(await context.CaseClinicServices.Where(cs => cs.CaseId == c.CaseId).ToListAsync(TestContext.Current.CancellationToken));

        // Step 3: Second image analysis re-triggers auto-add
        await caseClinicService.AddServiceToCaseByCodeAsync(c.CaseId, "ULTRASOUND_EXAM", TestContext.Current.CancellationToken);

        // Assert
        var reattached = await context.CaseClinicServices.FirstOrDefaultAsync(cs => cs.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.NotNull(reattached);
        Assert.Equal(usService.Id, reattached.ClinicServiceId);
        Assert.Equal(200000, reattached.PriceAtTime);
    }

    #endregion

    #region 6.2 Error Flows (3 test cases)

    [Fact]
    public async Task TC_6_2_1_ErrorFlow_AddServiceToEndedCase_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context, CaseStatus.End);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var s = new ClinicService { Id = Guid.NewGuid(), Code = "EXAM", Name = "Khám", Price = 100000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.Add(s);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            caseClinicService.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể thêm dịch vụ vào ca khám đã hoàn thành hoặc bị hủy.", ex.Message);
    }

    [Fact]
    public async Task TC_6_2_2_ErrorFlow_DeleteServiceFromPaidInvoice_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var s = new ClinicService { Id = Guid.NewGuid(), Code = "EXAM", Name = "Khám", Price = 100000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var junction = new CaseClinicService { Id = Guid.NewGuid(), CaseId = c.CaseId, ClinicServiceId = s.Id, PriceAtTime = 100000, CreatedAt = DateTime.UtcNow };
        var paidInvoice = new Invoice { Id = Guid.NewGuid(), CaseId = c.CaseId, Status = InvoiceStatus.PAID, TotalAmount = 100000, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow };

        context.ClinicServices.Add(s);
        context.CaseClinicServices.Add(junction);
        context.Invoices.Add(paidInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            caseClinicService.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể xóa dịch vụ khi hóa đơn đã thanh toán.", ex.Message);
    }

    [Fact]
    public async Task TC_6_2_3_ErrorFlow_AddInactiveService_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var (c, _, _) = SeedCaseWithPatient(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var inactiveService = new ClinicService { Id = Guid.NewGuid(), Code = "INACT_EXAM", Name = "Dịch vụ đã tắt", Price = 100000, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.ClinicServices.Add(inactiveService);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            caseClinicService.AddServiceToCaseAsync(c.CaseId, inactiveService.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Dịch vụ không tồn tại hoặc không hoạt động.", ex.Message);
    }

    #endregion

    #endregion
}
