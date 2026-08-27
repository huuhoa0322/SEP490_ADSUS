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
            Assert.Equal(10000, batch.UnitImportPrice); // 100000 / 10

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
                SupplierId = supplierId,
                UnitImportPrice = 5000
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
            Assert.Equal(2600, batch.UnitImportPrice); 
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
                SupplierId = supplierId,
                UnitImportPrice = 5000
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

            var batch1 = new MedicineBatch { Id = Guid.NewGuid(), MedicineId = med1.MedicineId, SupplierId = supplier.SupplierId, LotNumber = "LOT-1", QuantityBase = 100, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)) };
            var batch2 = new MedicineBatch { Id = Guid.NewGuid(), MedicineId = med2.MedicineId, LotNumber = "LOT-2", QuantityBase = 50, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)) };
            _dbContext.MedicineBatches.AddRange(batch1, batch2);

            _dbContext.InventoryTransactions.Add(new InventoryTransaction { Id = Guid.NewGuid(), BatchId = batch1.Id, MedicinePackagingId = pack.Id, QuantityInUnit = 10, QuantityBase = 100, TxnType = InventoryTxnType.Import, TxnDate = DateTime.UtcNow });
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
    }
}
