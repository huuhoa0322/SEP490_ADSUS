using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// NoShowService trước đây ghi thẳng AppDbContext; nay đi qua IAppointmentRepository, ICaseService
/// và IPatientProfileService (P11 review 25/09/2026). Helper dựng service THẬT trên repository chạy
/// cùng DB InMemory mà test đã nạp dữ liệu, để test cũ (nạp lịch hẹn vào DB rồi gọi check-in / job)
/// giữ nguyên ý nghĩa.
/// </summary>
internal static class NoShowTestServices
{
    public static NoShowService Create(AppDbContext db, INotificationService notifications, int graceTimeMinutes = 15) => new(
        Options.Create(new NoShowSettings { GraceTimeMinutes = graceTimeMinutes }),
        new AppointmentRepository(db),
        AppointmentRepositoryTestBridge.RealCaseService(db),
        PatientAccountTestServices.PatientProfiles(db),
        notifications,
        NullLogger<NoShowService>.Instance);
}
