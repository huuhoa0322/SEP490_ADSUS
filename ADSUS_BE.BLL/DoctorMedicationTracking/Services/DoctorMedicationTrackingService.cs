using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.DoctorMedicationTracking.DTOs;
using ADSUS_BE.BLL.DoctorMedicationTracking.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.DoctorMedicationTracking.Services;

public sealed class DoctorMedicationTrackingService : IDoctorMedicationTrackingService
{
    private readonly AppDbContext _db;
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly IPatientProfileRepository _patientProfileRepo;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DoctorMedicationTrackingService> _logger;

    private static readonly TimeZoneInfo VietnamZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    // Strip diacritics so "Le" matches "Lê", "Tran" matches "Trần".
    private static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var formD = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    public DoctorMedicationTrackingService(
        AppDbContext db,
        IPrescriptionRepository prescriptionRepo,
        IMedicationIntakeLogRepository intakeLogRepo,
        IPatientProfileRepository patientProfileRepo,
        INotificationService notificationService,
        ILogger<DoctorMedicationTrackingService> logger)
    {
        _db = db;
        _prescriptionRepo = prescriptionRepo;
        _intakeLogRepo = intakeLogRepo;
        _patientProfileRepo = patientProfileRepo;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DoctorPatientListResponse> GetPatientListAsync(
        Guid doctorId,
        string? search,
        string? adherenceLevel,
        bool? hasOverdueDoses,
        DateTime? nowUtc = null,
        CancellationToken ct = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var todayStartUtc = DateOnly.FromDateTime(now);
        var todayEndUtc = todayStartUtc.AddDays(1);

        // DateOnly.ToDateTime() returns DateTimeKind.Unspecified; explicitly set to UTC
        // so comparisons with DateTime.UtcNow are reliable across in-memory DB and PostgreSQL.
        var todayStart = DateTime.SpecifyKind(todayStartUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var todayEnd = DateTime.SpecifyKind(todayEndUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        // Query: Active prescriptions by doctor, with all necessary navigation
        var prescriptions = await _db.Prescriptions
            .AsNoTracking()
            .Where(p => p.DoctorId == doctorId && p.Status == PrescriptionStatus.Active)
            .Include(p => p.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(pp => pp.User)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.MedicationIntakeLogs)
            .ToListAsync(ct);

        // Group by patient
        var patientGroups = prescriptions
            .GroupBy(p => p.Case.PatientProfile.PatientProfileId);

        var result = new List<DoctorPatientDto>();

        foreach (var group in patientGroups)
        {
            var profile = group.First().Case.PatientProfile;
            var patientName = profile.User?.FullName ?? "Bệnh nhân";

            // Search filter (diacritic-insensitive: "Le" matches "Lê", "Tran" matches "Trần")
            if (!string.IsNullOrWhiteSpace(search) &&
                !NormalizeForSearch(patientName).Contains(NormalizeForSearch(search)))
            {
                continue;
            }

            // Collect all intake logs from all prescriptions of this patient
            var allLogs = group
                .SelectMany(p => p.PrescriptionItems)
                .SelectMany(pi => pi.MedicationIntakeLogs)
                .ToList();

            // Today's logs
            var todayLogs = allLogs
                .Where(l => l.ScheduledTime >= todayStart &&
                            l.ScheduledTime < todayEnd)
                .ToList();

            // Today's adherence
            var todayTaken = todayLogs.Count(l => l.ConfirmedAt.HasValue);
            var todayTotal = todayLogs.Count;
            var todayPercent = todayTotal > 0
                ? Math.Round((decimal)todayTaken / todayTotal * 100, 1)
                : 0m;

            var hasOverdue = todayLogs.Any(l => !l.ConfirmedAt.HasValue && l.ScheduledTime <= now);

            // Overall adherence (all logs across all prescriptions)
            var overallTaken = allLogs.Count(l => l.ConfirmedAt.HasValue);
            var overallTotal = allLogs.Count;
            var overallPercent = overallTotal > 0
                ? Math.Round((decimal)overallTaken / overallTotal * 100, 1)
                : 0m;

            var level = AdherenceLevel.FromPercent(todayPercent);

            // Filter by adherenceLevel
            if (!string.IsNullOrWhiteSpace(adherenceLevel) &&
                !string.Equals(level, adherenceLevel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Filter by hasOverdueDoses
            if (hasOverdueDoses == true && !hasOverdue)
            {
                continue;
            }

            result.Add(new DoctorPatientDto(
                profile.PatientProfileId,
                patientName,
                todayTaken,
                todayTotal,
                todayPercent,
                level,
                hasOverdue,
                group.Count()));
        }

        return new DoctorPatientListResponse(
            result.OrderBy(p => p.PatientName).ToList(),
            result.Count);
    }

    public async Task<PatientPrescriptionDetailResponse> GetPatientDetailAsync(
        Guid doctorId,
        Guid patientId,
        DateTime? nowUtc = null,
        CancellationToken ct = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var todayStartUtc = DateOnly.FromDateTime(now);
        var todayEndUtc = todayStartUtc.AddDays(1);
        var todayStart = DateTime.SpecifyKind(todayStartUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var todayEnd = DateTime.SpecifyKind(todayEndUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        // Get patient name
        var patientProfile = await _patientProfileRepo.GetByIdAsync(patientId, ct);
        if (patientProfile is null)
            throw new ResourceNotFoundException("Không tìm thấy hồ sơ bệnh nhân.");

        var patientName = patientProfile.User?.FullName ?? "Bệnh nhân";

        // Query Active prescriptions by doctor and patient
        var prescriptions = await _db.Prescriptions
            .AsNoTracking()
            .Where(p => p.DoctorId == doctorId &&
                        p.Status == PrescriptionStatus.Active &&
                        p.Case.PatientProfileId == patientId)
            .Include(p => p.Case)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.MedicationIntakeLogs)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .ToListAsync(ct);

        var cards = new List<PrescriptionCardDto>();

        foreach (var prescription in prescriptions)
        {
            // Case name
            var caseEntity = prescription.Case;
            var caseName = $"Ca khám – {caseEntity.CreatedAt:dd/MM/yyyy}";

            // All logs for this prescription
            var allLogs = prescription.PrescriptionItems
                .SelectMany(pi => pi.MedicationIntakeLogs)
                .ToList();

            // Today's logs
            var todayLogs = allLogs
                .Where(l => l.ScheduledTime >= todayStart &&
                            l.ScheduledTime < todayEnd)
                .ToList();

            // Today's doses
            var doseDtos = todayLogs
                .OrderBy(l => l.ScheduledTime)
                .Select(l =>
                {
                    var status = DeriveStatus(l, now);
                    var scheduledLocal = TimeZoneInfo.ConvertTimeFromUtc(l.ScheduledTime, VietnamZone);
                    return new TodayDoseDto(
                        l.IntakeId,
                        l.PrescriptionItem?.Medicine?.Name ?? "Thuốc",
                        l.PrescriptionItem?.Dosage ?? "",
                        scheduledLocal.ToString("HH:mm"),
                        status);
                })
                .ToList();

            // Adherence today
            var todayTaken = todayLogs.Count(l => l.ConfirmedAt.HasValue);
            var todayTotal = todayLogs.Count;
            var todayPct = todayTotal > 0
                ? Math.Round((decimal)todayTaken / todayTotal * 100, 1)
                : 0m;
            var adherenceToday = new AdherenceDto(todayTaken, todayTotal, todayPct);

            // Adherence overall
            var overallTaken = allLogs.Count(l => l.ConfirmedAt.HasValue);
            var overallTotal = allLogs.Count;
            var overallPct = overallTotal > 0
                ? Math.Round((decimal)overallTaken / overallTotal * 100, 1)
                : 0m;
            var adherenceOverall = new AdherenceDto(overallTaken, overallTotal, overallPct);

            cards.Add(new PrescriptionCardDto(
                prescription.PrescriptionId,
                caseEntity.CaseId,
                caseName,
                doseDtos,
                adherenceToday,
                adherenceOverall));
        }

        return new PatientPrescriptionDetailResponse(patientName, cards);
    }

    public async Task<RemindResponse> SendRemindersAsync(
        Guid doctorId,
        Guid patientId,
        RemindRequest request,
        DateTime? nowUtc = null,
        CancellationToken ct = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        // Validate prescription belongs to this doctor and patient
        var prescription = await _db.Prescriptions
            .AsNoTracking()
            .Where(p => p.PrescriptionId == request.PrescriptionId &&
                        p.DoctorId == doctorId &&
                        p.Status == PrescriptionStatus.Active &&
                        p.Case.PatientProfileId == patientId)
            .Include(p => p.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(pp => pp.User)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.MedicationIntakeLogs)
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .FirstOrDefaultAsync(ct);

        if (prescription is null)
            throw new ResourceNotFoundException("Không tìm thấy đơn thuốc hoặc bạn không có quyền nhắc đơn này.");

        var patientUser = prescription.Case.PatientProfile.User;
        if (patientUser is null)
            throw new ResourceNotFoundException("Bệnh nhân chưa có tài khoản.");

        var todayStartUtc = DateOnly.FromDateTime(now);
        var todayEndUtc = todayStartUtc.AddDays(1);
        var todayStart = DateTime.SpecifyKind(todayStartUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var todayEnd = DateTime.SpecifyKind(todayEndUtc.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        // Find PENDING/OVERTIME doses for today
        var allLogs = prescription.PrescriptionItems
            .SelectMany(pi => pi.MedicationIntakeLogs)
            .ToList();

        var targetLogs = allLogs
            .Where(l => l.ScheduledTime >= todayStart &&
                        l.ScheduledTime < todayEnd &&
                        !l.ConfirmedAt.HasValue &&
                        l.ScheduledTime <= now)
            .ToList();

        if (targetLogs.Count == 0)
        {
            return new RemindResponse(0, "Không có liều nào cần nhắc hôm nay.");
        }

        var sentCount = 0;
        foreach (var log in targetLogs)
        {
            var medicineName = log.PrescriptionItem?.Medicine?.Name ?? "thuốc";
            var scheduledLocal = TimeZoneInfo.ConvertTimeFromUtc(log.ScheduledTime, VietnamZone);

            try
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientUser.UserId,
                    Type = "medication_reminder",
                    Title = "Nhắc nhở uống thuốc",
                    Body = $"Bác sĩ nhắc: Đã đến giờ uống {medicineName} lúc {scheduledLocal:HH:mm}.",
                    DeepLink = $"/me/medication-intakes/{log.IntakeId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["scheduleId"] = log.IntakeId.ToString(),
                        ["medicineName"] = medicineName,
                        ["scheduledTime"] = log.ScheduledTime.ToString("O"),
                        ["prescriptionId"] = prescription.PrescriptionId.ToString(),
                        ["source"] = "doctor_manual_reminder"
                    }
                }, ct);

                sentCount++;

                _logger.LogInformation(
                    "[DOCTOR-REMINDER] Sent reminder for intake {IntakeId} to user {UserId}",
                    log.IntakeId, patientUser.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[DOCTOR-REMINDER] Failed to send reminder for intake {IntakeId}",
                    log.IntakeId);
            }
        }

        return new RemindResponse(sentCount, $"Đã gửi {sentCount} thông báo nhắc.");
    }

    private static string DeriveStatus(MedicationIntakeLog log, DateTime nowUtc)
        => log.ConfirmedAt.HasValue
            ? AdherenceCalculator.StatusTaken
            : (log.ScheduledTime <= nowUtc
                ? AdherenceCalculator.StatusOvertime
                : AdherenceCalculator.StatusPending);
}
