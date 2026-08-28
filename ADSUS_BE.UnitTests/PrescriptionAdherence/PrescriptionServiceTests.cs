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
            DoctorId: doctorId,
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
}
