using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
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

        // Validate medicine: if MedicineId is provided, check catalog exists.
        // If MedicineId is null, doctor typed a free-text name — skip catalog validation.
        foreach (var item in request.Items)
        {
            if (item.MedicineId.HasValue)
            {
                var medicine = await _medicineRepo.GetByIdAsync(item.MedicineId.Value, ct);
                if (medicine is null)
                    throw new ResourceNotFoundException($"Thuốc '{item.MedicineId}' không tồn tại trong danh mục.");
            }
        }

        // Get patient reminder preferences
        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.PatientReminderPreferences)
            .FirstOrDefaultAsync(p => p.PatientProfileId == caseEntity.PatientProfileId, ct);

        var morningPref = patientProfile?.PatientReminderPreferences
            .FirstOrDefault(p => p.CustomTime.Hour < 12);
        var middayPref = patientProfile?.PatientReminderPreferences
            .FirstOrDefault(p => p.CustomTime.Hour is >= 11 and <= 14);
        var eveningPref = patientProfile?.PatientReminderPreferences
            .FirstOrDefault(p => p.CustomTime.Hour >= 18);

        var morningTime = morningPref?.CustomTime ?? new TimeOnly(7, 0);
        var middayTime  = middayPref?.CustomTime  ?? new TimeOnly(12, 0);
        var eveningTime = eveningPref?.CustomTime  ?? new TimeOnly(20, 0);

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
            var prescriptionItem = new PrescriptionItem
            {
                PrescriptionItemId = itemId,
                PrescriptionId = prescription.PrescriptionId,
                MedicineId = itemDto.MedicineId,
                MedicineName = itemDto.MedicineName,
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

        await _db.SaveChangesAsync(ct);

        // Reload with navigation for response
        var response = await _prescriptionRepo.GetByIdAsync(prescription.PrescriptionId, ct)
            ?? throw new InvalidOperationException("Prescription not found after save.");

        return PrescriptionResponseMapper.FromEntity(response);
    }
}