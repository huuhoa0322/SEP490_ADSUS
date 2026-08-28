using System.Collections.Immutable;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.PrescriptionAdherence;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// UC-18 — Bác sĩ kê đơn thuốc từ ca đã được duyệt.
/// GB-04: bác sĩ phải tồn tại, Role == Doctor, Status == Active.
/// UC-18 BR-01: case phải ở trạng thái Confirmed.
/// GB-01: đơn mới luôn Active (không Draft).
/// </summary>
public sealed class PrescriptionService : IPrescriptionService
{
    private readonly AppDbContext _db;
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IPrescriptionItemRepository _itemRepo;
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly IUserRepository _userRepo;
    private readonly IMedicineRepository _medicineRepo;
    private readonly IMedicationIntakeScheduleGenerator _scheduleGenerator;

    public PrescriptionService(
        AppDbContext db,
        IPrescriptionRepository prescriptionRepo,
        IPrescriptionItemRepository itemRepo,
        IMedicationIntakeLogRepository intakeLogRepo,
        ICaseRepository caseRepo,
        IUserRepository userRepo,
        IMedicineRepository medicineRepo,
        IMedicationIntakeScheduleGenerator scheduleGenerator)
    {
        _db = db;
        _prescriptionRepo = prescriptionRepo;
        _itemRepo = itemRepo;
        _intakeLogRepo = intakeLogRepo;
        _caseRepo = caseRepo;
        _userRepo = userRepo;
        _medicineRepo = medicineRepo;
        _scheduleGenerator = scheduleGenerator;
    }

    public async Task<PrescriptionResponse> CreateAsync(
        Guid actorId,
        CreatePrescriptionRequest request,
        CancellationToken ct = default)
    {
        // GB-04: Validate doctor
        var doctor = await _userRepo.GetByIdAsync(actorId, ct);
        if (doctor is null)
            throw new ResourceNotFoundException("Tài khoản bác sĩ không tồn tại.");

        if (doctor.Role != UserRole.Doctor)
            throw new BusinessException("Chỉ bác sĩ mới được kê đơn thuốc.");

        if (doctor.Status != UserStatus.Active)
            throw new BusinessException("Tài khoản bác sĩ đang không hoạt động.");

        // UC-18 BR-01: Validate case is Confirmed
        var caseEntity = await _caseRepo.GetByIdAsync(request.CaseId, ct);
        if (caseEntity is null)
            throw new ResourceNotFoundException($"Ca khám '{request.CaseId}' không tồn tại.");

        if (caseEntity.Status != CaseStatus.Confirmed)
            throw new BusinessException("Chỉ ca đã được duyệt (Confirmed) mới được kê đơn thuốc.");

        // Validate case belongs to this doctor
        if (caseEntity.DoctorId != actorId)
            throw new BusinessException("Bác sĩ không có quyền kê đơn cho ca khám này.");

        // Option A: lookup-or-create medicine by name (case-insensitive).
        // Handles both: doctor picks from catalog OR types a new name.
        var medicineCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Get patient reminder preferences
        var patientPref = await _db.PatientReminderPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientProfileId == caseEntity.PatientProfileId, ct);

        var morningTime = patientPref?.MorningTime ?? new TimeOnly(7, 0);
        var middayTime  = patientPref?.MiddayTime ?? new TimeOnly(12, 0);
        var eveningTime = patientPref?.EveningTime ?? new TimeOnly(20, 0);

        var now = DateTime.UtcNow;

