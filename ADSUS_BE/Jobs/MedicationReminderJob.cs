using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ADSUS_BE.Jobs;

/// <summary>
/// JOB-01 — Nhắc nhở bệnh nhân uống thuốc.
/// Chạy mỗi phút (prod) hoặc 30 giây (dev).
/// GB-08: chỉ gọi IPushNotificationClient (push), không gửi Email/SMS.
/// ReminderWindowMinutes = 30: chỉ gửi push khi ScheduledTime rơi vào khoảng [now-30min, now].
/// </summary>
[DisallowConcurrentExecution]
public sealed class MedicationReminderJob : IJob
{
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly IPatientProfileRepository _patientProfileRepo;
    private readonly IPushNotificationClient _pushClient;
    private readonly ILogger<MedicationReminderJob> _logger;

    private const int ReminderWindowMinutes = 30;

    public MedicationReminderJob(
        IMedicationIntakeLogRepository intakeLogRepo,
        IPatientProfileRepository patientProfileRepo,
        IPushNotificationClient pushClient,
        ILogger<MedicationReminderJob> logger)
    {
        _intakeLogRepo = intakeLogRepo;
        _patientProfileRepo = patientProfileRepo;
        _pushClient = pushClient;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("[JOB-01] Medication reminder job started at {Time}", DateTime.UtcNow);

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-ReminderWindowMinutes);

        // Find pending logs where:
        // 1. ScheduledTime falls within [now-30min, now] (first pass when dose time is reached)
        // 2. Not yet confirmed (ConfirmedAt == null)
        var pendingReminders = await _intakeLogRepo.ListDueRemindersAsync(
            windowStart, ReminderWindowMinutes, context.CancellationToken);

        _logger.LogInformation("[JOB-01] Found {Count} pending reminders", pendingReminders.Count);

        var sentCount = 0;

        foreach (var log in pendingReminders)
        {
            try
            {
                var patientProfile = await _patientProfileRepo.GetByIdAsync(
                    log.PrescriptionItem?.Prescription?.Case?.PatientProfileId ?? Guid.Empty,
                    context.CancellationToken);

                if (patientProfile?.User is null)
                {
                    _logger.LogDebug(
                        "[JOB-01] No FCM token for patient profile {ProfileId}",
                        patientProfile?.PatientProfileId);
                    continue;
                }

                var medicineName = log.PrescriptionItem?.Medicine?.Name ?? "thuốc";
                var scheduledTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(
                    log.ScheduledTime,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));

                var message = new PushMessage(
                    Title: "Nhắc nhở uống thuốc",
                    Body: $"Đã đến giờ uống {medicineName} lúc {scheduledTimeLocal:HH:mm}.",
                    DeepLink: $"/me/medication-intakes/{log.IntakeId}",
                    Data: new Dictionary<string, string>
                    {
                        ["type"] = "medication_reminder",
                        ["intakeId"] = log.IntakeId.ToString(),
                        ["medicineName"] = medicineName,
                        ["scheduledTime"] = log.ScheduledTime.ToString("O"),
                    });

                await _pushClient.SendToUserAsync(patientProfile.UserId, message, context.CancellationToken);

                sentCount++;
                _logger.LogInformation(
                    "[JOB-01] Sent reminder for intake {IntakeId} to user {UserId}",
                    log.IntakeId, patientProfile.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-01] Failed to send reminder for intake {IntakeId}", log.IntakeId);
            }
        }

        _logger.LogInformation(
            "[JOB-01] Medication reminder job completed. Sent {Sent}/{Total} reminders",
            sentCount, pendingReminders.Count);
    }
}
