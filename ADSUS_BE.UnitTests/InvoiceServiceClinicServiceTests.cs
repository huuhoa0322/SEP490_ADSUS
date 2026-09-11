using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// Group 3: 20 test cases for InvoiceService (Fee calculation, dispensation & cancellation with clinic services)
/// Covers 3.1.1 - 3.5.2 in scratch/test_cases.md
/// </summary>
public class InvoiceServiceClinicServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (InvoiceService service, Mock<IInventoryService> inventoryMock, Mock<INotificationService> notifMock)
        CreateService(AppDbContext context)
    {
        var inventoryMock = new Mock<IInventoryService>();
        var intakeLogRepoMock = new Mock<IMedicationIntakeLogRepository>();
        var scheduleGeneratorMock = new Mock<IMedicationIntakeScheduleGenerator>();
        var notifMock = new Mock<INotificationService>();

        var service = new InvoiceService(
            context,
            inventoryMock.Object,
            intakeLogRepoMock.Object,
            scheduleGeneratorMock.Object,
            notifMock.Object);

        return (service, inventoryMock, notifMock);
    }

    private static Case SeedCase(AppDbContext context)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Nguyễn Văn A",
            Email = "a@gmail.com",
            Phone = "0981111001",
            PasswordHash = "hashed-password",
            Role = UserRole.Patient
        };
        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            PatientProfileId = profile.PatientProfileId,
            PatientProfile = profile,
            Status = CaseStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.PatientProfiles.Add(profile);
        context.Cases.Add(medicalCase);
        context.SaveChanges();

        return medicalCase;
    }

    private static CaseClinicService SeedClinicServiceForCase(AppDbContext context, Guid caseId, string code, string name, decimal price)
    {
        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Price = price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var caseClinicService = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            ClinicServiceId = clinicService.Id,
            ClinicService = clinicService,
            PriceAtTime = price,
            CreatedAt = DateTime.UtcNow
        };

        context.ClinicServices.Add(clinicService);
        context.CaseClinicServices.Add(caseClinicService);
        context.SaveChanges();

        return caseClinicService;
    }

    private static (Prescription prescription, PrescriptionItem item) SeedPrescriptionForCase(AppDbContext context, Guid caseId, decimal salePrice = 50000, int qty = 1)
    {
        var unit = new MedicineUnit { MedicineUnitId = Guid.NewGuid(), Name = "Hộp" };
        var medicine = new Medicine
        {
            MedicineId = Guid.NewGuid(),
            Name = "Paracetamol 500mg",
            UsageUnit = "Hộp",
            VolumePerBaseUnit = 1
        };
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
            SalePrice = salePrice
        };

        var prescription = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = caseId,
            Status = PrescriptionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = Guid.NewGuid(),
            PrescriptionId = prescription.PrescriptionId,
            MedicineId = medicine.MedicineId,
            Medicine = medicine,
            QuantityBase = qty,
            Dosage = "1 hộp"
        };
        prescription.PrescriptionItems.Add(pItem);

        context.MedicineUnits.Add(unit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(packaging);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        context.SaveChanges();

        return (prescription, pItem);
    }

    #region 3.1 GenerateInvoiceForCaseAsync — Happy Path (4 test cases)

    [Fact]
    public async Task TC_3_1_1_GenerateInvoice_ServicesAndMedicine_CreatesInvoiceWithBothItemTypes()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);

        var cs1 = SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);
        var cs2 = SeedClinicServiceForCase(context, medicalCase.CaseId, "ULTRASOUND_EXAM", "Khám siêu âm", 200000);
        var (_, pItem) = SeedPrescriptionForCase(context, medicalCase.CaseId, salePrice: 50000, qty: 1);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.PENDING, invoice.Status);
        Assert.Equal(350000, invoice.TotalAmount); // 100k + 200k + 50k

        var items = await context.InvoiceItems.Where(i => i.InvoiceId == invoiceId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, items.Count);

        var serviceItems = items.Where(i => i.ItemType == InvoiceItemType.Service).ToList();
        var medicineItems = items.Where(i => i.ItemType == InvoiceItemType.Medicine).ToList();

        Assert.Equal(2, serviceItems.Count);
        var medicineItem = Assert.Single(medicineItems);
        Assert.Contains(serviceItems, s => s.ReferenceId == cs1.Id && s.UnitPrice == 100000);
        Assert.Contains(serviceItems, s => s.ReferenceId == cs2.Id && s.UnitPrice == 200000);
        Assert.Equal(pItem.PrescriptionItemId, medicineItem.ReferenceId);
    }

    [Fact]
    public async Task TC_3_1_2_GenerateInvoice_ServiceOnly_CreatesInvoiceWithoutPrescription()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);

        var cs = SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 120000);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var invoice = await context.Invoices.FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(120000, invoice.TotalAmount);

        var items = await context.InvoiceItems.Where(i => i.InvoiceId == invoiceId).ToListAsync(TestContext.Current.CancellationToken);
        var singleItem = Assert.Single(items);
        Assert.Equal(InvoiceItemType.Service, singleItem.ItemType);
        Assert.Equal(cs.Id, singleItem.ReferenceId);
        Assert.Equal(120000, singleItem.UnitPrice);
    }

    [Fact]
    public async Task TC_3_1_3_GenerateInvoice_MedicineOnly_BackwardCompatibility()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);

        var (_, pItem) = SeedPrescriptionForCase(context, medicalCase.CaseId, salePrice: 80000, qty: 2);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var invoice = await context.Invoices.FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(160000, invoice.TotalAmount); // 80k * 2

        var items = await context.InvoiceItems.Where(i => i.InvoiceId == invoiceId).ToListAsync(TestContext.Current.CancellationToken);
        var singleItem = Assert.Single(items);
        Assert.Equal(InvoiceItemType.Medicine, singleItem.ItemType);
        Assert.Equal(pItem.PrescriptionItemId, singleItem.ReferenceId);
    }

    [Fact]
    public async Task TC_3_1_4_GenerateInvoice_TotalCalculation_MatchesSumOfAllItems()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);

        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "ULTRASOUND_EXAM", "Khám siêu âm", 200000);
        SeedPrescriptionForCase(context, medicalCase.CaseId, salePrice: 50000, qty: 1);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var invoice = await context.Invoices.FirstAsync(i => i.Id == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(350000, invoice.TotalAmount);
    }

    #endregion

    #region 3.2 GenerateInvoiceForCaseAsync — Edge Cases (8 test cases)

    [Fact]
    public async Task TC_3_2_1_GenerateInvoice_EmptyCase_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.GenerateInvoiceForCaseAsync(medicalCase.CaseId));
        Assert.Contains("Không tìm thấy dịch vụ hoặc đơn thuốc cho ca khám này", ex.Message);
    }

    [Fact]
    public async Task TC_3_2_2_GenerateInvoice_ExistingPendingInvoice_ReturnsExistingId()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        var existingPending = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(existingPending);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        Assert.Equal(existingPending.Id, resultId);
        var totalInvoices = await context.Invoices.CountAsync(i => i.CaseId == medicalCase.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalInvoices);
    }

    [Fact]
    public async Task TC_3_2_3_GenerateInvoice_ExistingPaidInvoice_ReturnsExistingId()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        var existingPaid = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            PaidAt = DateTime.UtcNow.AddHours(-1)
        };
        context.Invoices.Add(existingPaid);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        Assert.Equal(existingPaid.Id, resultId);
        var totalInvoices = await context.Invoices.CountAsync(i => i.CaseId == medicalCase.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(1, totalInvoices);
    }

    [Fact]
    public async Task TC_3_2_4_GenerateInvoice_ExistingCancelledInvoice_GeneratesFreshInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        var existingCancelled = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Status = InvoiceStatus.CANCELLED,
            CancelledReason = "Hủy do bác sĩ chỉnh sửa",
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        context.Invoices.Add(existingCancelled);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var resultId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        Assert.NotEqual(existingCancelled.Id, resultId);
        var newInvoice = await context.Invoices.FindAsync(new object[] { resultId }, TestContext.Current.CancellationToken);
        Assert.NotNull(newInvoice);
        Assert.Equal(InvoiceStatus.PENDING, newInvoice.Status);
        Assert.Equal(100000, newInvoice.TotalAmount);
    }

    [Fact]
    public async Task TC_3_2_5_GenerateInvoice_ServiceItemType_CorrectlyAssigned()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var item = await context.InvoiceItems.FirstAsync(i => i.InvoiceId == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceItemType.Service, item.ItemType);
    }

    [Fact]
    public async Task TC_3_2_6_GenerateInvoice_MedicineItemType_CorrectlyAssigned()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedPrescriptionForCase(context, medicalCase.CaseId);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var item = await context.InvoiceItems.FirstAsync(i => i.InvoiceId == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceItemType.Medicine, item.ItemType);
    }

    [Fact]
    public async Task TC_3_2_7_GenerateInvoice_ServiceReferenceId_MatchesCaseClinicServiceId()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        var cs = SeedClinicServiceForCase(context, medicalCase.CaseId, "XRAY", "Chụp X-quang", 150000);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var item = await context.InvoiceItems.FirstAsync(i => i.InvoiceId == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(cs.Id, item.ReferenceId);
    }

    [Fact]
    public async Task TC_3_2_8_GenerateInvoice_MedicineReferenceId_MatchesPrescriptionItemId()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        var (_, pItem) = SeedPrescriptionForCase(context, medicalCase.CaseId);

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        var item = await context.InvoiceItems.FirstAsync(i => i.InvoiceId == invoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(pItem.PrescriptionItemId, item.ReferenceId);
    }

    #endregion

    #region 3.3 PayAndDispenseAsync (2 test cases)

    [Fact]
    public async Task TC_3_3_1_PayAndDispense_ServiceOnly_MarksPaidWithoutInvokingDispenseOrIntakeLogs()
    {
        // Arrange
        using var context = CreateContext();
        var (service, inventoryMock, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        var cs = SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow
        };
        var item = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = cs.ClinicService.Name,
            Quantity = 1,
            UnitPrice = 100000,
            TotalPrice = 100000,
            ItemType = InvoiceItemType.Service,
            ReferenceId = cs.Id
        };
        invoice.InvoiceItems.Add(item);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.PayAndDispenseAsync(invoice.Id, PaymentMethod.CASH);

        // Assert
        var updated = await context.Invoices.FindAsync(new object[] { invoice.Id }, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.PAID, updated.Status);
        Assert.NotNull(updated.PaidAt);
        Assert.Equal(PaymentMethod.CASH, updated.PaymentMethod);

        // Inventory dispense should NOT be called for service-only invoice
        inventoryMock.Verify(inv => inv.DispenseAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TC_3_3_2_PayAndDispense_MixedInvoice_MarksPaidAndDispensesMedicines()
    {
        // Arrange
        using var context = CreateContext();
        var (service, inventoryMock, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);
        SeedPrescriptionForCase(context, medicalCase.CaseId, salePrice: 50000, qty: 1);

        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Act
        await service.PayAndDispenseAsync(invoiceId, PaymentMethod.BANK_TRANSFER);

        // Assert
        var updated = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.PAID, updated.Status);
        Assert.Equal(PaymentMethod.BANK_TRANSFER, updated.PaymentMethod);

        // Inventory dispense SHOULD be called for prescription
        inventoryMock.Verify(inv => inv.DispenseAsync(medicalCase.CaseId), Times.Once);
    }

    #endregion

    #region 3.4 CancelInvoiceAsync (4 test cases)

    [Fact]
    public async Task TC_3_4_1_CancelInvoice_PendingMixed_CancelsInvoiceRetainingCaseClinicServices()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        var cs = SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);
        SeedPrescriptionForCase(context, medicalCase.CaseId);

        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Act
        await service.CancelInvoiceAsync(invoiceId, new CancelInvoiceRequest { Reason = "Bệnh nhân yêu cầu hủy" });

        // Assert
        var invoice = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.CANCELLED, invoice.Status);
        Assert.Equal("Bệnh nhân yêu cầu hủy", invoice.CancelledReason);

        // CaseClinicService record remains untouched
        var junctionExists = await context.CaseClinicServices.AnyAsync(c => c.Id == cs.Id, TestContext.Current.CancellationToken);
        Assert.True(junctionExists);
    }

    [Fact]
    public async Task TC_3_4_2_CancelInvoice_PaidMixed_RetainsCaseClinicServices()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        var cs = SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);
        SeedPrescriptionForCase(context, medicalCase.CaseId);

        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);
        await service.PayAndDispenseAsync(invoiceId, PaymentMethod.CASH);

        // Act
        await service.CancelInvoiceAsync(invoiceId, new CancelInvoiceRequest { Reason = "Nhầm lẫn thông tin" });

        // Assert
        var invoice = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.CANCELLED, invoice.Status);

        // CaseClinicService retained
        Assert.True(await context.CaseClinicServices.AnyAsync(c => c.Id == cs.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TC_3_4_3_CancelInvoice_PaidServiceOnly_CancelsGracefullyWithoutNullRef()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        var cs = SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);
        await service.PayAndDispenseAsync(invoiceId, PaymentMethod.CASH);

        // Act - should cancel without throwing NullReferenceException
        await service.CancelInvoiceAsync(invoiceId, new CancelInvoiceRequest { Reason = "Hủy dịch vụ khám" });

        // Assert
        var invoice = await context.Invoices.FindAsync(new object[] { invoiceId }, TestContext.Current.CancellationToken);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.CANCELLED, invoice.Status);
        Assert.True(await context.CaseClinicServices.AnyAsync(c => c.Id == cs.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TC_3_4_4_CancelInvoice_ThenRegenerate_CreatesNewInvoiceWithExistingServices()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);

        var oldInvoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);
        await service.CancelInvoiceAsync(oldInvoiceId, new CancelInvoiceRequest { Reason = "Thay đổi phương thức" });

        // Act - Re-generate
        var newInvoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Assert
        Assert.NotEqual(oldInvoiceId, newInvoiceId);
        var newInvoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == newInvoiceId, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.PENDING, newInvoice.Status);
        Assert.Equal(100000, newInvoice.TotalAmount);
        Assert.Single(newInvoice.InvoiceItems);
    }

    #endregion

    #region 3.5 GetInvoiceDetailAsync (2 test cases)

    [Fact]
    public async Task TC_3_5_1_GetInvoiceDetail_ProjectsItemTypeCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);
        SeedClinicServiceForCase(context, medicalCase.CaseId, "GENERAL_EXAM", "Khám thường", 100000);
        SeedPrescriptionForCase(context, medicalCase.CaseId, salePrice: 50000, qty: 1);

        var invoiceId = await service.GenerateInvoiceForCaseAsync(medicalCase.CaseId);

        // Act
        var detail = await service.GetInvoiceDetailAsync(invoiceId);

        // Assert
        Assert.NotNull(detail);
        Assert.Equal(invoiceId, detail.Id);
        Assert.Equal(2, detail.Items.Count);

        var serviceItem = detail.Items.FirstOrDefault(i => i.ItemType == "SERVICE");
        var medicineItem = detail.Items.FirstOrDefault(i => i.ItemType == "MEDICINE");

        Assert.NotNull(serviceItem);
        Assert.NotNull(medicineItem);
        Assert.Equal(100000, serviceItem.UnitPrice);
        Assert.Equal(50000, medicineItem.UnitPrice);
    }

    [Fact]
    public async Task TC_3_5_2_GetInvoiceDetail_LegacyInvoiceItems_ProjectAsMedicine()
    {
        // Arrange
        using var context = CreateContext();
        var (service, _, _) = CreateService(context);
        var medicalCase = SeedCase(context);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = medicalCase.CaseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        };
        var legacyItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = "Thuốc ho",
            Quantity = 1,
            UnitPrice = 50000,
            TotalPrice = 50000,
            ItemType = InvoiceItemType.Medicine, // Default legacy
            ReferenceId = Guid.NewGuid()
        };
        invoice.InvoiceItems.Add(legacyItem);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var detail = await service.GetInvoiceDetailAsync(invoice.Id);

        // Assert
        var item = Assert.Single(detail.Items);
        Assert.Equal("MEDICINE", item.ItemType);
    }

    #endregion
}
