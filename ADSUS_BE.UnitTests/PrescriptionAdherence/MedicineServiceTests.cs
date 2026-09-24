using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public class MedicineServiceTests
{
    private readonly Mock<IMedicineRepository> _medicineRepoMock;
    private readonly AppDbContext _db;
    private readonly MedicineService _sut; // System Under Test

    public MedicineServiceTests()
    {
        _medicineRepoMock = new Mock<IMedicineRepository>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new MedicineService(
            _medicineRepoMock.BackedBy(_db).Object,
            new MedicinePackagingRepository(_db),
            new MedicineUnitRepository(_db));
    }

    [Fact]
    public async Task SearchMedicinesAsync_WithKeyword_ReturnsMappedResponses()
    {
        // Arrange
        var keyword = "para";
        var limit = 20;
        var medicines = new List<Medicine>
        {
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paracetamol 500mg", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow },
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Paralmax", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow }
        };

        _medicineRepoMock.Setup(repo => repo.SearchByNameAsync(keyword, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicines);

        // Act
        var result = await _sut.SearchMedicinesAsync(keyword, limit, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var list = result as List<MedicineResponse> ?? new List<MedicineResponse>(result);
        Assert.Equal(2, list.Count);
        Assert.Equal("Paracetamol 500mg", list[0].Name);
        Assert.Equal("Paralmax", list[1].Name);
        
        _medicineRepoMock.Verify(repo => repo.SearchByNameAsync(keyword, limit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchMedicinesAsync_EmptyKeyword_ReturnsMappedResponses()
    {
        // Arrange
        var keyword = "";
        var limit = 5;
        var medicines = new List<Medicine>
        {
            new Medicine { MedicineId = Guid.NewGuid(), Name = "Aspirin", Status = MedicineStatus.Active, CreatedAt = DateTime.UtcNow }
        };

        _medicineRepoMock.Setup(repo => repo.SearchByNameAsync(keyword, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicines);

        // Act
        var result = await _sut.SearchMedicinesAsync(keyword, limit, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var list = result as List<MedicineResponse> ?? new List<MedicineResponse>(result);
        var medicine = Assert.Single(list);
        Assert.Equal("Aspirin", medicine.Name);
    }
    [Fact]
    public async Task ActivateMedicineAsync_ValidId_SetsStatusToActive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Status = MedicineStatus.Inactive };

        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.ActivateMedicineAsync(id, CancellationToken.None);

        // Assert
        Assert.Equal(MedicineStatus.Active, existing.Status);
        _medicineRepoMock.Verify(repo => repo.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateMedicineAsync_InvalidId_ThrowsResourceNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ADSUS_BE.BLL.Common.Exceptions.ResourceNotFoundException>(
            () => _sut.ActivateMedicineAsync(id, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusAndInStockFilters_CallsRepositoryAndMapsResponses()
    {
        // Arrange
        var medId = Guid.NewGuid();
        var medicines = new List<Medicine>
        {
            new Medicine
            {
                MedicineId = medId,
                Name = "Med A",
                Status = MedicineStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LowStockThreshold = 10,
                MedicineBatches = new List<MedicineBatch>
                {
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 50, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) }
                }
            }
        };

        _medicineRepoMock.Setup(repo => repo.GetPagedAsync(1, 10, "med", true, "ACTIVE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((medicines, 1));

        // Act
        var result = await _sut.GetPagedAsync(1, 10, "med", true, "ACTIVE", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        var medicine = Assert.Single(result.Items);
        Assert.Equal("Med A", medicine.Name);
        Assert.Equal("ACTIVE", medicine.Status);
        Assert.Equal(50, medicine.TotalInventoryBase);

        _medicineRepoMock.Verify(repo => repo.GetPagedAsync(1, 10, "med", true, "ACTIVE", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==============================================
    // CreateMedicineAsync Tests
    // ==============================================
    [Fact]
    public async Task CreateMedicineAsync_Success_ValidRequest_NoUsageUnitAndVolume()
    {
        var request = new CreateMedicineRequest
        {
            Name = "Aspirin",
            UsageUnit = null,
            VolumePerBaseUnit = null,
            MedicineUnitId = Guid.NewGuid(),
            SalePrice = 100
        };

        _medicineRepoMock.Setup(repo => repo.FindByNameAsync(request.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);

        var result = await _sut.CreateMedicineAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Aspirin", result.Name);
        _medicineRepoMock.Verify(repo => repo.AddAsync(It.IsAny<Medicine>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMedicineAsync_Success_ValidRequest_WithUsageUnitAndVolume()
    {
        var request = new CreateMedicineRequest
        {
            Name = "Syrup",
            UsageUnit = "ml",
            VolumePerBaseUnit = 100,
            MedicineUnitId = Guid.NewGuid(),
            SalePrice = 100,
            LowStockThreshold = 50
        };

        _medicineRepoMock.Setup(repo => repo.FindByNameAsync(request.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);

        var result = await _sut.CreateMedicineAsync(request, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("ml", result.UsageUnit);
        Assert.Equal(100, result.VolumePerBaseUnit);
        Assert.Equal(50, result.LowStockThreshold);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_VolumeProvided_WithoutUsageUnit()
    {
        var request = new CreateMedicineRequest { Name = "Syrup", VolumePerBaseUnit = 100, UsageUnit = "   " };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.", exception.Message);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_UsageUnitProvided_WithoutVolume()
    {
        var request = new CreateMedicineRequest { Name = "Syrup", VolumePerBaseUnit = -5, UsageUnit = "ml" };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Vui lòng nhập đúng Hàm lượng (lớn hơn 0) khi đã nhập Đơn vị dùng.", exception.Message);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_NameAlreadyExists()
    {
        var request = new CreateMedicineRequest { Name = "Aspirin" };
        _medicineRepoMock.Setup(repo => repo.FindByNameAsync(request.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Medicine { Name = "Aspirin" });
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("đã tồn tại", exception.Message);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_NegativeLowStockThreshold()
    {
        var request = new CreateMedicineRequest { Name = "Aspirin", LowStockThreshold = -10 };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Ngưỡng cảnh báo hết hàng không được nhỏ hơn 0.", exception.Message);
    }

    // ==============================================
    // UpdateMedicineAsync Tests
    // ==============================================
    [Fact]
    public async Task UpdateMedicineAsync_Success_ValidRequest()
    {
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Name = "Aspirin", UsageUnit = null, VolumePerBaseUnit = null, LowStockThreshold = 0 };
        var request = new UpdateMedicineRequest { Name = "Aspirin", UsageUnit = "mg", VolumePerBaseUnit = 500, LowStockThreshold = 200 };

        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _sut.UpdateMedicineAsync(id, request, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("mg", result.UsageUnit);
        Assert.Equal(500, result.VolumePerBaseUnit);
        Assert.Equal(200, result.LowStockThreshold);
        
        _medicineRepoMock.Verify(repo => repo.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_IdNotFound()
    {
        var request = new UpdateMedicineRequest { Name = "Aspirin" };
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine?)null);
        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.UpdateMedicineAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy thuốc.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_NameModified()
    {
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Name = "Aspirin" };
        var request = new UpdateMedicineRequest { Name = "Aspirin 500mg" };
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(id, request, TestContext.Current.CancellationToken));
        Assert.Equal("Tên thuốc là Master Data gốc, tuyệt đối không được sửa sau khi tạo.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_NegativeLowStockThreshold()
    {
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Name = "Aspirin" };
        var request = new UpdateMedicineRequest { Name = "Aspirin", LowStockThreshold = -5 };
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(id, request, TestContext.Current.CancellationToken));
        Assert.Equal("Ngưỡng cảnh báo hết hàng không được nhỏ hơn 0.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_VolumeProvided_WithoutUsageUnit()
    {
        var request = new UpdateMedicineRequest { VolumePerBaseUnit = 100, UsageUnit = "" };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken));
        Assert.Equal("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_UsageUnitProvided_WithoutVolume()
    {
        var request = new UpdateMedicineRequest { VolumePerBaseUnit = 0, UsageUnit = "ml" };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken));
        Assert.Equal("Vui lòng nhập đúng Hàm lượng (lớn hơn 0) khi đã nhập Đơn vị dùng.", exception.Message);
    }

    // ==============================================
    // Medicine Packaging (Add/Update) Tests
    // ==============================================
    [Fact]
    public async Task AddPackagingAsync_Fail_DuplicateUnit()
    {
        var medId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var request = new CreateMedicinePackagingRequest { MedicineUnitId = unitId };
        
        // Add a mock existing packaging in db with the same unit
        _db.Set<MedicinePackaging>().Add(new MedicinePackaging { MedicineId = medId, MedicineUnitId = unitId });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.AddPackagingAsync(medId, request, TestContext.Current.CancellationToken));
        Assert.Equal("Đơn vị tính này đã được sử dụng cho thuốc. Không thể thêm trùng.", exception.Message);
    }

    [Fact]
    public async Task AddPackagingAsync_Fail_IsBaseUnit()
    {
        var medId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var request = new CreateMedicinePackagingRequest { MedicineUnitId = unitId, IsBaseUnit = true };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.AddPackagingAsync(medId, request, TestContext.Current.CancellationToken));
        Assert.Equal("Thuốc đã có đơn vị cơ sở và không thể thiết lập thêm đơn vị cơ sở khác.", exception.Message);
    }

    [Fact]
    public async Task UpdatePackagingAsync_Success_ChangeUnit()
    {
        var medId = Guid.NewGuid();
        var packagingId = Guid.NewGuid();
        var oldUnitId = Guid.NewGuid();
        var newUnitId = Guid.NewGuid();
        
        _db.Set<MedicineUnit>().Add(new MedicineUnit { MedicineUnitId = oldUnitId, Name = "Old" });
        _db.Set<MedicineUnit>().Add(new MedicineUnit { MedicineUnitId = newUnitId, Name = "New" });

        var existing = new MedicinePackaging { Id = packagingId, MedicineId = medId, MedicineUnitId = oldUnitId };
        _db.Set<MedicinePackaging>().Add(existing);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateMedicinePackagingRequest { MedicineUnitId = newUnitId, ConversionFactor = 2 };
        var result = await _sut.UpdatePackagingAsync(packagingId, request, TestContext.Current.CancellationToken);
        
        Assert.NotNull(result);
        Assert.Equal(newUnitId, existing.MedicineUnitId);
        Assert.Equal(2, existing.ConversionFactor);
    }

    [Fact]
    public async Task UpdatePackagingAsync_Fail_DuplicateUnit()
    {
        var medId = Guid.NewGuid();
        var packagingIdToUpdate = Guid.NewGuid();
        var existingUnitId = Guid.NewGuid();
        var duplicateUnitId = Guid.NewGuid();
        
        _db.Set<MedicinePackaging>().Add(new MedicinePackaging { Id = packagingIdToUpdate, MedicineId = medId, MedicineUnitId = existingUnitId });
        _db.Set<MedicinePackaging>().Add(new MedicinePackaging { Id = Guid.NewGuid(), MedicineId = medId, MedicineUnitId = duplicateUnitId });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateMedicinePackagingRequest { MedicineUnitId = duplicateUnitId };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdatePackagingAsync(packagingIdToUpdate, request, TestContext.Current.CancellationToken));
        Assert.Equal("Đơn vị tính này đã được sử dụng bởi một quy cách khác của cùng loại thuốc.", exception.Message);
    }

    [Fact]
    public async Task UpdatePackagingAsync_Fail_ModifyBaseUnit()
    {
        var medId = Guid.NewGuid();
        var packagingIdToUpdate = Guid.NewGuid();
        var existingUnitId = Guid.NewGuid();
        var newUnitId = Guid.NewGuid();
        
        _db.Set<MedicinePackaging>().Add(new MedicinePackaging { Id = packagingIdToUpdate, MedicineId = medId, MedicineUnitId = existingUnitId, IsBaseUnit = true });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 1. Try to uncheck IsBaseUnit
        var req1 = new UpdateMedicinePackagingRequest { MedicineUnitId = existingUnitId, IsBaseUnit = false, ConversionFactor = 1 };
        var ex1 = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdatePackagingAsync(packagingIdToUpdate, req1, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể gỡ bỏ trạng thái đơn vị cơ sở của quy cách này.", ex1.Message);

        // 2. Try to change MedicineUnitId
        var req2 = new UpdateMedicinePackagingRequest { MedicineUnitId = newUnitId, IsBaseUnit = true, ConversionFactor = 1 };
        var ex2 = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdatePackagingAsync(packagingIdToUpdate, req2, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể thay đổi đơn vị tính của đơn vị cơ sở.", ex2.Message);

        // 3. Try to change ConversionFactor
        var req3 = new UpdateMedicinePackagingRequest { MedicineUnitId = existingUnitId, IsBaseUnit = true, ConversionFactor = 2 };
        var ex3 = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdatePackagingAsync(packagingIdToUpdate, req3, TestContext.Current.CancellationToken));
        Assert.Equal("Hệ số quy đổi của đơn vị cơ sở luôn bằng 1.", ex3.Message);
    }

    [Fact]
    public async Task DeletePackagingAsync_Success()
    {
        var medId = Guid.NewGuid();
        var packagingId = Guid.NewGuid();
        
        _db.Set<MedicinePackaging>().Add(new MedicinePackaging { Id = packagingId, MedicineId = medId, IsBaseUnit = false });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _sut.DeletePackagingAsync(packagingId, TestContext.Current.CancellationToken);

        var exists = await _db.Set<MedicinePackaging>().AnyAsync(p => p.Id == packagingId, TestContext.Current.CancellationToken);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeletePackagingAsync_Fail_IsBaseUnit()
    {
        var medId = Guid.NewGuid();
        var packagingId = Guid.NewGuid();
        
        _db.Set<MedicinePackaging>().Add(new MedicinePackaging { Id = packagingId, MedicineId = medId, IsBaseUnit = true });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.DeletePackagingAsync(packagingId, TestContext.Current.CancellationToken));
        Assert.Equal("Không thể xóa đơn vị cơ sở của thuốc.", exception.Message);
    }

    // ==============================================
    // BR-123: Exclude Expired Batches from TotalInventoryBase Tests
    // ==============================================
    [Fact]
    public async Task GetPagedAsync_BR123_ExcludesExpiredBatchesFromTotalInventoryBase()
    {
        // Arrange
        var medId = Guid.NewGuid();
        var today = ClinicClock.Today(); // ngày phòng khám, cùng mốc với service
        var medicines = new List<Medicine>
        {
            new Medicine
            {
                MedicineId = medId,
                Name = "Paracetamol 500mg",
                Status = MedicineStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LowStockThreshold = 10,
                MedicineBatches = new List<MedicineBatch>
                {
                    // Valid batch: 100 units, expires in 60 days
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 100, ExpiryDate = today.AddDays(60) },
                    // Expired batch: 50 units, expired yesterday
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 50, ExpiryDate = today.AddDays(-1) },
                    // Valid batch: 30 units, expires today (boundary check: non-expired)
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 30, ExpiryDate = today },
                    // Severely expired batch: 200 units, expired last year
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 200, ExpiryDate = today.AddYears(-1) }
                }
            }
        };

        _medicineRepoMock.Setup(repo => repo.GetPagedAsync(1, 10, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((medicines, 1));

        // Act
        var result = await _sut.GetPagedAsync(1, 10, null, null, null, CancellationToken.None);

        // Assert: TotalInventoryBase must equal 100 + 30 = 130, strictly excluding 50 and 200
        Assert.NotNull(result);
        var item = Assert.Single(result.Items);
        Assert.Equal(130, item.TotalInventoryBase);
    }

    [Fact]
    public async Task SearchMedicinesAsync_BR123_ExcludesExpiredBatchesFromTotalInventoryBase()
    {
        // Arrange
        var medId = Guid.NewGuid();
        var today = ClinicClock.Today(); // ngày phòng khám, cùng mốc với service
        var medicines = new List<Medicine>
        {
            new Medicine
            {
                MedicineId = medId,
                Name = "Amoxicillin",
                Status = MedicineStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LowStockThreshold = 5,
                MedicineBatches = new List<MedicineBatch>
                {
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 80, ExpiryDate = today.AddDays(180) },
                    new MedicineBatch { Id = Guid.NewGuid(), QuantityBase = 40, ExpiryDate = today.AddDays(-10) }
                }
            }
        };

        _medicineRepoMock.Setup(repo => repo.SearchByNameAsync("Amox", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(medicines);

        // Act
        var result = await _sut.SearchMedicinesAsync("Amox", 20, CancellationToken.None);

        // Assert: TotalInventoryBase must equal 80, strictly excluding 40
        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(80, item.TotalInventoryBase);
    }

    [Fact]
    public async Task GetByIdAsync_BR123_ExcludesExpiredBatchesFromTotalInventoryBase()
    {
        // Arrange
        var medId = Guid.NewGuid();
        var today = ClinicClock.Today(); // ngày phòng khám, cùng mốc với service
        var medicine = new Medicine
        {
            MedicineId = medId,
            Name = "Ibuprofen 400mg",
            Status = MedicineStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LowStockThreshold = 10,
            MedicineBatches = new List<MedicineBatch>
            {
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "LOT-1", QuantityBase = 25, ExpiryDate = today.AddMonths(3) },
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "LOT-2", QuantityBase = 75, ExpiryDate = today.AddDays(-5) }
            }
        };

        _db.Medicines.Add(medicine);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetByIdAsync(medId, CancellationToken.None);

        // Assert: TotalInventoryBase must equal 25, strictly excluding 75
        Assert.NotNull(result);
        Assert.Equal(25, result.TotalInventoryBase);
    }

    [Fact]
    public async Task GetByIdAsync_BatchExpiredYesterdayClinicTime_ExcludedFromTotalInventoryBase()
    {
        // "Hôm nay" là ngày phòng khám (UTC+7) — theo UTC thì từ 00:00 đến 07:00 giờ VN lô hết hạn
        // hôm qua vẫn bị tính là còn hạn.
        var medId = Guid.NewGuid();
        var today = ClinicClock.Today();
        _db.Medicines.Add(new Medicine
        {
            MedicineId = medId,
            Name = "Cefuroxim 500mg",
            Status = MedicineStatus.Active,
            CreatedAt = DateTime.UtcNow,
            MedicineBatches = new List<MedicineBatch>
            {
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "LOT-TODAY", QuantityBase = 40, ExpiryDate = today },
                new MedicineBatch { Id = Guid.NewGuid(), LotNumber = "LOT-YESTERDAY", QuantityBase = 60, ExpiryDate = today.AddDays(-1) }
            }
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _sut.GetByIdAsync(medId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(40, result.TotalInventoryBase);
    }
}
