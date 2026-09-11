using System;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.ClinicServiceManagement;
using ADSUS_BE.BLL.ClinicServiceManagement.DTOs;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// Group 1: 26 test cases for ClinicServiceManagementService (Admin CRUD)
/// Covers 1.1.1 - 1.4.5 in scratch/test_cases.md
/// </summary>
public class ClinicServiceManagementServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ClinicServiceManagementService CreateService(AppDbContext context)
    {
        return new ClinicServiceManagementService(context, NullLogger<ClinicServiceManagementService>.Instance);
    }

    #region 1.1 CreateAsync (8 test cases)

    [Fact]
    public async Task TC_1_1_1_CreateAsync_HappyPath_ReturnsCreatedResponseWithIsActiveTrue()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = "XRAY",
            Name = "Chụp X-quang",
            Price = 150000,
            Description = "Chụp X-quang tim phổi thẳng"
        };

        // Act
        var result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("XRAY", result.Code);
        Assert.Equal("Chụp X-quang", result.Name);
        Assert.Equal(150000, result.Price);
        Assert.Equal("Chụp X-quang tim phổi thẳng", result.Description);
        Assert.True(result.IsActive);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);

        // Verify in database
        var entity = await context.ClinicServices.FirstOrDefaultAsync(s => s.Id == result.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.Equal("XRAY", entity.Code);
        Assert.True(entity.IsActive);
    }

    [Fact]
    public async Task TC_1_1_2_CreateAsync_DuplicateCode_ThrowsConflictException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        context.ClinicServices.Add(new ClinicService
        {
            Id = Guid.NewGuid(),
            Code = "GENERAL_EXAM",
            Name = "Khám thường",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new CreateClinicServiceRequest
        {
            Code = "general_exam", // Case-insensitive duplicate
            Name = "Khám tổng quát trùng lặp",
            Price = 120000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Mã dịch vụ đã tồn tại.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TC_1_1_3_CreateAsync_EmptyOrWhitespaceCode_ThrowsValidationException(string? code)
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = code!,
            Name = "Khám tai mũi họng",
            Price = 120000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Mã dịch vụ không được để trống."));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TC_1_1_4_CreateAsync_EmptyOrWhitespaceName_ThrowsValidationException(string? name)
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = "ENT_EXAM",
            Name = name!,
            Price = 120000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Tên dịch vụ không được để trống."));
    }

    [Fact]
    public async Task TC_1_1_5_CreateAsync_NegativePrice_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = "BLOOD_TEST",
            Name = "Xét nghiệm máu",
            Price = -50000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Giá dịch vụ phải lớn hơn 0."));
    }

    [Fact]
    public async Task TC_1_1_6_CreateAsync_ZeroPrice_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = "FREE_EXAM",
            Name = "Khám miễn phí",
            Price = 0
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Giá dịch vụ phải lớn hơn 0."));
    }

    [Fact]
    public async Task TC_1_1_7_CreateAsync_CodeExceeds50Chars_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = new string('A', 51),
            Name = "Dịch vụ mã siêu dài",
            Price = 100000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Mã dịch vụ không được vượt quá 50 ký tự."));
    }

    [Fact]
    public async Task TC_1_1_8_CreateAsync_NameExceeds200Chars_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var request = new CreateClinicServiceRequest
        {
            Code = "LONG_NAME_TEST",
            Name = new string('A', 201),
            Price = 100000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Tên dịch vụ không được vượt quá 200 ký tự."));
    }

    #endregion

    #region 1.2 UpdateAsync (8 test cases)

    [Fact]
    public async Task TC_1_2_1_UpdateAsync_UpdatePrice_ModifiesPriceAndUpdatesTimestamp()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        var originalTime = DateTime.UtcNow.AddMinutes(-10);
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "TEST_PRICE",
            Name = "Kiểm tra giá",
            Price = 100000,
            IsActive = true,
            CreatedAt = originalTime,
            UpdatedAt = originalTime
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest
        {
            Price = 200000
        };

        // Act
        var result = await service.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(200000, result.Price);
        Assert.True(result.UpdatedAt > originalTime);

        var entity = await context.ClinicServices.FindAsync(new object[] { id }, TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.Equal(200000, entity.Price);
    }

    [Fact]
    public async Task TC_1_2_2_UpdateAsync_UpdateName_ModifiesNamePreservingOtherFields()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "OLD_CODE",
            Name = "Tên cũ",
            Price = 150000,
            Description = "Mô tả ban đầu",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest
        {
            Name = "Khám tổng quát"
        };

        // Act
        var result = await service.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Khám tổng quát", result.Name);
        Assert.Equal(150000, result.Price);
        Assert.Equal("OLD_CODE", result.Code);
        Assert.Equal("Mô tả ban đầu", result.Description);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task TC_1_2_3_UpdateAsync_NotFound_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateClinicServiceRequest { Price = 200000 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(nonExistentId, request, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy dịch vụ.", ex.Message);
    }

    [Fact]
    public async Task TC_1_2_4_UpdateAsync_NegativePrice_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "PRICE_TEST",
            Name = "Kiểm tra giá âm",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest { Price = -1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(id, request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Giá dịch vụ phải lớn hơn 0."));
    }

    [Fact]
    public async Task TC_1_2_5_UpdateAsync_ZeroPrice_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "PRICE_ZERO",
            Name = "Kiểm tra giá 0",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest { Price = 0 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(id, request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Giá dịch vụ phải lớn hơn 0."));
    }

    [Fact]
    public async Task TC_1_2_6_UpdateAsync_PartialUpdateOnlyPrice_PreservesNameAndDescription()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "PARTIAL",
            Name = "Dịch vụ A",
            Description = "Mô tả A",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest
        {
            Price = 175000,
            Name = null,
            Description = null
        };

        // Act
        var result = await service.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(175000, result.Price);
        Assert.Equal("Dịch vụ A", result.Name);
        Assert.Equal("Mô tả A", result.Description);
    }

    [Fact]
    public async Task TC_1_2_7_UpdateAsync_ReactivateService_SetsIsActiveToTrue()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "REACTIVATE",
            Name = "Dịch vụ đã tắt",
            Price = 100000,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest
        {
            IsActive = true
        };

        // Act
        var result = await service.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsActive);

        var entity = await context.ClinicServices.FindAsync(new object[] { id }, TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.True(entity.IsActive);
    }

    [Fact]
    public async Task TC_1_2_8_UpdateAsync_NameExceeds200Chars_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "LONG_UPDATE",
            Name = "Tên hợp lệ",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = new UpdateClinicServiceRequest
        {
            Name = new string('B', 201)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(id, request, TestContext.Current.CancellationToken));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Tên dịch vụ không được vượt quá 200 ký tự."));
    }

    #endregion

    #region 1.3 DeactivateAsync (5 test cases)

    [Fact]
    public async Task TC_1_3_1_DeactivateAsync_ActiveService_SoftDeletesAndSetsIsActiveFalse()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "DEACT_TEST",
            Name = "Vô hiệu hóa",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await service.DeactivateAsync(id, TestContext.Current.CancellationToken);

        // Assert
        var entity = await context.ClinicServices.FindAsync(new object[] { id }, TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.False(entity.IsActive);
    }

    [Fact]
    public async Task TC_1_3_2_DeactivateAsync_AlreadyInactive_ExecutesGracefullyIdempotent()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        var initialUpdated = DateTime.UtcNow.AddHours(-1);
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "ALREADY_INACTIVE",
            Name = "Đã ngưng",
            Price = 100000,
            IsActive = false,
            CreatedAt = initialUpdated,
            UpdatedAt = initialUpdated
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - should return cleanly without throwing
        await service.DeactivateAsync(id, TestContext.Current.CancellationToken);

        // Assert
        var entity = await context.ClinicServices.FindAsync(new object[] { id }, TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.False(entity.IsActive);
        Assert.Equal(initialUpdated, entity.UpdatedAt);
    }

    [Fact]
    public async Task TC_1_3_3_DeactivateAsync_NotFound_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeactivateAsync(nonExistentId, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy dịch vụ.", ex.Message);
    }

    [Fact]
    public async Task TC_1_3_4_DeactivateAsync_HistoryRetention_CaseClinicServicesPreserveReferences()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var serviceId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        var clinicService = new ClinicService
        {
            Id = serviceId,
            Code = "HIST_TEST",
            Name = "Dịch vụ lịch sử",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var caseRecord = new CaseClinicService
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            ClinicServiceId = serviceId,
            PriceAtTime = 100000,
            CreatedAt = DateTime.UtcNow
        };

        context.ClinicServices.Add(clinicService);
        context.CaseClinicServices.Add(caseRecord);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Deactivate service
        await service.DeactivateAsync(serviceId, TestContext.Current.CancellationToken);

        // Assert - Query historical record
        var historical = await context.CaseClinicServices
            .Include(cs => cs.ClinicService)
            .FirstOrDefaultAsync(cs => cs.Id == caseRecord.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(historical);
        Assert.NotNull(historical.ClinicService);
        Assert.Equal("Dịch vụ lịch sử", historical.ClinicService.Name);
        Assert.Equal("HIST_TEST", historical.ClinicService.Code);
        Assert.False(historical.ClinicService.IsActive);
        Assert.Equal(100000, historical.PriceAtTime);
    }

    [Fact]
    public async Task TC_1_3_5_DeactivateAsync_BlockNewCase_AddServiceToCaseAsyncThrowsForDeactivatedService()
    {
        // Arrange
        using var context = CreateContext();
        var mgmtService = CreateService(context);
        var caseClinicService = new CaseClinicServiceService(context, NullLogger<CaseClinicServiceService>.Instance);

        var serviceId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        context.ClinicServices.Add(new ClinicService
        {
            Id = serviceId,
            Code = "DIS_TEST",
            Name = "Dịch vụ sắp tắt",
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.Cases.Add(new Case
        {
            CaseId = caseId,
            DoctorId = Guid.NewGuid(),
            PatientProfileId = Guid.NewGuid(),
            Status = CaseStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Deactivate service
        await mgmtService.DeactivateAsync(serviceId, TestContext.Current.CancellationToken);

        // Act & Assert - Attempting to attach deactivated service to case should throw
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            caseClinicService.AddServiceToCaseAsync(caseId, serviceId, TestContext.Current.CancellationToken));
        Assert.Equal("Dịch vụ không tồn tại hoặc không hoạt động.", ex.Message);
    }

    #endregion

    #region 1.4 GetAllAsync / GetByIdAsync (5 test cases)

    [Fact]
    public async Task TC_1_4_1_GetAllAsync_FilterActive_ReturnsOnlyActiveServices()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);

        context.ClinicServices.AddRange(
            new ClinicService { Id = Guid.NewGuid(), Code = "ACT1", Name = "Active 1", Price = 10000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ClinicService { Id = Guid.NewGuid(), Code = "ACT2", Name = "Active 2", Price = 20000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ClinicService { Id = Guid.NewGuid(), Code = "INACT", Name = "Inactive 1", Price = 30000, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetAllAsync(isActive: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.True(s.IsActive));
        Assert.Contains(result, s => s.Code == "ACT1");
        Assert.Contains(result, s => s.Code == "ACT2");
    }

    [Fact]
    public async Task TC_1_4_2_GetAllAsync_FilterNull_ReturnsBothActiveAndInactive()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);

        context.ClinicServices.AddRange(
            new ClinicService { Id = Guid.NewGuid(), Code = "ACT1", Name = "Active 1", Price = 10000, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ClinicService { Id = Guid.NewGuid(), Code = "INACT1", Name = "Inactive 1", Price = 20000, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetAllAsync(isActive: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.IsActive);
        Assert.Contains(result, s => !s.IsActive);
    }

    [Fact]
    public async Task TC_1_4_3_GetAllAsync_EmptyDatabase_ReturnsEmptyListWithoutThrowing()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);

        // Act
        var result = await service.GetAllAsync(isActive: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task TC_1_4_4_GetByIdAsync_Found_ReturnsClinicServiceResponse()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var id = Guid.NewGuid();
        context.ClinicServices.Add(new ClinicService
        {
            Id = id,
            Code = "GET_TEST",
            Name = "Dịch vụ tìm kiếm",
            Description = "Chi tiết mô tả",
            Price = 250000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetByIdAsync(id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("GET_TEST", result.Code);
        Assert.Equal("Dịch vụ tìm kiếm", result.Name);
        Assert.Equal("Chi tiết mô tả", result.Description);
        Assert.Equal(250000, result.Price);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task TC_1_4_5_GetByIdAsync_NotFound_ThrowsBusinessException()
    {
        // Arrange
        using var context = CreateContext();
        var service = CreateService(context);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.GetByIdAsync(nonExistentId, TestContext.Current.CancellationToken));
        Assert.Equal("Không tìm thấy dịch vụ.", ex.Message);
    }

    #endregion
}
