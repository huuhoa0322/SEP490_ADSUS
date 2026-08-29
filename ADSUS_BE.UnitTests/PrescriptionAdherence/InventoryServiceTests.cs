using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence
{
    public class InventoryServiceTests
    {
        private readonly AppDbContext _dbContext;
        private readonly Mock<ILogger<InventoryService>> _loggerMock;
        private readonly InventoryService _service;

        public InventoryServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AppDbContext(options);
            _loggerMock = new Mock<ILogger<InventoryService>>();
            _service = new InventoryService(_dbContext, _loggerMock.Object);
        }

        [Fact]
        public async Task ImportMedicineAsync_NewLot_Success()
        {
            // Arrange
            var medicineId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var packagingId = Guid.NewGuid();

            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Test Med", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = supplierId, Name = "Test Sup", IsActive = true, PhoneNumber = "0123", Email = "a@a", Address = "b", TaxCode = "c" });
            _dbContext.MedicinePackagings.Add(new MedicinePackaging { Id = packagingId, MedicineId = medicineId, ConversionFactor = 10 });
            await _dbContext.SaveChangesAsync();

            var request = new ImportInventoryRequest
            {
                MedicineId = medicineId,
                SupplierId = supplierId,
                MedicinePackagingId = packagingId,
                LotNumber = "LOT-123",
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                Quantity = 5,
                ImportPricePerUnit = 100000 // 100k for 1 Box (10 Base units => 10k/unit)
            };

            // Act
            await _service.ImportMedicineAsync(request);

            // Assert
            var batch = await _dbContext.MedicineBatches.FirstOrDefaultAsync(b => b.LotNumber == "LOT-123");
            Assert.NotNull(batch);
            Assert.Equal(50, batch.QuantityBase); // 5 * 10
            Assert.Equal(10000, batch.BaseUnitAvgImportPrice); // 100000 / 10

            var txn = await _dbContext.InventoryTransactions.FirstOrDefaultAsync(t => t.BatchId == batch.Id);
            Assert.NotNull(txn);
            Assert.Equal(InventoryTxnType.Import, txn.TxnType);
            Assert.Equal(5, txn.QuantityInUnit);
            Assert.Equal(50, txn.QuantityBase);
        }

        [Fact]
        public async Task ImportMedicineAsync_ExistingLot_SameExpiry_UpdatesQuantityAndPrice()
        {
            // Arrange
            var medicineId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var packagingId = Guid.NewGuid();
            var expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Test Med", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = supplierId, Name = "Test Sup", IsActive = true, PhoneNumber = "0123", Email = "a@a", Address = "b", TaxCode = "c" });
            _dbContext.MedicinePackagings.Add(new MedicinePackaging { Id = packagingId, MedicineId = medicineId, ConversionFactor = 10 });
            
            // Existing Batch: 10 base units, 5,000 unit import price
            _dbContext.MedicineBatches.Add(new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOT-123",
                ExpiryDate = expiry,
                QuantityBase = 10,
                BaseUnitAvgImportPrice = 5000
            });
            await _dbContext.SaveChangesAsync();

            var request = new ImportInventoryRequest
            {
                MedicineId = medicineId,
                SupplierId = supplierId,
                MedicinePackagingId = packagingId,
                LotNumber = "LOT-123",
                ExpiryDate = DateTime.UtcNow.AddDays(30), // Must be exactly same date
                Quantity = 4, // 4 * 10 = 40 base units, price = 200,000 / 10 = 20,000/box => 2,000/base
                ImportPricePerUnit = 20000 
            };

            // Act
            await _service.ImportMedicineAsync(request);

            // Assert
            var batch = await _dbContext.MedicineBatches.FirstOrDefaultAsync(b => b.LotNumber == "LOT-123");
            Assert.NotNull(batch);
            Assert.Equal(50, batch.QuantityBase); // 10 + 40
            
            // Weighted avg price: (10 * 5000 + 40 * 2000) / 50 = (50000 + 80000) / 50 = 130000 / 50 = 2600
            Assert.Equal(2600, batch.BaseUnitAvgImportPrice); 
        }

        [Fact]
        public async Task ImportMedicineAsync_ExistingLot_DifferentExpiry_ThrowsException()
        {
            // Arrange
            var medicineId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var packagingId = Guid.NewGuid();

            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Test Med", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = supplierId, Name = "Test Sup", IsActive = true, PhoneNumber = "0123", Email = "a@a", Address = "b", TaxCode = "c" });
            _dbContext.MedicinePackagings.Add(new MedicinePackaging { Id = packagingId, MedicineId = medicineId, ConversionFactor = 10 });
            
            // Existing Batch
            _dbContext.MedicineBatches.Add(new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOT-123",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                QuantityBase = 10,
                BaseUnitAvgImportPrice = 5000
            });
            await _dbContext.SaveChangesAsync();

            var request = new ImportInventoryRequest
            {
                MedicineId = medicineId,
                SupplierId = supplierId,
                MedicinePackagingId = packagingId,
                LotNumber = "LOT-123",
                ExpiryDate = DateTime.UtcNow.AddDays(40), // Different Expiry
                Quantity = 4,
                ImportPricePerUnit = 20000 
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ImportMedicineAsync(request));
            Assert.Contains("Hạn sử dụng", ex.Message);
        }

        [Fact]
        public async Task ImportMedicineAsync_InvalidExpiry_PastDate_ThrowsException()
        {
            // Arrange
            var request = new ImportInventoryRequest
            {
                MedicineId = Guid.NewGuid(),
                SupplierId = Guid.NewGuid(),
                MedicinePackagingId = Guid.NewGuid(),
                LotNumber = "LOT-123",
                ExpiryDate = DateTime.UtcNow.AddDays(-1), // Past date
                Quantity = 4,
                ImportPricePerUnit = 20000 
            };
            
            _dbContext.Medicines.Add(new Medicine { MedicineId = request.MedicineId, Name = "M", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = request.SupplierId, Name = "S", IsActive = true, PhoneNumber = "", Email = "", Address = "", TaxCode = "" });
            await _dbContext.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ImportMedicineAsync(request));
            Assert.Contains("Hạn sử dụng phải lớn hơn", ex.Message);
        }
        [Fact]
        public async Task GetInventoryHistoryAsync_WithSearch_ReturnsMatchingRecords()
        {
            // Arrange
            var med1 = new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow };
            var med2 = new Medicine { MedicineId = Guid.NewGuid(), Name = "Aspirin", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow };
            var supplier = new Supplier { SupplierId = Guid.NewGuid(), Name = "Hau Giang Pharma", IsActive = true, PhoneNumber = "", Email = "", Address = "", TaxCode = "" };
            var unit = new MedicineUnit { MedicineUnitId = Guid.NewGuid(), Name = "Hộp" };
            var pack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = med1.MedicineId, MedicineUnitId = unit.MedicineUnitId, ConversionFactor = 10 };
            
            _dbContext.Medicines.AddRange(med1, med2);
            _dbContext.Suppliers.Add(supplier);
            _dbContext.MedicineUnits.Add(unit);
            _dbContext.MedicinePackagings.Add(pack);

            var batch1 = new MedicineBatch { Id = Guid.NewGuid(), MedicineId = med1.MedicineId, LotNumber = "LOT-1", QuantityBase = 100, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)) };
            var batch2 = new MedicineBatch { Id = Guid.NewGuid(), MedicineId = med2.MedicineId, LotNumber = "LOT-2", QuantityBase = 50, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)) };
            _dbContext.MedicineBatches.AddRange(batch1, batch2);

            _dbContext.InventoryTransactions.Add(new InventoryTransaction { Id = Guid.NewGuid(), BatchId = batch1.Id, MedicinePackagingId = pack.Id, QuantityInUnit = 10, QuantityBase = 100, TxnType = InventoryTxnType.Import, TxnDate = DateTime.UtcNow, SupplierId = supplier.SupplierId });
            _dbContext.InventoryTransactions.Add(new InventoryTransaction { Id = Guid.NewGuid(), BatchId = batch2.Id, MedicinePackagingId = pack.Id, QuantityInUnit = 5, QuantityBase = 50, TxnType = InventoryTxnType.Import, TxnDate = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var filter = new InventoryHistoryFilter { Search = "Paracetamol" };

            // Act
            var result = await _service.GetInventoryHistoryAsync(filter);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.TotalItems);
            Assert.Equal("Paracetamol", result.Items.First().MedicineName);
            Assert.Equal("LOT-1", result.Items.First().LotNumber);
            Assert.Equal("Hau Giang Pharma", result.Items.First().SupplierName);
        }
        [Fact]
        public async Task ProcessBulkImportAsync_ValidationFails_ThrowsExceptionWithRowIndex()
        {
            // Arrange
            var medicineId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var packagingId = Guid.NewGuid();
            var unitId = Guid.NewGuid();

            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Test Med", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = supplierId, Name = "Test Sup", IsActive = true, PhoneNumber = "1", Email = "a", Address = "a", TaxCode = "1" });
            _dbContext.MedicineUnits.Add(new MedicineUnit { MedicineUnitId = unitId, Name = "Unit" });
            _dbContext.MedicinePackagings.Add(new MedicinePackaging { Id = packagingId, MedicineId = medicineId, MedicineUnitId = unitId, ConversionFactor = 10 });
            await _dbContext.SaveChangesAsync();

            var requests = new System.Collections.Generic.List<ImportInventoryRequest>
            {
                new ImportInventoryRequest
                {
                    MedicineId = medicineId, SupplierId = supplierId, MedicinePackagingId = packagingId,
                    LotNumber = "LOT-1", ExpiryDate = DateTime.UtcNow.AddDays(30), Quantity = 1, ImportPricePerUnit = 1000
                },
                new ImportInventoryRequest
                {
                    MedicineId = medicineId, SupplierId = supplierId, MedicinePackagingId = packagingId,
                    LotNumber = "LOT-2", ExpiryDate = DateTime.UtcNow.AddDays(-1), Quantity = 1, ImportPricePerUnit = 1000
                } // Invalid row (index 1 => Row 2)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ImportMedicineBulkAsync(requests));
            Assert.Contains("Lỗi ở Hàng số 2", ex.Message);
            Assert.Contains("Hạn sử dụng", ex.Message);
        }

        [Fact]
        public async Task ValidateImportAsync_DuplicateLot_DifferentMedicine_ReturnsInvalid()
        {
            // Arrange
            var medicineId1 = Guid.NewGuid();
            var medicineId2 = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var packagingId = Guid.NewGuid();
            var unitId = Guid.NewGuid();

            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId1, Name = "Med 1", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId2, Name = "Med 2", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = supplierId, Name = "Sup", IsActive = true, PhoneNumber = "1", Email = "a", Address = "a", TaxCode = "1" });
            _dbContext.MedicineUnits.Add(new MedicineUnit { MedicineUnitId = unitId, Name = "Unit" });
            _dbContext.MedicinePackagings.Add(new MedicinePackaging { Id = packagingId, MedicineId = medicineId2, MedicineUnitId = unitId, ConversionFactor = 1 });
            
            _dbContext.MedicineBatches.Add(new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId1, // Belongs to Med 1
                LotNumber = "LOT-SHARED",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                QuantityBase = 10
            });
            await _dbContext.SaveChangesAsync();

            var request = new ImportInventoryRequest
            {
                MedicineId = medicineId2, // Try to import for Med 2
                SupplierId = supplierId,
                MedicinePackagingId = packagingId,
                LotNumber = "LOT-SHARED",
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                Quantity = 1,
                ImportPricePerUnit = 100
            };

            // Act
            var result = await _service.ValidateImportAsync(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("đã được sử dụng cho một loại thuốc khác", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateImportAsync_DuplicateLot_DifferentExpiry_ReturnsInvalid()
        {
            // Arrange
            var medicineId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var packagingId = Guid.NewGuid();
            var unitId = Guid.NewGuid();

            _dbContext.Medicines.Add(new Medicine { MedicineId = medicineId, Name = "Med", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow });
            _dbContext.Suppliers.Add(new Supplier { SupplierId = supplierId, Name = "Sup", IsActive = true, PhoneNumber = "1", Email = "a", Address = "a", TaxCode = "1" });
            _dbContext.MedicineUnits.Add(new MedicineUnit { MedicineUnitId = unitId, Name = "Unit" });
            _dbContext.MedicinePackagings.Add(new MedicinePackaging { Id = packagingId, MedicineId = medicineId, MedicineUnitId = unitId, ConversionFactor = 1 });
            
            _dbContext.MedicineBatches.Add(new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOT-SAME",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                QuantityBase = 10
            });
            await _dbContext.SaveChangesAsync();

            var request = new ImportInventoryRequest
            {
                MedicineId = medicineId,
                SupplierId = supplierId,
                MedicinePackagingId = packagingId,
                LotNumber = "LOT-SAME",
                ExpiryDate = DateTime.UtcNow.AddDays(50), // Different expiry
                Quantity = 1,
                ImportPricePerUnit = 100
            };

            // Act
            var result = await _service.ValidateImportAsync(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("khác Hạn sử dụng", result.ErrorMessage);
        }

        [Fact]
        public async Task DispenseAsync_NormalCase_ShouldDeductBatchAndFreezeCOGS()
        {
            // Arrange
            var caseId = Guid.NewGuid();
            var medicineId = Guid.NewGuid();
            var baseUnitId = Guid.NewGuid();

            var baseUnit = new MedicineUnit { MedicineUnitId = baseUnitId, Name = "Viên" };
            var medicine = new Medicine { MedicineId = medicineId, Name = "Amoxicillin", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
            var basePack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = baseUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 6000, Medicine = medicine, MedicineUnit = baseUnit };

            var batch = new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOT1",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(5)),
                QuantityBase = 100,
                BaseUnitAvgImportPrice = 4500, // COGS to freeze
                Medicine = medicine
            };

            var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
            var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 20, Medicine = medicine, Dosage = "1 viên" };

            _dbContext.MedicineUnits.Add(baseUnit);
            _dbContext.Medicines.Add(medicine);
            _dbContext.MedicinePackagings.Add(basePack);
            _dbContext.MedicineBatches.Add(batch);
            _dbContext.Prescriptions.Add(prescription);
            _dbContext.PrescriptionItems.Add(pItem);
            await _dbContext.SaveChangesAsync();

            // Act
            await _service.DispenseAsync(caseId);

            // Assert
            var updatedBatch = await _dbContext.MedicineBatches.FirstAsync(b => b.Id == batch.Id);
            Assert.Equal(80, updatedBatch.QuantityBase); // 100 - 20 = 80

            var txn = await _dbContext.InventoryTransactions.FirstAsync(t => t.BatchId == batch.Id);
            Assert.Equal(20, txn.QuantityBase);
            Assert.Equal(20, txn.QuantityInUnit);
            Assert.Equal(basePack.Id, txn.MedicinePackagingId);
            Assert.Equal(4500, txn.ActualImportPrice); // COGS Frozen
            Assert.Equal(InventoryTxnType.Dispense, txn.TxnType);
        }

        [Fact]
        public async Task DispenseAsync_CrossBatch_FEFOSorting_ShouldDeductOldestFirst()
        {
            // Arrange
            var caseId = Guid.NewGuid();
            var medicineId = Guid.NewGuid();
            var baseUnitId = Guid.NewGuid();

            var baseUnit = new MedicineUnit { MedicineUnitId = baseUnitId, Name = "Viên" };
            var medicine = new Medicine { MedicineId = medicineId, Name = "Paracetamol", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
            var basePack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = baseUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 6000, Medicine = medicine, MedicineUnit = baseUnit };

            var batchOld = new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOTO",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), // Cận date hơn
                QuantityBase = 10, // Chỉ có 10 viên
                BaseUnitAvgImportPrice = 4000,
                Medicine = medicine
            };

            var batchNew = new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOTN",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10)), // Xa date
                QuantityBase = 50,
                BaseUnitAvgImportPrice = 5000,
                Medicine = medicine
            };

            var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
            // Đơn 15 viên -> Lấy 10 viên lô cũ, 5 viên lô mới
            var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 15, Medicine = medicine, Dosage = "1 viên" };

            _dbContext.MedicineUnits.Add(baseUnit);
            _dbContext.Medicines.Add(medicine);
            _dbContext.MedicinePackagings.Add(basePack);
            _dbContext.MedicineBatches.AddRange(batchNew, batchOld); // Insert không theo thứ tự
            _dbContext.Prescriptions.Add(prescription);
            _dbContext.PrescriptionItems.Add(pItem);
            await _dbContext.SaveChangesAsync();

            // Act
            await _service.DispenseAsync(caseId);

            // Assert
            var oldBatchResult = await _dbContext.MedicineBatches.FirstAsync(b => b.Id == batchOld.Id);
            Assert.Equal(0, oldBatchResult.QuantityBase); // Hết sạch lô cũ

            var newBatchResult = await _dbContext.MedicineBatches.FirstAsync(b => b.Id == batchNew.Id);
            Assert.Equal(45, newBatchResult.QuantityBase); // Lô mới bị trừ 5

            var txns = await _dbContext.InventoryTransactions.Where(t => t.PrescriptionItemId == pItem.PrescriptionItemId).ToListAsync();
            Assert.Equal(2, txns.Count); // Sinh 2 giao dịch

            var txnOld = txns.First(t => t.BatchId == batchOld.Id);
            Assert.Equal(10, txnOld.QuantityBase);
            Assert.Equal(4000, txnOld.ActualImportPrice);

            var txnNew = txns.First(t => t.BatchId == batchNew.Id);
            Assert.Equal(5, txnNew.QuantityBase);
            Assert.Equal(5000, txnNew.ActualImportPrice);
        }

        [Fact]
        public async Task DispenseAsync_NotEnoughStock_ShouldThrowException()
        {
            // Arrange
            var caseId = Guid.NewGuid();
            var medicineId = Guid.NewGuid();
            var baseUnitId = Guid.NewGuid();

            var baseUnit = new MedicineUnit { MedicineUnitId = baseUnitId, Name = "Viên" };
            var medicine = new Medicine { MedicineId = medicineId, Name = "Aspirin", UsageUnit = "Viên", VolumePerBaseUnit = 1 };
            var basePack = new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medicineId, MedicineUnitId = baseUnitId, ConversionFactor = 1, IsBaseUnit = true, IsSellable = true, SalePrice = 6000, Medicine = medicine, MedicineUnit = baseUnit };

            var batch = new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = medicineId,
                LotNumber = "LOTA",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(5)),
                QuantityBase = 5, // Chỉ có 5
                BaseUnitAvgImportPrice = 4500,
                Medicine = medicine
            };

            var prescription = new Prescription { PrescriptionId = Guid.NewGuid(), CaseId = caseId, Status = PrescriptionStatus.Active };
            var pItem = new PrescriptionItem { PrescriptionItemId = Guid.NewGuid(), PrescriptionId = prescription.PrescriptionId, MedicineId = medicineId, QuantityBase = 10, Medicine = medicine, Dosage = "1 viên" }; // Cần 10

            _dbContext.MedicineUnits.Add(baseUnit);
            _dbContext.Medicines.Add(medicine);
            _dbContext.MedicinePackagings.Add(basePack);
            _dbContext.MedicineBatches.Add(batch);
            _dbContext.Prescriptions.Add(prescription);
            _dbContext.PrescriptionItems.Add(pItem);
            await _dbContext.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BusinessException>(() => _service.DispenseAsync(caseId));
            Assert.Contains("không đủ tồn kho hợp lệ", exception.Message);
        }
    }
}
