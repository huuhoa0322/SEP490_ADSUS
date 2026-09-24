using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// InventoryService/InvoiceService trước đây nhận thẳng AppDbContext; nay nhận repository +
/// IUnitOfWork (P11 review 24/09/2026). Helper này dựng chúng trên repository THẬT chạy cùng DB
/// InMemory mà test đã nạp dữ liệu, để các test cũ giữ nguyên phần Arrange/Assert.
/// </summary>
internal static class PrescriptionAdherenceTestServices
{
    public static InventoryService Inventory(AppDbContext db, ILogger<InventoryService>? logger = null) =>
        new(
            new InventoryRepository(db),
            new MedicineRepository(db),
            new SupplierRepository(db),
            new MedicinePackagingRepository(db),
            new PrescriptionRepository(db),
            new UnitOfWork(db),
            logger ?? NullLogger<InventoryService>.Instance);

    public static InvoiceService Invoice(
        AppDbContext db,
        IInventoryService inventoryService,
        IMedicationIntakeLogRepository intakeLogRepo,
        IMedicationIntakeScheduleGenerator scheduleGenerator,
        INotificationService notificationService)
    {
        // Test cũ truyền null khi luồng được test không đụng tới liều (huỷ hoá đơn trước đây đọc
        // liều thẳng từ DbContext) — dùng repository thật trên cùng DB.
        intakeLogRepo ??= new MedicationIntakeLogRepository(db);

        // Test truyền mock IMedicationIntakeLogRepository nhưng nạp liều thẳng vào DB — nối các
        // method huỷ hoá đơn mới cần (đọc/xoá liều) sang repository thật trên cùng DB.
        if (intakeLogRepo is IMocked<IMedicationIntakeLogRepository> mocked)
        {
            var real = new MedicationIntakeLogRepository(db);
            mocked.Mock.Setup(r => r.ListByItemIdsForUpdateAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .Returns((IReadOnlyCollection<Guid> ids, CancellationToken ct) => real.ListByItemIdsForUpdateAsync(ids, ct));
            mocked.Mock.Setup(r => r.RemoveRange(It.IsAny<IEnumerable<ADSUS_BE.DAL.Entities.MedicationIntakeLog>>()))
                .Callback((IEnumerable<ADSUS_BE.DAL.Entities.MedicationIntakeLog> logs) => real.RemoveRange(logs));
        }

        return new InvoiceService(
            new InvoiceRepository(db),
            new PrescriptionRepository(db),
            new PrescriptionItemRepository(db),
            new InventoryRepository(db),
            new ReminderPreferenceRepository(db),
            new UserRepository(db),
            ClinicServiceTestServices.CaseClinicService(db),
            new UnitOfWork(db),
            inventoryService,
            intakeLogRepo,
            scheduleGenerator,
            notificationService);
    }
}
