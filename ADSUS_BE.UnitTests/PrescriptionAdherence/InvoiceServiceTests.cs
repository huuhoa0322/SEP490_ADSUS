using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
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
}
