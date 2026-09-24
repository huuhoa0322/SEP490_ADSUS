using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// AppointmentService trước đây truy vấn thẳng AppDbContext, nên test của nó mock repository
/// nhưng nạp dữ liệu thẳng vào DB InMemory. Các truy vấn đó nay đã chuyển vào repository (P11
/// review 24/09/2026) — helper này nối các method MỚI của mock sang repository thật chạy trên
/// CÙNG DB InMemory đó, để dữ liệu test đã nạp vẫn được đọc đúng như trước. Setup riêng của từng
/// test cho các method cũ (GetByIdAsync, GetByIdForUpdateAsync...) giữ nguyên, không bị ảnh hưởng.
/// </summary>
internal static class AppointmentRepositoryTestBridge
{
    public static Mock<IAppointmentRepository> BackedBy(this Mock<IAppointmentRepository> mock, AppDbContext db)
    {
        var real = new AppointmentRepository(db);

        mock.Setup(r => r.ListForPatientOrBookerAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<AppointmentStatus?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid profileId, Guid? bookerId, AppointmentStatus? status, CancellationToken ct) =>
                real.ListForPatientOrBookerAsync(profileId, bookerId, status, ct));
        mock.Setup(r => r.CountUserCancellationsSinceAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid userId, DateTime since, CancellationToken ct) => real.CountUserCancellationsSinceAsync(userId, since, ct));
        mock.Setup(r => r.GetCheckinQueuePageAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<AppointmentStatus?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((DateOnly from, DateOnly to, string? search, AppointmentStatus? status, bool excludeCancelled, int page, int pageSize, CancellationToken ct) =>
                real.GetCheckinQueuePageAsync(from, to, search, status, excludeCancelled, page, pageSize, ct));
        mock.Setup(r => r.GetNextForDoctorOnDateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns((Guid doctorId, DateOnly date, CancellationToken ct) => real.GetNextForDoctorOnDateAsync(doctorId, date, ct));
        mock.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => real.GetByIdForUpdateAsync(id, ct));
        mock.Setup(r => r.GetByIdWithDetailsForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => real.GetByIdWithDetailsForUpdateAsync(id, ct));
        mock.Setup(r => r.GetLatestBookedByCaseForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, CancellationToken ct) => real.GetLatestBookedByCaseForUpdateAsync(caseId, ct));
        mock.Setup(r => r.GetLatestByCaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, CancellationToken ct) => real.GetLatestByCaseAsync(caseId, ct));
        mock.Setup(r => r.GetForRescheduleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => real.GetForRescheduleAsync(id, ct));
        mock.Setup(r => r.GetWithBookerAndRelationshipAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => real.GetWithBookerAndRelationshipAsync(id, ct));
        mock.Setup(r => r.GetFirstOnDateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AppointmentStatus>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid profileId, DateOnly date, IReadOnlyCollection<AppointmentStatus> statuses, Guid? excludeId, CancellationToken ct) =>
                real.GetFirstOnDateAsync(profileId, date, statuses, excludeId, ct));
        mock.Setup(r => r.CountBookedByProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid profileId, CancellationToken ct) => real.CountBookedByProfileAsync(profileId, ct));
        mock.Setup(r => r.CountSelfBookedByProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid profileId, CancellationToken ct) => real.CountSelfBookedByProfileAsync(profileId, ct));
        mock.Setup(r => r.CountBookedForOthersByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid userId, CancellationToken ct) => real.CountBookedForOthersByUserAsync(userId, ct));
        mock.Setup(r => r.CountProfileCancellationsSinceAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid profileId, DateTime since, CancellationToken ct) => real.CountProfileCancellationsSinceAsync(profileId, since, ct));
        mock.Setup(r => r.ExistsActiveInSlotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid profileId, Guid slotId, CancellationToken ct) => real.ExistsActiveInSlotAsync(profileId, slotId, ct));
        mock.Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Returns((Appointment appointment, CancellationToken ct) => real.AddAsync(appointment, ct));
        mock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => real.SaveChangesAsync(ct));

        return mock;
    }

    /// <summary>
    /// Các bước chuyển trạng thái Case do lịch hẹn kích hoạt (chuyển từ AppointmentService sang
    /// ICaseService) chạy bằng CaseService THẬT trên cùng DB InMemory — test kiểm tra đúng logic
    /// thật chứ không phải bản chép lại. Các setup khác của mock (CreateFromBookingAsync...) giữ nguyên.
    /// </summary>
    public static Mock<ICaseService> BackedBy(this Mock<ICaseService> mock, AppDbContext db)
    {
        var real = new CaseService(
            new CaseRepository(db),
            Mock.Of<IUltrasoundImageRepository>(),
            Mock.Of<IPatientProfileRepository>(),
            new UserRepository(db),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance);

        mock.Setup(s => s.StageCheckinFromAppointmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, CancellationToken ct) => real.StageCheckinFromAppointmentAsync(caseId, ct));
        mock.Setup(s => s.StageCancelFromAppointmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, CancellationToken ct) => real.StageCancelFromAppointmentAsync(caseId, ct));
        mock.Setup(s => s.StageReplaceSymptomsFromAppointmentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<SymptomInput>?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, IReadOnlyList<SymptomInput>? symptoms, CancellationToken ct) =>
                real.StageReplaceSymptomsFromAppointmentAsync(caseId, symptoms, ct));
        mock.Setup(s => s.ListSymptomsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, CancellationToken ct) => real.ListSymptomsAsync(caseId, ct));
        mock.Setup(s => s.StageRescheduleFromAppointmentAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CaseStatus?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, Guid? doctorId, CaseStatus? status, CancellationToken ct) =>
                real.StageRescheduleFromAppointmentAsync(caseId, doctorId, status, ct));

        return mock;
    }

    public static Mock<IScheduleSlotRepository> BackedBy(this Mock<IScheduleSlotRepository> mock, AppDbContext db)
    {
        var real = new ScheduleSlotRepository(db);

        // Mặc định đọc slot thật từ DB InMemory; test nào tự Setup GetByIdForUpdateAsync sau
        // lời gọi này sẽ ghi đè (Moq dùng Setup đăng ký sau cùng).
        mock.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken ct) => real.GetByIdForUpdateAsync(id, ct));
        mock.Setup(r => r.ListBookedForDoctorAfterForUpdateAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
            .Returns((Guid doctorId, DateOnly date, TimeOnly after, CancellationToken ct) =>
                real.ListBookedForDoctorAfterForUpdateAsync(doctorId, date, after, ct));
        mock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => real.SaveChangesAsync(ct));

        return mock;
    }
}
