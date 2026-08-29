using System;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.PrescriptionAdherence;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

public class PrescriptionServiceTests
{
    private readonly Mock<IPrescriptionRepository> _prescriptionRepoMock = new();
    private readonly Mock<IPrescriptionItemRepository> _itemRepoMock = new();
    private readonly Mock<IMedicationIntakeLogRepository> _intakeLogRepoMock = new();
    private readonly Mock<ICaseRepository> _caseRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IMedicineRepository> _medicineRepoMock = new();
    private readonly Mock<IMedicationIntakeScheduleGenerator> _scheduleGeneratorMock = new();

    private PrescriptionService CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        return new PrescriptionService(
            db,
            _prescriptionRepoMock.Object,
            _itemRepoMock.Object,
            _intakeLogRepoMock.Object,
            _caseRepoMock.Object,
            _userRepoMock.Object,
            _medicineRepoMock.Object,
            _scheduleGeneratorMock.Object
        );
    }

    [Fact]
    public async Task CreateAsync_MedicineNotFound_ThrowsBusinessException()
    {
        // Arrange
        var service = CreateService();
        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Active });

        _caseRepoMock.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { CaseId = caseId, DoctorId = doctorId, Status = CaseStatus.Confirmed });

        var request = new CreatePrescriptionRequest(
            CaseId: caseId,
            Items: new[]
            {
                new CreatePrescriptionItemDto(
                    MedicineName: "ThuocKhongTonTai",
                    QuantityPerDose: 1,
                    DurationDays: 1,
                    StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    Instructions: null,
                    ScheduleSlots: new[] { ADSUS_BE.BLL.PrescriptionAdherence.DTOs.ScheduleSlot.Morning }
                )
            },
            GeneralNote: null
        );

        // Giả lập DB không tìm thấy thuốc
        _medicineRepoMock.Setup(r => r.FindByNameAsync("ThuocKhongTonTai", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medicine)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(doctorId, request));
        Assert.Contains("không tồn tại trong hệ thống hoặc đã bị ngừng sử dụng", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_InsufficientInventory_ThrowsBusinessException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        var service = new PrescriptionService(
            db,
            _prescriptionRepoMock.Object,
            _itemRepoMock.Object,
            _intakeLogRepoMock.Object,
            _caseRepoMock.Object,
            _userRepoMock.Object,
            _medicineRepoMock.Object,
            _scheduleGeneratorMock.Object
        );

        var doctorId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var medicineId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Active });

        _caseRepoMock.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { CaseId = caseId, DoctorId = doctorId, Status = CaseStatus.Confirmed });

        // Tìm thấy thuốc trong danh mục
        _medicineRepoMock.Setup(r => r.FindByNameAsync("Paracetamol", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Medicine { MedicineId = medicineId, Name = "Paracetamol", Status = MedicineStatus.Active, UsageUnit = "viên" });

        // Add 10 viên vào kho (Trong khi request yêu cầu 1 * 1 * 15 = 15 viên)
        db.MedicineBatches.Add(new MedicineBatch
        {
            Id = Guid.NewGuid(),
            MedicineId = medicineId,
            QuantityBase = 10,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            LotNumber = "LOT01"
        });
        await db.SaveChangesAsync();

        var request = new CreatePrescriptionRequest(
            CaseId: caseId,
            Items: new[]
            {
                new CreatePrescriptionItemDto(
                    MedicineName: "Paracetamol",
                    QuantityPerDose: 1,
                    DurationDays: 15,
                    StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    Instructions: null,
                    ScheduleSlots: new[] { ADSUS_BE.BLL.PrescriptionAdherence.DTOs.ScheduleSlot.Morning }
                )
            },
            GeneralNote: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(doctorId, request));
        Assert.Contains("không đủ số lượng trong kho", ex.Message);
        Assert.Contains("Yêu cầu: 15", ex.Message);
        Assert.Contains("Hiện còn: 10", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Validation actor / case — các nhánh lỗi chưa có test
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_DoctorNotFound_ThrowsResourceNotFoundException()
    {
        var service = CreateService();
        var unknownDoctorId = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(unknownDoctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new CreatePrescriptionRequest(
            CaseId: Guid.NewGuid(),
            Items: Array.Empty<CreatePrescriptionItemDto>(),
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => service.CreateAsync(unknownDoctorId, request));
        Assert.Contains("bác sĩ", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ActorIsNotDoctor_ThrowsBusinessException()
    {
        var service = CreateService();
        var nurseId = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(nurseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = nurseId, Role = UserRole.Nurse, Status = UserStatus.Active });

        var request = new CreatePrescriptionRequest(
            CaseId: Guid.NewGuid(),
            Items: Array.Empty<CreatePrescriptionItemDto>(),
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CreateAsync(nurseId, request));
        Assert.Contains("Chỉ bác sĩ mới được kê đơn", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_DoctorInactive_ThrowsBusinessException()
    {
        var service = CreateService();
        var doctorId = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Deactivated });

        var request = new CreatePrescriptionRequest(
            CaseId: Guid.NewGuid(),
            Items: Array.Empty<CreatePrescriptionItemDto>(),
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CreateAsync(doctorId, request));
        Assert.Contains("không hoạt động", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_CaseNotFound_ThrowsResourceNotFoundException()
    {
        var service = CreateService();
        var doctorId = Guid.NewGuid();
        var caseId   = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Active });

        _caseRepoMock
            .Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Case?)null);

        var request = new CreatePrescriptionRequest(
            CaseId: caseId,
            Items: Array.Empty<CreatePrescriptionItemDto>(),
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => service.CreateAsync(doctorId, request));
        Assert.Contains(caseId.ToString(), ex.Message);
    }

    [Fact]
    public async Task CreateAsync_CaseNotConfirmed_ThrowsBusinessException()
    {
        var service = CreateService();
        var doctorId = Guid.NewGuid();
        var caseId   = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Active });

        _caseRepoMock
            .Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { CaseId = caseId, DoctorId = doctorId, Status = CaseStatus.Created }); // Không phải Confirmed

        var request = new CreatePrescriptionRequest(
            CaseId: caseId,
            Items: Array.Empty<CreatePrescriptionItemDto>(),
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CreateAsync(doctorId, request));
        Assert.Contains("Confirmed", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_CaseNotOwnedByDoctor_ThrowsBusinessException()
    {
        var service  = CreateService();
        var doctorId = Guid.NewGuid();
        var otherDoc = Guid.NewGuid(); // Chủ thực sự của case
        var caseId   = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Active });

        _caseRepoMock
            .Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { CaseId = caseId, DoctorId = otherDoc, Status = CaseStatus.Confirmed }); // Bác sĩ khác

        var request = new CreatePrescriptionRequest(
            CaseId: caseId,
            Items: Array.Empty<CreatePrescriptionItemDto>(),
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CreateAsync(doctorId, request));
        Assert.Contains("quyền kê đơn", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_MedicineInactive_ThrowsBusinessException()
    {
        var service = CreateService();
        var doctorId = Guid.NewGuid();
        var caseId   = Guid.NewGuid();
        var medicineId = Guid.NewGuid();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = doctorId, Role = UserRole.Doctor, Status = UserStatus.Active });

        _caseRepoMock
            .Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { CaseId = caseId, DoctorId = doctorId, Status = CaseStatus.Confirmed });

        // Thuốc tồn tại nhưng Inactive
        _medicineRepoMock
            .Setup(r => r.FindByNameAsync("ThuocCu", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Medicine { MedicineId = medicineId, Name = "ThuocCu", Status = MedicineStatus.Inactive });

        var request = new CreatePrescriptionRequest(
            CaseId: caseId,
            Items: new[]
            {
                new CreatePrescriptionItemDto(
                    MedicineName: "ThuocCu",
                    QuantityPerDose: 1,
                    DurationDays: 3,
                    StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    Instructions: null,
                    ScheduleSlots: new[] { ADSUS_BE.BLL.PrescriptionAdherence.DTOs.ScheduleSlot.Morning }
                )
            },
            GeneralNote: null);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CreateAsync(doctorId, request));
        Assert.Contains("không tồn tại trong hệ thống", ex.Message);
    }
}
