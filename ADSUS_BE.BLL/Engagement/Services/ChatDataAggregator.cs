using ADSUS_BE.BLL.Engagement.DTOs;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.Engagement.Services;

/// <summary>
/// Tổng hợp dữ liệu bệnh nhân cho chatbot — từ 7 data sources trở lên.
///
/// Selective query: chỉ query sources cần thiết dựa trên intent đã detect.
/// allergies/diseases không có repository riêng → query trực tiếp qua AppDbContext.
///
/// Giới hạn context để tránh token bloat khi gửi LLM:
/// - Đơn thuốc: 2 đơn gần nhất, mỗi đơn 5 items
/// - Ca khám: 3 ca gần nhất
/// - Lịch hẹn: 3 lịch BOOKED tiếp theo
/// - Nhật ký SK: 7 ngày gần nhất
/// - Blog: 3 bài gần nhất
/// </summary>
public sealed class ChatDataAggregator : IChatDataAggregator
{
    private const int MaxPrescriptions = 2;
    private const int MaxItemsPerPrescription = 5;
    private const int MaxRecentCases = 3;
    private const int MaxUpcomingAppointments = 3;
    private const int HealthLogDays = 7;
    private const int MaxRecentBlogs = 3;

    private readonly AppDbContext _db;
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly IHealthLogRepository _healthLogRepo;
    private readonly IBlogPostRepository _blogPostRepo;

    public ChatDataAggregator(
        AppDbContext db,
        IPrescriptionRepository prescriptionRepo,
        IMedicationIntakeLogRepository intakeLogRepo,
        IAppointmentRepository appointmentRepo,
        ICaseRepository caseRepo,
        IHealthLogRepository healthLogRepo,
        IBlogPostRepository blogPostRepo)
    {
        _db = db;
        _prescriptionRepo = prescriptionRepo;
        _intakeLogRepo = intakeLogRepo;
        _appointmentRepo = appointmentRepo;
        _caseRepo = caseRepo;
        _healthLogRepo = healthLogRepo;
        _blogPostRepo = blogPostRepo;
    }