        // Create prescription
        var prescription = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = request.CaseId,
            DoctorId = actorId,
            PrescribedDate = DateOnly.FromDateTime(now),
            GeneralNote = request.GeneralNote,
            CreatedAt = now,
            UpdatedAt = now,
            Status = PrescriptionStatus.Active,
        };

        await _prescriptionRepo.AddAsync(prescription, ct);

        // Create items + generate intake logs
        var allLogs = new List<MedicationIntakeLog>();

        foreach (var itemDto in request.Items)
        {
            var itemId = Guid.NewGuid();

            // Lookup or create medicine by name
            if (!medicineCache.TryGetValue(itemDto.MedicineName, out var medicineId))
            {
                var existing = await _medicineRepo.FindByNameAsync(itemDto.MedicineName, ct);
                if (existing is null || existing.Status == MedicineStatus.Inactive)
                {
                    throw new BusinessException($"Thuốc '{itemDto.MedicineName}' không tồn tại trong hệ thống hoặc đã bị ngừng sử dụng. Vui lòng chọn thuốc từ danh sách.");
                }
                
                medicineId = existing.MedicineId;
                medicineCache[itemDto.MedicineName] = medicineId;
            }

            var prescriptionItem = new PrescriptionItem
            {
                PrescriptionItemId = itemId,
                PrescriptionId = prescription.PrescriptionId,
                MedicineId = medicineId,
                Dosage = itemDto.Dosage,
                DurationDays = itemDto.DurationDays,
                StartDate = itemDto.StartDate,
                Instructions = itemDto.Instructions,
                ScheduleSlots = itemDto.ScheduleSlots
                    .Select(s => (ReminderSlot)(int)s)
                    .ToArray(),
            };
            await _itemRepo.AddAsync(prescriptionItem, ct);

            // Generate intake logs
            var itemWithPatient = new PrescriptionItemWithPatient(
                itemId,
                caseEntity.PatientProfileId,
                itemDto.StartDate,
                itemDto.DurationDays);

            var scheduledDoses = await _scheduleGenerator.GenerateAsync(
                itemWithPatient,
                itemDto.ScheduleSlots,
                morningTime,
                middayTime,
                eveningTime,
                DateTime.UtcNow,
                ct);

            foreach (var dose in scheduledDoses)
            {
                // Idempotent: skip if already exists in DB
                var existing = await _intakeLogRepo.FindByItemAndTimeAsync(
                    dose.PrescriptionItemId, dose.ScheduledTimeUtc, ct);
                if (existing is not null) continue;

                allLogs.Add(new MedicationIntakeLog
                {
                    IntakeId = Guid.NewGuid(),
                    PrescriptionItemId = dose.PrescriptionItemId,
                    ScheduledTime = dose.ScheduledTimeUtc,
                    ConfirmedAt = null,
                });
            }
        }

        if (allLogs.Count > 0)
            await _intakeLogRepo.AddRangeAsync(allLogs, ct);

        // Sau khi tạo đơn thuốc → tự động chuyển ca sang END (trạng thái cuối).
        // Dùng GetForUpdateAsync để lấy entity có theo dõi thay đổi.
        var trackedCase = await _caseRepo.GetForUpdateAsync(request.CaseId, ct)
            ?? throw new ResourceNotFoundException($"Ca '{request.CaseId}' not found.");
        trackedCase.Status = CaseStatus.End;
        trackedCase.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Reload with navigation for response
        var response = await _prescriptionRepo.GetByIdAsync(prescription.PrescriptionId, ct)
            ?? throw new InvalidOperationException("Prescription not found after save.");

        return PrescriptionResponseMapper.FromEntity(response);
    }

    public async Task<PrescriptionResponse?> GetByCaseIdAsync(Guid caseId, CancellationToken ct = default)
    {
        var prescription = await _prescriptionRepo.GetByCaseIdAsync(caseId, ct);
        return prescription is null ? null : PrescriptionResponseMapper.FromEntity(prescription);
    }

    public async Task<IReadOnlyList<PrescriptionWithComplianceResponse>> GetCasePrescriptionsWithComplianceAsync(
        Guid actorId,
        Guid caseId,
        CancellationToken ct = default)
    {
        var prescriptions = await _prescriptionRepo.ListByCaseAsync(caseId, ct);

        if (prescriptions.Count == 0)
            return Array.Empty<PrescriptionWithComplianceResponse>();

        // Tách đơn của actor và đơn bác sĩ khác
        var ownPrescriptions = prescriptions.Where(p => p.DoctorId == actorId).ToList();
        var otherPrescriptions = prescriptions.Where(p => p.DoctorId != actorId).ToList();

        // Lấy stats cho đơn của actor
        var ownItemIds = ownPrescriptions
            .SelectMany(p => p.PrescriptionItems)
            .Select(i => i.PrescriptionItemId)
            .ToList();

        var stats = ownItemIds.Count > 0
            ? await _intakeLogRepo.GetIntakeStatsByPrescriptionAsync(ownItemIds, ct)
            : ImmutableDictionary<Guid, IntakeStats>.Empty;

        var result = new List<PrescriptionWithComplianceResponse>();

        foreach (var p in prescriptions)
        {
            var isOwn = p.DoctorId == actorId;

            var items = p.PrescriptionItems.Select(pi =>
            {
                double? itemPct = null;
                if (isOwn && stats.TryGetValue(pi.PrescriptionItemId, out var s))
                    itemPct = s.AdherencePercent;

                return new PrescriptionItemWithComplianceResponse(
                    pi.PrescriptionItemId,
                    pi.MedicineId,
                    pi.Medicine?.Name ?? string.Empty,
                    pi.Dosage,
                    pi.DurationDays,
                    pi.StartDate,
                    pi.Instructions,
                    pi.ScheduleSlots?.Select(s => s.ToString()).ToList(),
                    itemPct);
            }).ToList();

            double? prescriptionPct = null;
            if (isOwn)
            {
                // Tính % đơn = tổng taken / tổng doses từ stats dict (trực tiếp, không qua items list
                // vì items list chỉ chứa stats có trong stats dict, kể cả 0-doses entries)
                var prescriptionItemIds = items.Select(i => i.PrescriptionItemId).ToHashSet();
                var totalTaken = 0;
                var totalDoses = 0;
                foreach (var itemId in prescriptionItemIds)
                {
                    if (stats.TryGetValue(itemId, out var s))
                    {
                        totalTaken += s.TakenDoses;
                        totalDoses += s.TotalDoses;
                    }
                }
                if (totalDoses > 0)
                    prescriptionPct = Math.Round(totalTaken * 100.0 / totalDoses, 1);
                else
                    prescriptionPct = 0; // No logs yet = 0%, not null
            }

            result.Add(new PrescriptionWithComplianceResponse(
                p.PrescriptionId,
                p.CaseId,
                p.DoctorId,
                p.PrescribedDate,
                p.GeneralNote,
                p.CreatedAt,
                p.UpdatedAt,
                prescriptionPct,
                items));
        }

        return result;
    }
}