using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public class SupplierServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SupplierService _sut;

    public SupplierServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new SupplierService(_db);
    }


    [Fact]
    public async Task CreateSupplierAsync_ShouldThrowBusinessException_WhenTaxCodeFormatIsInvalid()
    {
        // Arrange
        var request = new CreateSupplierRequest("New", "0981234567", "email@test.com", "Address", "invalid-tax");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateSupplierAsync(request, CancellationToken.None));
        Assert.Contains("Mã số thuế phải là", ex.Message);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task GetSuppliersAsync_ShouldReturnPagedResult_WhenNoSearch()
    {
        // Arrange
        _db.Suppliers.AddRange(
            new Supplier { SupplierId = Guid.NewGuid(), Name = "A", PhoneNumber = "123", Email = "a@a", Address = "A", TaxCode = "0000000001", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Supplier { SupplierId = Guid.NewGuid(), Name = "B", PhoneNumber = "123", Email = "b@b", Address = "B", TaxCode = "0000000002", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSuppliersAsync(1, 10, null, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetSuppliersAsync_ShouldFilterBySearchKeyword()
    {
        // Arrange
        _db.Suppliers.AddRange(
            new Supplier { SupplierId = Guid.NewGuid(), Name = "Dược Hậu Giang", PhoneNumber = "0987654321", Email = "a@a", Address = "A", TaxCode = "1234567890", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Supplier { SupplierId = Guid.NewGuid(), Name = "Imexpharm", PhoneNumber = "0999999999", Email = "b@b", Address = "B", TaxCode = "0987654321", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var resultByName = await _sut.GetSuppliersAsync(1, 10, "hậu giang", CancellationToken.None);
        var resultByPhone = await _sut.GetSuppliersAsync(1, 10, "0999999", CancellationToken.None);
        var resultByTaxCode = await _sut.GetSuppliersAsync(1, 10, "123456", CancellationToken.None);

        // Assert
        Assert.Single(resultByName.Items);
        Assert.Equal("Dược Hậu Giang", resultByName.Items[0].Name);

        Assert.Single(resultByPhone.Items);
        Assert.Equal("Imexpharm", resultByPhone.Items[0].Name);

        Assert.Single(resultByTaxCode.Items);
        Assert.Equal("Dược Hậu Giang", resultByTaxCode.Items[0].Name);
    }

    [Fact]
    public async Task GetSupplierByIdAsync_ShouldReturnSupplier_WhenExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _db.Suppliers.Add(new Supplier { SupplierId = id, Name = "Test", PhoneNumber = "123", Email = "a", Address = "a", TaxCode = "0000000001", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSupplierByIdAsync(id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.SupplierId);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task GetSupplierByIdAsync_ShouldThrowResourceNotFoundException_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.GetSupplierByIdAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldCreate_WhenDataIsValid()
    {
        // Arrange
        var request = new CreateSupplierRequest("New Supplier", "0981234567", "email@test.com", "Address", "1234567890");

        // Act
        var result = await _sut.CreateSupplierAsync(request, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.SupplierId);
        Assert.Equal("New Supplier", result.Name);
        Assert.True(result.IsActive);
        
        var dbItem = await _db.Suppliers.FindAsync(result.SupplierId);
        Assert.NotNull(dbItem);
        Assert.Equal("New Supplier", dbItem.Name);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldThrowBusinessException_WhenNameAlreadyExists()
    {
        // Arrange
        _db.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), Name = "Duplicate", PhoneNumber = "0980000111", Email = "111", Address = "1", TaxCode = "1111111111", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var request = new CreateSupplierRequest("  duplicate ", "0981234567", "email@test.com", "Address", "1234567890");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateSupplierAsync(request, CancellationToken.None));
        Assert.Equal("Tên nhà cung cấp đã tồn tại.", ex.Message);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldThrowBusinessException_WhenPhoneAlreadyExists()
    {
        // Arrange
        _db.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), Name = "Other", PhoneNumber = "0981234567", Email = "111", Address = "1", TaxCode = "1111111111", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var request = new CreateSupplierRequest("New", "0981234567", "email@test.com", "Address", "1234567890");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateSupplierAsync(request, CancellationToken.None));
        Assert.Equal("Số điện thoại nhà cung cấp đã tồn tại.", ex.Message);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldThrowBusinessException_WhenEmailAlreadyExists()
    {
        // Arrange
        _db.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), Name = "Other", PhoneNumber = "0980000111", Email = "email@test.com", Address = "1", TaxCode = "1111111111", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var request = new CreateSupplierRequest("New", "0981234567", "  Email@test.com ", "Address", "1234567890");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateSupplierAsync(request, CancellationToken.None));
        Assert.Equal("Email nhà cung cấp đã tồn tại.", ex.Message);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldThrowBusinessException_WhenTaxCodeAlreadyExists()
    {
        // Arrange
        _db.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), Name = "Other", PhoneNumber = "0980000111", Email = "111", Address = "1", TaxCode = "1234567890", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var request = new CreateSupplierRequest("New", "0981234567", "email@test.com", "Address", "1234567890");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.CreateSupplierAsync(request, CancellationToken.None));
        Assert.Equal("Mã số thuế nhà cung cấp đã tồn tại.", ex.Message);
    }

    [Fact]
    public async Task UpdateSupplierAsync_ShouldUpdate_WhenDataIsValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        _db.Suppliers.Add(new Supplier { SupplierId = id, Name = "Old Name", PhoneNumber = "0900000001", Email = "1", Address = "1", TaxCode = "0000000001", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var request = new UpdateSupplierRequest("New Name", "0981234567", "new@test.com", "New Address");

        // Act
        var result = await _sut.UpdateSupplierAsync(id, request, CancellationToken.None);

        // Assert
        Assert.Equal("New Name", result.Name);
        Assert.Equal("0981234567", result.PhoneNumber);
        Assert.Equal("new@test.com", result.Email);

        var dbItem = await _db.Suppliers.FindAsync(id);
        Assert.NotNull(dbItem);
        Assert.Equal("New Name", dbItem.Name);
    }

    [Fact]
    public async Task UpdateSupplierAsync_ShouldThrowResourceNotFoundException_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateSupplierRequest("New Name", "0981234567", "new@test.com", "New Address");

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.UpdateSupplierAsync(id, request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSupplierAsync_ShouldThrowBusinessException_WhenNameExistsInOtherSupplier()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _db.Suppliers.Add(new Supplier { SupplierId = id1, Name = "Supplier 1", PhoneNumber = "0980000111", Email = "111", Address = "1", TaxCode = "1111111111", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Suppliers.Add(new Supplier { SupplierId = id2, Name = "Supplier 2", PhoneNumber = "0900000222", Email = "222", Address = "2", TaxCode = "0000000002", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var request = new UpdateSupplierRequest("  supplier 1 ", "0981234567", "new@test.com", "New Address");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _sut.UpdateSupplierAsync(id2, request, CancellationToken.None));
        Assert.Equal("Tên nhà cung cấp đã tồn tại.", ex.Message);
    }

    [Fact]
    public async Task UpdateSupplierStatusAsync_ShouldUpdateStatus_WhenExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _db.Suppliers.Add(new Supplier { SupplierId = id, Name = "Test", PhoneNumber = "0900000001", Email = "1", Address = "1", TaxCode = "0000000001", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // Act
        await _sut.UpdateSupplierStatusAsync(id, false, CancellationToken.None);

        // Assert
        var dbItem = await _db.Suppliers.FindAsync(id);
        Assert.NotNull(dbItem);
        Assert.False(dbItem.IsActive);
    }

    [Fact]
    public async Task UpdateSupplierStatusAsync_ShouldThrowResourceNotFoundException_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.UpdateSupplierStatusAsync(id, false, CancellationToken.None));
    }
}
