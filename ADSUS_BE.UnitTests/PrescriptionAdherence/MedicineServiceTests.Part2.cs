using System;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Entities;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public partial class MedicineServiceTests
{
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
            .ReturnsAsync((Medicine)null);

        var result = await _sut.CreateMedicineAsync(request);

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
            SalePrice = 100
        };

        _medicineRepoMock.Setup(repo => repo.FindByNameAsync(request.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine)null);

        var result = await _sut.CreateMedicineAsync(request);

        Assert.NotNull(result);
        Assert.Equal("ml", result.UsageUnit);
        Assert.Equal(100, result.VolumePerBaseUnit);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_VolumeProvided_WithoutUsageUnit()
    {
        var request = new CreateMedicineRequest { Name = "Syrup", VolumePerBaseUnit = 100, UsageUnit = "   " };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request));
        Assert.Equal("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.", exception.Message);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_UsageUnitProvided_WithoutVolume()
    {
        var request = new CreateMedicineRequest { Name = "Syrup", VolumePerBaseUnit = -5, UsageUnit = "ml" };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request));
        Assert.Equal("Vui lòng nhập đúng Hàm lượng (lớn hơn 0) khi đã nhập Đơn vị dùng.", exception.Message);
    }

    [Fact]
    public async Task CreateMedicineAsync_Fail_NameAlreadyExists()
    {
        var request = new CreateMedicineRequest { Name = "Aspirin" };
        _medicineRepoMock.Setup(repo => repo.FindByNameAsync(request.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Medicine { Name = "Aspirin" });
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateMedicineAsync(request));
        Assert.Contains("đã tồn tại", exception.Message);
    }

    // ==============================================
    // UpdateMedicineAsync Tests
    // ==============================================
    [Fact]
    public async Task UpdateMedicineAsync_Success_ValidRequest()
    {
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Name = "Aspirin", UsageUnit = null, VolumePerBaseUnit = null };
        var request = new UpdateMedicineRequest { Name = "Aspirin", UsageUnit = "mg", VolumePerBaseUnit = 500 };

        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _sut.UpdateMedicineAsync(id, request);
        Assert.NotNull(result);
        Assert.Equal("mg", result.UsageUnit);
        Assert.Equal(500, result.VolumePerBaseUnit);
        _medicineRepoMock.Verify(repo => repo.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_IdNotFound()
    {
        var request = new UpdateMedicineRequest { Name = "Aspirin" };
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine)null);
        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.UpdateMedicineAsync(Guid.NewGuid(), request));
        Assert.Equal("Không tìm thấy thuốc.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_NameModified()
    {
        var id = Guid.NewGuid();
        var existing = new Medicine { MedicineId = id, Name = "Aspirin" };
        var request = new UpdateMedicineRequest { Name = "Aspirin 500mg" };
        _medicineRepoMock.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(id, request));
        Assert.Equal("Tên thuốc là Master Data gốc, tuyệt đối không được sửa sau khi tạo.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_VolumeProvided_WithoutUsageUnit()
    {
        var request = new UpdateMedicineRequest { VolumePerBaseUnit = 100, UsageUnit = "" };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(Guid.NewGuid(), request));
        Assert.Equal("Vui lòng nhập Đơn vị dùng (Usage Unit) khi đã nhập Hàm lượng.", exception.Message);
    }

    [Fact]
    public async Task UpdateMedicineAsync_Fail_UsageUnitProvided_WithoutVolume()
    {
        var request = new UpdateMedicineRequest { VolumePerBaseUnit = 0, UsageUnit = "ml" };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateMedicineAsync(Guid.NewGuid(), request));
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
        await _db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.AddPackagingAsync(medId, request));
        Assert.Equal("Đơn vị tính này đã được sử dụng cho thuốc. Không thể thêm trùng.", exception.Message);
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
        await _db.SaveChangesAsync();

        var request = new UpdateMedicinePackagingRequest { MedicineUnitId = newUnitId, ConversionFactor = 2 };
        var result = await _sut.UpdatePackagingAsync(packagingId, request);
        
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
        await _db.SaveChangesAsync();

        var request = new UpdateMedicinePackagingRequest { MedicineUnitId = duplicateUnitId };
        var exception = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdatePackagingAsync(packagingIdToUpdate, request));
        Assert.Equal("Đơn vị tính này đã được sử dụng bởi một quy cách khác của cùng loại thuốc.", exception.Message);
    }
}
