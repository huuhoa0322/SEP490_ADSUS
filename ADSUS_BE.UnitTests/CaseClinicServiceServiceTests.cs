using System;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// Group 2: 30 test cases for CaseClinicServiceService (Doctor gắn/xóa dịch vụ trên ca khám)
/// Covers 2.1.1 - 2.5.2 in scratch/test_cases.md
/// </summary>
public class CaseClinicServiceServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CaseClinicServiceService CreateService(AppDbContext context)
    {
        return new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);
    }

    private static (Case medicalCase, ClinicService clinicService) SeedCaseAndService(
        AppDbContext context,
        CaseStatus caseStatus = CaseStatus.InProgress,
        bool serviceActive = true,
        decimal servicePrice = 100000,
        string serviceCode = "GENERAL_EXAM",
        string serviceName = "Khám thường")
    {
        var medicalCase = new Case
        {
            CaseId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            PatientProfileId = Guid.NewGuid(),
            Status = caseStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var clinicService = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = serviceCode,
            Name = serviceName,
            Price = servicePrice,
            IsActive = serviceActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Cases.Add(medicalCase);
        context.ClinicServices.Add(clinicService);
        context.SaveChanges();

        return (medicalCase, clinicService);
    }

    #region 2.1 AddServiceToCaseAsync — Validation (11 test cases)

    [Fact]
    public async Task TC_2_1_1_AddService_CaseInProgress_CreatedWithPriceAtTime()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.InProgress, servicePrice: 150000);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(c.CaseId, result.CaseId);
        Assert.Equal(s.Id, result.ClinicServiceId);
        Assert.Equal(150000, result.PriceAtTime);
        Assert.Equal(s.Name, result.ServiceName);
        Assert.Equal(s.Code, result.ServiceCode);

        var saved = await context.CaseClinicServices.FirstOrDefaultAsync(cs => cs.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(150000, saved.PriceAtTime);
    }

    [Fact]
    public async Task TC_2_1_2_AddService_CaseBooked_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.Booked);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể thêm dịch vụ vào ca khám chưa check-in.", ex.Message);
    }

    [Fact]
    public async Task TC_2_1_2b_AddServiceByCode_CaseBooked_CreatedSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.Booked, serviceCode: "GENERAL_EXAM");

        // Act
        await service.AddServiceToCaseByCodeAsync(c.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);

        // Assert
        var exists = await context.CaseClinicServices.AnyAsync(cs => cs.CaseId == c.CaseId && cs.ClinicServiceId == s.Id, TestContext.Current.CancellationToken);
        Assert.True(exists);
    }

    [Fact]
    public async Task TC_2_1_3_AddService_CaseConfirmed_CreatedSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.Confirmed);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(c.CaseId, result.CaseId);
        Assert.Equal(s.Id, result.ClinicServiceId);
    }

    [Fact]
    public async Task TC_2_1_4_AddService_CaseEnd_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.End);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể thêm dịch vụ vào ca khám đã hoàn thành hoặc bị hủy.", ex.Message);
    }

    [Fact]
    public async Task TC_2_1_5_AddService_CaseCancelled_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.Cancelled);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể thêm dịch vụ vào ca khám đã hoàn thành hoặc bị hủy.", ex.Message);
    }

    [Fact]
    public async Task TC_2_1_6_AddService_CaseNotFound_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var nonExistentCaseId = Guid.NewGuid();
        var (_, s) = SeedCaseAndService(context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(nonExistentCaseId, s.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy ca khám.", ex.Message);
    }

    [Fact]
    public async Task TC_2_1_7_AddService_ServiceNotFound_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, _) = SeedCaseAndService(context);
        var nonExistentServiceId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, nonExistentServiceId, TestContext.Current.CancellationToken));
        Assert.Equal("Dịch vụ không tồn tại hoặc không hoạt động.", ex.Message);
    }

    [Fact]
    public async Task TC_2_1_8_AddService_ServiceInactive_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, serviceActive: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Dịch vụ không tồn tại hoặc không hoạt động.", ex.Message);
    }

    [Fact]
    public async Task TC_2_1_9_AddService_DuplicateCall_IsIdempotentAndDoesNotDuplicate()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context);

        // Act
        var first = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);
        var second = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.Id, second.Id);
        var count = await context.CaseClinicServices.CountAsync(cs => cs.CaseId == c.CaseId && cs.ClinicServiceId == s.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TC_2_1_10_AddService_SnapshotAccuracy_MatchesCatalogPrice()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 175000);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(175000, result.PriceAtTime);
        var record = await context.CaseClinicServices.FindAsync(new object[] { result.Id }, TestContext.Current.CancellationToken);
        Assert.NotNull(record);
        Assert.Equal(175000, record.PriceAtTime);
    }

    [Fact]
    public async Task TC_2_1_11_AddService_PriceMutationImmunity_CatalogUpdateDoesNotAlterSnapshot()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 100000);

        // Act 1: Add to case at 100k
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Act 2: Admin updates catalog price to 200k
        var entity = await context.ClinicServices.FindAsync(new object[] { s.Id }, TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        entity.Price = 200000;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert: Case record still has 100k
        var caseRecord = await context.CaseClinicServices.FindAsync(new object[] { result.Id }, TestContext.Current.CancellationToken);
        Assert.NotNull(caseRecord);
        Assert.Equal(100000, caseRecord.PriceAtTime);
    }

    #endregion

    #region 2.2 AddServiceToCaseAsync — Invoice auto-append (6 test cases)

    [Fact]
    public async Task TC_2_2_1_AddService_WithPendingInvoice_AutoAppendsInvoiceItemAndIncrementsTotal()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 100000);

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

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        var invoice = await context.Invoices.FirstAsync(i => i.Id == pendingInvoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(150000, invoice.TotalAmount); // 50000 + 100000
        var items = await context.InvoiceItems.Where(i => i.InvoiceId == pendingInvoice.Id).ToListAsync(TestContext.Current.CancellationToken);
        var item = Assert.Single(items);
        Assert.Equal(result.Id, item.ReferenceId);
        Assert.Equal(InvoiceItemType.Service, item.ItemType);
        Assert.Equal(100000, item.UnitPrice);
        Assert.Equal(100000, item.TotalPrice);
    }

    [Fact]
    public async Task TC_2_2_2_AddService_ItemTypeVerification_SetsItemTypeToService()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context);

        var pendingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(pendingInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        var item = await context.InvoiceItems.FirstOrDefaultAsync(i => i.InvoiceId == pendingInvoice.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(item);
        Assert.Equal(InvoiceItemType.Service, item.ItemType);
    }

    [Fact]
    public async Task TC_2_2_3_AddService_ReferenceIdVerification_MatchesCaseClinicServiceId()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context);

        var pendingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(pendingInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        var item = await context.InvoiceItems.FirstOrDefaultAsync(i => i.InvoiceId == pendingInvoice.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(item);
        Assert.Equal(result.Id, item.ReferenceId);
    }

    [Fact]
    public async Task TC_2_2_4_AddService_NoExistingInvoice_CreatesJunctionOnlyWithoutEmptyInvoiceShell()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var invoices = await context.Invoices.Where(i => i.CaseId == c.CaseId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(invoices);
    }

    [Fact]
    public async Task TC_2_2_5_AddService_ExistingPaidInvoice_DoesNotAppendToPaidInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 100000);

        var paidInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 80000,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            PaidAt = DateTime.UtcNow.AddHours(-1)
        };
        context.Invoices.Add(paidInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == paidInvoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(80000, invoice.TotalAmount); // Unmodified
        Assert.Empty(invoice.InvoiceItems);
    }

    [Fact]
    public async Task TC_2_2_6_AddService_SnapshotUsedInInvoice_InvoiceItemUsesSnapshotPrice()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 120000);

        var pendingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(pendingInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, TestContext.Current.CancellationToken);

        // Assert
        var invoiceItem = await context.InvoiceItems.FirstOrDefaultAsync(i => i.InvoiceId == pendingInvoice.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(invoiceItem);
        Assert.Equal(result.PriceAtTime, invoiceItem.UnitPrice);
        Assert.Equal(result.PriceAtTime, invoiceItem.TotalPrice);
        Assert.Equal(120000, invoiceItem.UnitPrice);
    }

    #endregion

    #region 2.3 AddServiceToCaseByCodeAsync (4 test cases)

    [Fact]
    public async Task TC_2_3_1_AddByCode_ValidCode_ResolvesAndAttachesService()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, serviceCode: "GENERAL_EXAM");

        // Act
        await service.AddServiceToCaseByCodeAsync(c.CaseId, "GENERAL_EXAM", TestContext.Current.CancellationToken);

        // Assert
        var attached = await context.CaseClinicServices.FirstOrDefaultAsync(cs => cs.CaseId == c.CaseId && cs.ClinicServiceId == s.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(attached);
    }

    [Fact]
    public async Task TC_2_3_2_AddByCode_UnknownCode_GracefulSkipWithoutThrowing()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, _) = SeedCaseAndService(context);

        // Act & Assert (should not throw)
        await service.AddServiceToCaseByCodeAsync(c.CaseId, "NON_EXISTENT_CODE", TestContext.Current.CancellationToken);

        var count = await context.CaseClinicServices.CountAsync(cs => cs.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task TC_2_3_3_AddByCode_InactiveCode_GracefulSkipWithoutThrowing()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, serviceActive: false, serviceCode: "INACTIVE_CODE");

        // Act & Assert (should not throw)
        await service.AddServiceToCaseByCodeAsync(c.CaseId, "INACTIVE_CODE", TestContext.Current.CancellationToken);

        var count = await context.CaseClinicServices.CountAsync(cs => cs.CaseId == c.CaseId, TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task TC_2_3_4_AddByCode_DuplicateCall_IdempotentOnSecondCall()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, serviceCode: "ULTRASOUND_EXAM");

        // Act
        await service.AddServiceToCaseByCodeAsync(c.CaseId, "ULTRASOUND_EXAM", TestContext.Current.CancellationToken);
        await service.AddServiceToCaseByCodeAsync(c.CaseId, "ULTRASOUND_EXAM", TestContext.Current.CancellationToken);

        // Assert
        var count = await context.CaseClinicServices.CountAsync(cs => cs.CaseId == c.CaseId && cs.ClinicServiceId == s.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    #endregion

    #region 2.4 RemoveServiceFromCaseAsync (7 test cases)

    [Fact]
    public async Task TC_2_4_1_RemoveService_NoInvoice_JunctionRecordDeleted()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context);
        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = s.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken);

        // Assert
        var exists = await context.CaseClinicServices.AnyAsync(cs => cs.Id == junction.Id, TestContext.Current.CancellationToken);
        Assert.False(exists);
    }

    [Fact]
    public async Task TC_2_4_2_RemoveService_PendingInvoice_RemovesInvoiceItemAndDecrementsTotal()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 100000);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 250000,
            CreatedAt = DateTime.UtcNow
        };
        var serviceItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = s.Name,
            Quantity = 1,
            UnitPrice = 100000,
            TotalPrice = 100000,
            ItemType = InvoiceItemType.Service,
            ReferenceId = junction.Id
        };
        var medicineItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = "Thuốc kháng sinh",
            Quantity = 1,
            UnitPrice = 150000,
            TotalPrice = 150000,
            ItemType = InvoiceItemType.Medicine,
            ReferenceId = Guid.NewGuid()
        };
        invoice.InvoiceItems.Add(serviceItem);
        invoice.InvoiceItems.Add(medicineItem);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken);

        // Assert
        var updatedInvoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(150000, updatedInvoice.TotalAmount);
        Assert.Single(updatedInvoice.InvoiceItems);
        Assert.Equal(InvoiceItemType.Medicine, updatedInvoice.InvoiceItems.First().ItemType);
        Assert.False(await context.CaseClinicServices.AnyAsync(cs => cs.Id == junction.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TC_2_4_3_RemoveService_PaidInvoice_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = s.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);

        var paidInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            PaidAt = DateTime.UtcNow
        };
        context.Invoices.Add(paidInvoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể xóa dịch vụ khi hóa đơn đã thanh toán.", ex.Message);
    }

    [Fact]
    public async Task TC_2_4_3b_RemoveService_CaseBooked_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.Booked);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = s.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể xóa dịch vụ khỏi ca khám chưa check-in.", ex.Message);
    }

    [Fact]
    public async Task TC_2_4_3c_RemoveService_CaseEndOrCancelled_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (cEnd, s1) = SeedCaseAndService(context, CaseStatus.End);
        var (cCancelled, s2) = SeedCaseAndService(context, CaseStatus.Cancelled);

        var junctionEnd = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = cEnd.CaseId,
            ClinicServiceId = s1.Id,
            PriceAtTime = s1.Price,
            CreatedAt = DateTime.UtcNow
        };
        var junctionCancelled = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = cCancelled.CaseId,
            ClinicServiceId = s2.Id,
            PriceAtTime = s2.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.AddRange(junctionEnd, junctionCancelled);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - End
        var exEnd = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(cEnd.CaseId, junctionEnd.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể xóa dịch vụ khỏi ca khám đã hoàn thành hoặc bị hủy.", exEnd.Message);

        // Act & Assert - Cancelled
        var exCancelled = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(cCancelled.CaseId, junctionCancelled.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể xóa dịch vụ khỏi ca khám đã hoàn thành hoặc bị hủy.", exCancelled.Message);
    }

    [Fact]
    public async Task TC_2_4_4_RemoveService_NotFound_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, _) = SeedCaseAndService(context);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(c.CaseId, nonExistentId, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy dịch vụ.", ex.Message);
    }

    [Fact]
    public async Task TC_2_4_5_RemoveService_CaseIdMismatch_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c1, s) = SeedCaseAndService(context);
        var (c2, _) = SeedCaseAndService(context);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c1.CaseId, // Belongs to c1
            ClinicServiceId = s.Id,
            PriceAtTime = s.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert: Try removing for c2
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(c2.CaseId, junction.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy dịch vụ.", ex.Message);
    }

    [Fact]
    public async Task TC_2_4_6_RemoveService_LastItemOnPendingInvoice_AutoCancelsInvoice()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 100000);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow
        };
        var singleServiceItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = s.Name,
            Quantity = 1,
            UnitPrice = 100000,
            TotalPrice = 100000,
            ItemType = InvoiceItemType.Service,
            ReferenceId = junction.Id
        };
        invoice.InvoiceItems.Add(singleServiceItem);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken);

        // Assert
        var updatedInvoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.CANCELLED, updatedInvoice.Status);
        Assert.Equal(0, updatedInvoice.TotalAmount);
        Assert.Empty(updatedInvoice.InvoiceItems);
        Assert.NotNull(updatedInvoice.CancelledReason);
    }

    [Fact]
    public async Task TC_2_4_7_RemoveService_MixedInvoice_RemovesServiceOnlyRetainingMedicine()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, servicePrice: 120000);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = 120000,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 220000,
            CreatedAt = DateTime.UtcNow
        };
        var serviceItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = s.Name,
            Quantity = 1,
            UnitPrice = 120000,
            TotalPrice = 120000,
            ItemType = InvoiceItemType.Service,
            ReferenceId = junction.Id
        };
        var medicineItem = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = "Paracetamol",
            Quantity = 1,
            UnitPrice = 100000,
            TotalPrice = 100000,
            ItemType = InvoiceItemType.Medicine,
            ReferenceId = Guid.NewGuid()
        };
        invoice.InvoiceItems.Add(serviceItem);
        invoice.InvoiceItems.Add(medicineItem);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, TestContext.Current.CancellationToken);

        // Assert
        var updatedInvoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoice.Id, TestContext.Current.CancellationToken);
        Assert.Equal(InvoiceStatus.PENDING, updatedInvoice.Status);
        Assert.Equal(100000, updatedInvoice.TotalAmount);
        Assert.Single(updatedInvoice.InvoiceItems);
        Assert.Equal(medicineItem.Id, updatedInvoice.InvoiceItems.First().Id);
    }

    #endregion

    #region 2.5 GetServicesForCaseAsync (2 test cases)

    [Fact]
    public async Task TC_2_5_1_GetServicesForCase_Populated_ReturnsAllServicesWithSnapshot()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s1) = SeedCaseAndService(context, serviceCode: "SVC1", serviceName: "Dịch vụ 1", servicePrice: 100000);
        var s2 = new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "SVC2",
            Name = "Dịch vụ 2",
            Price = 200000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ClinicServices.Add(s2);

        var j1 = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s1.Id,
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var j2 = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s2.Id,
            PriceAtTime = 200000,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.AddRange(j1, j2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetServicesForCaseAsync(c.CaseId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("SVC1", result[0].ServiceCode);
        Assert.Equal("Dịch vụ 1", result[0].ServiceName);
        Assert.Equal(100000, result[0].PriceAtTime);

        Assert.Equal("SVC2", result[1].ServiceCode);
        Assert.Equal("Dịch vụ 2", result[1].ServiceName);
        Assert.Equal(200000, result[1].PriceAtTime);
    }

    [Fact]
    public async Task TC_2_5_2_GetServicesForCase_Empty_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var caseId = Guid.NewGuid();

        // Act
        var result = await service.GetServicesForCaseAsync(caseId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region 2.6 Doctor Authorization (Bác sĩ phụ trách ca)

    [Fact]
    public async Task AddService_ActingDoctorIsNotResponsibleDoctor_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.InProgress);
        var otherDoctorId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddServiceToCaseAsync(c.CaseId, s.Id, otherDoctorId, TestContext.Current.CancellationToken));
        Assert.Equal("Chỉ bác sĩ phụ trách ca khám mới có quyền chọn dịch vụ khám.", ex.Message);
    }

    [Fact]
    public async Task AddService_ActingDoctorIsResponsibleDoctor_Succeeds()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.InProgress);

        // Act
        var result = await service.AddServiceToCaseAsync(c.CaseId, s.Id, c.DoctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(c.CaseId, result.CaseId);
        Assert.Equal(s.Id, result.ClinicServiceId);
    }

    [Fact]
    public async Task RemoveService_ActingDoctorIsNotResponsibleDoctor_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.InProgress);
        var otherDoctorId = Guid.NewGuid();

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = s.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, otherDoctorId, TestContext.Current.CancellationToken));
        Assert.Equal("Chỉ bác sĩ phụ trách ca khám mới có quyền xóa dịch vụ khám.", ex.Message);
    }

    [Fact]
    public async Task RemoveService_ActingDoctorIsResponsibleDoctor_Succeeds()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var (c, s) = SeedCaseAndService(context, CaseStatus.InProgress);

        var junction = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = c.CaseId,
            ClinicServiceId = s.Id,
            PriceAtTime = s.Price,
            CreatedAt = DateTime.UtcNow
        };
        context.CaseClinicServices.Add(junction);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.RemoveServiceFromCaseAsync(c.CaseId, junction.Id, c.DoctorId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(await context.CaseClinicServices.AnyAsync(cs => cs.Id == junction.Id, TestContext.Current.CancellationToken));
    }

    #endregion
}
