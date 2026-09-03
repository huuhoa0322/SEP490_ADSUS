using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public class InvoiceServiceTests
{
    private DbContextOptions<AppDbContext> GetInMemoryOptions(string dbName)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_ExactPackaging_ShouldCreateInvoiceItemsWithoutRounding()
    {
        // Arrange
        var options = GetInMemoryOptions("Invoice_Test_Exact");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var pillUnitId = Guid.NewGuid();
        var blisterUnitId = Guid.NewGuid();

        var pillUnit = new MedicineUnit { MedicineUnitId = pillUnitId, Name = "Viên" };
        var blisterUnit = new MedicineUnit { MedicineUnitId = blisterUnitId, Name = "Vỉ" };
        
        var medicine = new Medicine { MedicineId = medicineId, Name = "Amoxicillin", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
        
        var blisterPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = blisterUnitId, ConversionFactor = 10, IsBaseUnit = false, IsSellable = true, SalePrice = 50000, Medicine = medicine, MedicineUnit = blisterUnit };
        var pillPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = pillUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 6000, Medicine = medicine, MedicineUnit = pillUnit };

        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 20, Medicine = medicine, Dosage = "1 viên" };

        context.MedicineUnits.AddRange(pillUnit, blisterUnit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.AddRange(blisterPack, pillPack);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync();

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(caseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(InvoiceStatus.PENDING, invoice.Status);
        Assert.Single(invoice.InvoiceItems); // Should only have 1 item: 2 Vỉ
        
        var item = invoice.InvoiceItems.First();
        Assert.Equal(2, item.Quantity);
        Assert.Equal(50000, item.UnitPrice);
        Assert.Equal(100000, item.TotalPrice);
        Assert.Equal(100000, invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_UsageUnit_ShouldCalculateUsingVolumePerBaseUnit()
    {
        // Arrange
        var options = GetInMemoryOptions("Invoice_Test_UsageUnit");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var packUnitId = Guid.NewGuid();

        var packUnit = new MedicineUnit { MedicineUnitId = packUnitId, Name = "Gói" };
        
        // VolumePerBaseUnit = 50: 1 Gói = 50 Viên
        var medicine = new Medicine { MedicineId = medicineId, Name = "Atox 250g", UsageUnit = "Viên", VolumePerBaseUnit = 50 };
        
        // BaseUnit = Gói, CF = 1
        var packPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = packUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 100000, Medicine = medicine, MedicineUnit = packUnit };

        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
        // QuantityBase = 4 (Viên)
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 4, Medicine = medicine, Dosage = "1 viên" };

        context.MedicineUnits.Add(packUnit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(packPack);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync();

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(caseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId);
        Assert.Single(invoice.InvoiceItems);
        
        var item = invoice.InvoiceItems.First();
        // 4 Viên < 50 Viên (1 Gói) -> rounds up to 1 Gói
        Assert.Equal(1, item.Quantity);
        Assert.Equal(100000, item.UnitPrice);
        Assert.Equal(100000, item.TotalPrice);
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_NeedsRounding_ShouldCreateMultipleItemsAndCeil()
    {
        // Arrange
        var options = GetInMemoryOptions("Invoice_Test_RoundUp");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var pillUnitId = Guid.NewGuid();
        var blisterUnitId = Guid.NewGuid();

        var pillUnit = new MedicineUnit { MedicineUnitId = pillUnitId, Name = "Viên" };
        var blisterUnit = new MedicineUnit { MedicineUnitId = blisterUnitId, Name = "Vỉ" };
        
        var medicine = new Medicine { MedicineId = medicineId, Name = "Paracetamol", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
        
        var blisterPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = blisterUnitId, ConversionFactor = 10, IsBaseUnit = false, IsSellable = true, SalePrice = 58000, Medicine = medicine, MedicineUnit = blisterUnit };
        var pillPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = pillUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 6000, Medicine = medicine, MedicineUnit = pillUnit };

        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
        // 15 viên = 1 Vỉ + 5 viên lẻ
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 15, Medicine = medicine, Dosage = "1 viên" };

        context.MedicineUnits.AddRange(pillUnit, blisterUnit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.AddRange(blisterPack, pillPack);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync();

        // Act
        var invoiceId = await service.GenerateInvoiceForCaseAsync(caseId);

        // Assert
        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(2, invoice.InvoiceItems.Count); // 1 item cho Vỉ, 1 item cho Viên
        
        var blisterItem = invoice.InvoiceItems.First(i => i.Description.Contains("Vỉ"));
        Assert.Equal(1, blisterItem.Quantity);
        Assert.Equal(58000, blisterItem.TotalPrice);

        var pillItem = invoice.InvoiceItems.First(i => i.Description.Contains("Viên"));
        Assert.Equal(5, pillItem.Quantity);
        Assert.Equal(30000, pillItem.TotalPrice);

        Assert.Equal(88000, invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_UsageUnit_RemainderMergedIntoSamePack_ShouldBeSingleRow()
    {
        // Scenario: 54 Viên, BS=Gói (VolumePerBaseUnit=50)
        // Greedy: 54 / 50 = 1 Gói (remainder 4)
        // Remainder 4 < 50 → round up → merge into Gói row: total 2 Gói (not 1 Gói + "(Làm tròn lên)" Gói)
        var options = GetInMemoryOptions("Invoice_Test_RemainderMerge");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var packUnitId = Guid.NewGuid();

        var packUnit = new MedicineUnit { MedicineUnitId = packUnitId, Name = "Gói" };
        var medicine = new Medicine { MedicineId = medicineId, Name = "Atox 250g", UsageUnit = "Viên", VolumePerBaseUnit = 50 };
        var packPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = packUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 100000, Medicine = medicine, MedicineUnit = packUnit };

        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 54, Medicine = medicine, Dosage = "1 viên" };

        context.MedicineUnits.Add(packUnit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(packPack);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync();

        var invoiceId = await service.GenerateInvoiceForCaseAsync(caseId);

        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId);
        // Must be exactly 1 row (no separate "(Làm tròn lên)" row)
        Assert.Single(invoice.InvoiceItems);
        var item = invoice.InvoiceItems.Single();
        Assert.Equal(2, item.Quantity);        // ceil(54/50) = 2
        Assert.Equal(100000, item.UnitPrice);
        Assert.Equal(200000, item.TotalPrice);
        Assert.Equal(200000, invoice.TotalAmount);
        Assert.DoesNotContain("Làm tròn lên", item.Description);
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_MultiLevel_RemainderMergedIntoSmallestPack()
    {
        // Scenario: 154 Viên, BS=Gói (VolumePerBaseUnit=50), also Hộp(5 Gói) sellable
        // Greedy: Hộp capacity = 5*50 = 250. 154 < 250 → 0 Hộp
        //         Gói capacity = 1*50 = 50.  154 / 50 = 3 Gói, remainder = 4
        //         Remainder 4 > 0 → merge 1 into Gói row → 4 Gói total, single row
        var options = GetInMemoryOptions("Invoice_Test_MultiLevelMerge");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var packUnitId = Guid.NewGuid();
        var boxUnitId  = Guid.NewGuid();

        var packUnit = new MedicineUnit { MedicineUnitId = packUnitId, Name = "Gói" };
        var boxUnit  = new MedicineUnit { MedicineUnitId = boxUnitId,  Name = "Hộp" };
        var medicine = new Medicine { MedicineId = medicineId, Name = "Atox 250g", UsageUnit = "Viên", VolumePerBaseUnit = 50 };

        var boxPack  = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = boxUnitId,  ConversionFactor = 5, IsBaseUnit = false, IsSellable = true, SalePrice = 450000, Medicine = medicine, MedicineUnit = boxUnit };
        var packPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = packUnitId, ConversionFactor = 1, IsBaseUnit = true,  IsSellable = true, SalePrice = 100000, Medicine = medicine, MedicineUnit = packUnit };

        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 154, Medicine = medicine, Dosage = "1 viên" };

        context.MedicineUnits.AddRange(packUnit, boxUnit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.AddRange(boxPack, packPack);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync();

        var invoiceId = await service.GenerateInvoiceForCaseAsync(caseId);

        var invoice = await context.Invoices.Include(i => i.InvoiceItems).FirstAsync(i => i.Id == invoiceId);
        // Only 1 row (all Gói, no Hộp since 154 < 250)
        Assert.Single(invoice.InvoiceItems);
        var item = invoice.InvoiceItems.Single();
        Assert.DoesNotContain("Làm tròn lên", item.Description);
        Assert.Equal(4, item.Quantity); // 3 Gói from greedy + 1 for remainder
        Assert.Equal(100000, item.UnitPrice);
        Assert.Equal(400000, item.TotalPrice);
        Assert.Equal(400000, invoice.TotalAmount);
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_NoSellablePackaging_ShouldThrowBusinessException()
    {
        // Arrange
        var options = GetInMemoryOptions("Invoice_Test_Exception");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();
        var pillUnitId = Guid.NewGuid();

        var pillUnit = new MedicineUnit { MedicineUnitId = pillUnitId, Name = "Viên" };
        var medicine = new Medicine { MedicineId = medicineId, Name = "Paracetamol", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
        
        // IsSellable = false
        var pillPack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = pillUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = false, SalePrice = 6000, Medicine = medicine, MedicineUnit = pillUnit };

        var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
        var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 15, Medicine = medicine, Dosage = "1 viên" };

        context.MedicineUnits.Add(pillUnit);
        context.Medicines.Add(medicine);
        context.MedicinePackagings.Add(pillPack);
        context.Prescriptions.Add(prescription);
        context.PrescriptionItems.Add(pItem);
        await context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => service.GenerateInvoiceForCaseAsync(caseId));
    }

    // ─────────────────────────────────────────────────────────────────────
    // GenerateInvoiceForCaseAsync — edge cases
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_NoPrescription_ShouldThrowBusinessException()
    {
        // Arrange — CaseId không có Prescription nào
        var options = GetInMemoryOptions("Invoice_Test_NoPrescription");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.GenerateInvoiceForCaseAsync(caseId));
        Assert.Contains("Không tìm thấy đơn thuốc", ex.Message);
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_AlreadyHasPendingInvoice_ShouldReturnExistingId()
    {
        // Arrange — Case đã có Invoice PENDING → idempotent, trả lại ID cũ
        var options = GetInMemoryOptions("Invoice_Test_Idempotent");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var existingInvoiceId = Guid.NewGuid();

        context.Invoices.Add(new Invoice
        {
            Id = existingInvoiceId,
            CaseId = caseId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var returnedId = await service.GenerateInvoiceForCaseAsync(caseId);

        // Assert — phải trả về đúng ID cũ, không tạo mới
        Assert.Equal(existingInvoiceId, returnedId);
        Assert.Equal(1, context.Invoices.Count()); // Vẫn chỉ 1 invoice
    }

    [Fact]
    public async Task GenerateInvoiceForCaseAsync_AlreadyHasPaidInvoice_ShouldReturnExistingId()
    {
        // Arrange — Case đã có Invoice PAID → idempotent
        var options = GetInMemoryOptions("Invoice_Test_IdempotentPaid");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var caseId = Guid.NewGuid();
        var existingInvoiceId = Guid.NewGuid();

        context.Invoices.Add(new Invoice
        {
            Id = existingInvoiceId,
            CaseId = caseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 100000,
            CreatedAt = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var returnedId = await service.GenerateInvoiceForCaseAsync(caseId);

        // Assert
        Assert.Equal(existingInvoiceId, returnedId);
        Assert.Equal(1, context.Invoices.Count());
    }

    // ─────────────────────────────────────────────────────────────────────
    // GetInvoiceDetailAsync — không tìm thấy hóa đơn
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvoiceDetailAsync_NotFound_ShouldThrowBusinessException()
    {
        var options = GetInMemoryOptions("Invoice_Test_GetDetail_NotFound");
        using var context = new AppDbContext(options);
        var service = new InvoiceService(context, null!);

        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.GetInvoiceDetailAsync(nonExistentId));
        Assert.Contains("Không tìm thấy hóa đơn", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PayAndDispenseAsync — các nhánh lỗi
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PayAndDispenseAsync_InvoiceNotFound_ShouldThrowBusinessException()
    {
        var options = GetInMemoryOptions("Invoice_Test_Pay_NotFound");
        using var context = new AppDbContext(options);
        var inventoryMock = new Moq.Mock<ADSUS_BE.BLL.PrescriptionAdherence.Interfaces.IInventoryService>();
        var service = new InvoiceService(context, inventoryMock.Object);

        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.PayAndDispenseAsync(nonExistentId, PaymentMethod.CASH));
        Assert.Contains("Không tìm thấy hóa đơn", ex.Message);
    }

    [Fact]
    public async Task PayAndDispenseAsync_AlreadyPaid_ShouldThrowBusinessException()
    {
        var options = GetInMemoryOptions("Invoice_Test_Pay_AlreadyPaid");
        using var context = new AppDbContext(options);
        var inventoryMock = new Moq.Mock<ADSUS_BE.BLL.PrescriptionAdherence.Interfaces.IInventoryService>();
        var service = new InvoiceService(context, inventoryMock.Object);

        var invoiceId = Guid.NewGuid();
        var caseId   = Guid.NewGuid();

        context.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CaseId = caseId,
            Status = InvoiceStatus.PAID,      // Đã thanh toán rồi
            TotalAmount = 80000,
            CreatedAt = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.PayAndDispenseAsync(invoiceId, PaymentMethod.CASH));
        Assert.Contains("đã được thanh toán", ex.Message);

        // Đảm bảo DispenseAsync KHÔNG được gọi khi đã paid
        inventoryMock.Verify(
            s => s.DispenseAsync(It.IsAny<Guid>()),
            Moq.Times.Never);
    }

    [Fact]
    public async Task CancelInvoiceAsync_Pending_Success()
    {
        var options = GetInMemoryOptions("Invoice_Test_Cancel_Pending");
        using var context = new AppDbContext(options);
        var inventoryMock = new Moq.Mock<ADSUS_BE.BLL.PrescriptionAdherence.Interfaces.IInventoryService>();
        var service = new InvoiceService(context, inventoryMock.Object);

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 50000
        });
        await context.SaveChangesAsync();

        var request = new CancelInvoiceRequest { Reason = "Bệnh nhân đổi ý" };
        await service.CancelInvoiceAsync(invoiceId, request);

        var invoice = await context.Invoices.FindAsync(invoiceId);
        Assert.Equal(InvoiceStatus.CANCELLED, invoice.Status);
        Assert.Equal("Bệnh nhân đổi ý", invoice.CancelledReason);
    }

    [Fact]
    public async Task CancelInvoiceAsync_Paid_ReverseDispense()
    {
        var options = GetInMemoryOptions("Invoice_Test_Cancel_Paid");
        using var context = new AppDbContext(options);
        var inventoryMock = new Moq.Mock<ADSUS_BE.BLL.PrescriptionAdherence.Interfaces.IInventoryService>();
        var service = new InvoiceService(context, inventoryMock.Object);

        var caseId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var prescriptionId = Guid.NewGuid();
        var pItemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        context.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CaseId = caseId,
            Status = InvoiceStatus.PAID,
            TotalAmount = 50000
        });

        context.Prescriptions.Add(new Prescription
        {
            PrescriptionId = prescriptionId,
            CaseId = caseId,
            Status = PrescriptionStatus.Active
        });

        context.PrescriptionItems.Add(new PrescriptionItem
        {
            PrescriptionItemId = pItemId,
            PrescriptionId = prescriptionId,
            Dosage = "1 viên"
        });

        context.MedicineBatches.Add(new MedicineBatch
        {
            Id = batchId,
            QuantityBase = 50,
            LotNumber = "LOT123"
        });

        context.InventoryTransactions.Add(new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            TxnType = InventoryTxnType.Dispense,
            QuantityInUnit = 2,
            QuantityBase = 20,
            PrescriptionItemId = pItemId
        });

        await context.SaveChangesAsync();

        var request = new CancelInvoiceRequest { Reason = "Nhầm lẫn kê đơn" };
        await service.CancelInvoiceAsync(invoiceId, request);

        var invoice = await context.Invoices.FindAsync(invoiceId);
        Assert.Equal(InvoiceStatus.CANCELLED, invoice.Status);
        Assert.Equal("Nhầm lẫn kê đơn", invoice.CancelledReason);

        var batch = await context.MedicineBatches.FindAsync(batchId);
        Assert.Equal(70, batch.QuantityBase); // 50 + 20

        var reverseTxn = await context.InventoryTransactions
            .FirstOrDefaultAsync(t => t.TxnType == InventoryTxnType.Adjustment && t.Reason == "Hoàn kho tự động do hủy hóa đơn");
        Assert.NotNull(reverseTxn);
        Assert.Equal(20, reverseTxn.QuantityBase);
        Assert.Equal(pItemId, reverseTxn.PrescriptionItemId);
    }

    [Fact]
    public async Task CancelInvoiceAsync_NotFound()
    {
        var options = GetInMemoryOptions("Invoice_Test_Cancel_NotFound");
        using var context = new AppDbContext(options);
        var inventoryMock = new Moq.Mock<ADSUS_BE.BLL.PrescriptionAdherence.Interfaces.IInventoryService>();
        var service = new InvoiceService(context, inventoryMock.Object);

        var request = new CancelInvoiceRequest { Reason = "Lý do" };
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CancelInvoiceAsync(Guid.NewGuid(), request));
        Assert.Contains("Không tìm thấy", ex.Message);
    }

    [Fact]
    public async Task CancelInvoiceAsync_AlreadyCancelled()
    {
        var options = GetInMemoryOptions("Invoice_Test_Cancel_Already");
        using var context = new AppDbContext(options);
        var inventoryMock = new Moq.Mock<ADSUS_BE.BLL.PrescriptionAdherence.Interfaces.IInventoryService>();
        var service = new InvoiceService(context, inventoryMock.Object);

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            Status = InvoiceStatus.CANCELLED
        });
        await context.SaveChangesAsync();

        var request = new CancelInvoiceRequest { Reason = "Lý do" };
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CancelInvoiceAsync(invoiceId, request));
        Assert.Contains("đã bị hủy", ex.Message);
    }
}