    public async Task<PatientChatContext?> BuildContextAsync(
        Guid userId, IntentResult intent, CancellationToken ct = default)
    {
        // Lấy patientProfileId trước — dùng cho mọi query phía sau
        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (patientProfile == null)
            return null;

        var patientProfileId = patientProfile.PatientProfileId;
        var sources = intent.TriggeredSources;

        // ── Always fetch basic info ──────────────────────────────────────────────
        var basicInfo = BuildBasicInfo(patientProfile);

        // ── Selective query based on intent ─────────────────────────────────────
        PatientBasicContextDto? basic = basicInfo;
        IReadOnlyList<PrescriptionContextDto>? prescriptions = null;
        IReadOnlyList<TodayIntakeContextDto>? todayIntakes = null;
        IReadOnlyList<UpcomingAppointmentContextDto>? appointments = null;
        IReadOnlyList<CaseHistoryContextDto>? cases = null;
        IReadOnlyList<AllergyContextDto>? allergies = null;
        IReadOnlyList<DiseaseContextDto>? diseases = null;
        IReadOnlyList<HealthLogContextDto>? healthLogs = null;
        IReadOnlyList<BlogPostListItemResponse>? blogs = null;

        if (sources.HasFlag(DataSource.ActivePrescriptions))
            prescriptions = await BuildPrescriptionsAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.TodayIntakes))
            todayIntakes = await BuildTodayIntakesAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.UpcomingAppointments))
            appointments = await BuildUpcomingAppointmentsAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.RecentCases))
            cases = await BuildRecentCasesAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.Allergies))
            allergies = await BuildAllergiesAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.Diseases))
            diseases = await BuildDiseasesAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.RecentHealthLogs))
            healthLogs = await BuildHealthLogsAsync(patientProfileId, ct);

        if (sources.HasFlag(DataSource.RecentBlogs))
            blogs = await BuildRecentBlogsAsync(ct);

        return new PatientChatContext(
            BasicInfo: basic,
            ActivePrescriptions: prescriptions,
            TodayIntakes: todayIntakes,
            UpcomingAppointments: appointments,
            RecentCases: cases,
            Allergies: allergies,
            Diseases: diseases,
            RecentHealthLogs: healthLogs,
            RecentBlogs: blogs);
    }

    // ─── Basic info ──────────────────────────────────────────────────────────────

    private static PatientBasicContextDto BuildBasicInfo(PatientProfile profile)
    {
        var user = profile.User;
        var age = user?.DateOfBirth is { } dob
            ? DateTime.UtcNow.Year - dob.Year
                - (DateTime.UtcNow < dob.AddYears(DateTime.UtcNow.Year - dob.Year).ToDateTime(TimeOnly.MinValue) ? 1 : 0)
            : (int?)null;

        return new PatientBasicContextDto(
            user?.FullName ?? string.Empty,
            user?.DateOfBirth,
            age);
    }

    // ─── Prescriptions ───────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<PrescriptionContextDto>> BuildPrescriptionsAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var prescriptions = await _db.Prescriptions
            .AsNoTracking()
            .Include(p => p.PrescriptionItems.Take(MaxItemsPerPrescription))
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Case)
            .Where(p => p.Case.PatientProfileId == patientProfileId)
            .OrderByDescending(p => p.PrescribedDate)
            .Take(MaxPrescriptions)
            .ToListAsync(ct);

        return prescriptions
            .Where(p => p.Status == PrescriptionStatus.Active)
            .Select(p => new PrescriptionContextDto(
                p.PrescriptionId,
                p.PrescribedDate,
                p.GeneralNote,
                p.PrescriptionItems
                    .Take(MaxItemsPerPrescription)
                    .Select(pi => new PrescriptionItemContextDto(
                        pi.Medicine?.Name ?? string.Empty,
                        pi.Dosage,
                        pi.Instructions,
                        pi.DurationDays,
                        pi.StartDate))
                    .ToList()))
            .ToList();
    }

    // ─── Today intakes ────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<TodayIntakeContextDto>> BuildTodayIntakesAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var logs = await _intakeLogRepo.ListUpcomingAsync(patientProfileId, ct);

        return logs
            .Select(log => new TodayIntakeContextDto(
                log.IntakeId,
                log.PrescriptionItem?.Medicine?.Name ?? string.Empty,
                log.PrescriptionItem?.Dosage ?? string.Empty,
                log.PrescriptionItem?.Instructions,
                log.ScheduledTime,
                DeriveStatus(log)))
            .ToList();
    }

    private static string DeriveStatus(MedicationIntakeLog log)
    {
        if (log.ConfirmedAt.HasValue)
            return "Đã uống";
        var now = DateTime.UtcNow;
        return log.ScheduledTime <= now ? "Quá giờ" : "Chưa uống";
    }

    // ─── Upcoming appointments ───────────────────────────────────────────────────

    private async Task<IReadOnlyList<UpcomingAppointmentContextDto>> BuildUpcomingAppointmentsAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var nowDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var appointments = await _appointmentRepo.ListByPatientAsync(patientProfileId, ct);

        return appointments
            .Where(a => a.Slot.SlotDate >= nowDate && a.Status == AppointmentStatus.Booked)
            .OrderBy(a => a.Slot.SlotDate)
            .ThenBy(a => a.Slot.StartTime)
            .Take(MaxUpcomingAppointments)
            .Select(a => new UpcomingAppointmentContextDto(
                a.AppointmentId,
                a.Slot.SlotDate,
                a.Slot.StartTime,
                a.Slot.EndTime,
                a.Slot.Doctor?.FullName ?? string.Empty,
                a.Reason))
            .ToList();
    }

    // ─── Recent cases ────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<CaseHistoryContextDto>> BuildRecentCasesAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var (cases, _) = await _caseRepo.SearchByPatientAsync(
            patientProfileId, null, "desc", 1, MaxRecentCases, ct);

        return cases
            .OrderByDescending(c => c.VisitDate)
            .Take(MaxRecentCases)
            .Select(c => new CaseHistoryContextDto(
                c.CaseId,
                c.VisitDate,
                c.FinalDiagnosis,
                c.DoctorConclusion,
                c.Doctor?.FullName ?? string.Empty))
            .ToList();
    }

    // ─── Allergies ───────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<AllergyContextDto>> BuildAllergiesAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var allergies = await _db.PatientAllergies
            .AsNoTracking()
            .Include(pa => pa.AllergyType)
            .Where(pa => pa.PatientProfileId == patientProfileId)
            .ToListAsync(ct);

        return allergies
            .Select(a => new AllergyContextDto(
                a.Id,
                a.AllergyType?.Name ?? string.Empty,
                a.Note))
            .ToList();
    }

    // ─── Diseases ───────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<DiseaseContextDto>> BuildDiseasesAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var diseases = await _db.PatientDiseases
            .AsNoTracking()
            .Include(pd => pd.Disease)
            .Where(pd => pd.PatientProfileId == patientProfileId)
            .ToListAsync(ct);

        return diseases
            .Select(d => new DiseaseContextDto(
                d.Id,
                d.Disease?.Name ?? string.Empty,
                d.Note))
            .ToList();
    }

    // ─── Health logs ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<HealthLogContextDto>> BuildHealthLogsAsync(
        Guid patientProfileId, CancellationToken ct)
    {
        var logs = await _healthLogRepo.GetLatestByPatientAsync(
            patientProfileId, HealthLogDays * 3, ct);

        return logs
            .Where(l => l.LogDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-HealthLogDays)))
            .OrderByDescending(l => l.LogDate)
            .Take(HealthLogDays)
            .Select(l => new HealthLogContextDto(l.LogDate, l.Content))
            .ToList();
    }

    // ─── Blog posts ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<BlogPostListItemResponse>> BuildRecentBlogsAsync(
        CancellationToken ct)
    {
        var posts = await _blogPostRepo.ListPublishedAsync(ct);

        return posts
            .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .Take(MaxRecentBlogs)
            .Select(p => new BlogPostListItemResponse
            {
                Id = p.PostId,
                Title = p.Title,
                PublishedAt = p.PublishedAt ?? p.CreatedAt,
                AuthorName = p.Author?.FullName ?? string.Empty,
            })
            .ToList();
    }
}
