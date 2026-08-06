using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// UC-11 (Patient) — xem lịch uống thuốc.
/// UC-17 — xác nhận đã uống (GB-01: one-way Pending → Taken).
/// Patient chỉ xem đơn của CHÍNH MÌNH (actorId = patientId).
/// </summary>
public sealed class MedicationIntakeService : IMedicationIntakeService
{
    private readonly AppDbContext _db;
    private readonly IMedicationIntakeLogRepository _intakeLogRepo;
    private readonly IPrescriptionRepository _prescriptionRepo;

    public MedicationIntakeService(
        AppDbContext db,
        IMedicationIntakeLogRepository intakeLogRepo,
        IPrescriptionRepository prescriptionRepo)
    {
        _db = db;
        _intakeLogRepo = intakeLogRepo;
        _prescriptionRepo = prescriptionRepo;
    }

    public async Task<IReadOnlyList<IntakeLogResponse>> ListByPrescriptionAsync(
        Guid patientId,
        Guid prescriptionId,
        CancellationToken ct = default)
    {
        // Load prescription + verify ownership
        var prescription = await _prescriptionRepo.GetByIdAsync(prescriptionId, ct);
        if (prescription is null)
            throw new ResourceNotFoundException("Đơn thuốc không tồn tại.");

        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientId, ct)
            ?? throw new ResourceNotFoundException("Hồ sơ bệnh nhân không tồn tại.");

        if (prescription.Case?.PatientProfileId != patientProfile.PatientProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền xem đơn thuốc này.");

        var logs = await _intakeLogRepo.ListByPrescriptionAsync(prescriptionId, ct);
        return logs.Select(IntakeLogResponseMapper.FromEntity).ToList();
    }

    public async Task<IReadOnlyList<IntakeLogResponse>> ListUpcomingAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientId, ct)
            ?? throw new ResourceNotFoundException("Hồ sơ bệnh nhân không tồn tại.");

        var logs = await _intakeLogRepo.ListUpcomingAsync(patientProfile.PatientProfileId, ct);
        return logs.Select(IntakeLogResponseMapper.FromEntity).ToList();
    }

    public async Task ConfirmTakenAsync(
        Guid patientId,
        Guid intakeId,
        CancellationToken ct = default)
    {
        var log = await _intakeLogRepo.GetByIdAsync(intakeId, ct)
            ?? throw new ResourceNotFoundException($"Liều thuốc '{intakeId}' không tồn tại.");

        // Verify patient owns this intake log
        var prescription = await _prescriptionRepo.GetByIdAsync(log.PrescriptionItem?.PrescriptionId ?? Guid.Empty, ct)
            ?? throw new ResourceNotFoundException("Đơn thuốc không tồn tại.");

        var patientProfile = await _db.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientId, ct)
            ?? throw new ResourceNotFoundException("Hồ sơ bệnh nhân không tồn tại.");

        if (prescription.Case?.PatientProfileId != patientProfile.PatientProfileId)
            throw new UnauthorizedAccessException("Bạn không có quyền xác nhận liều thuốc này.");

        // GB-01: one-way — only update if still Pending
        if (log.ConfirmedAt.HasValue)
            return; // Idempotent: already Taken, return 204

        // Không cho bệnh nhân xác nhận liều trước giờ uống — tránh gian lận tuân thủ
        // và đảm bảo dữ liệu ConfirmedAt phản ánh đúng thời điểm uống thực tế.
        var now = DateTime.UtcNow;
        if (log.ScheduledTime > now)
            throw new BusinessException(
                "Chưa đến giờ uống thuốc. Không thể xác nhận sớm hơn giờ đã hẹn.");

        await _intakeLogRepo.ConfirmTakenAsync(intakeId, now, ct);
    }

    public async Task<AdherenceSummary> GetAdherenceAsync(
        Guid prescriptionId,
        CancellationToken ct = default)
    {
        var prescription = await _prescriptionRepo.GetByIdAsync(prescriptionId, ct)
            ?? throw new ResourceNotFoundException("Đơn thuốc không tồn tại.");

        var items = await _db.PrescriptionItems
            .AsNoTracking()
            .Include(i => i.MedicationIntakeLogs)
            .Where(i => i.PrescriptionId == prescriptionId)
            .ToListAsync(ct);

        var logs = items.SelectMany(i => i.MedicationIntakeLogs).ToList();
        var now = DateTime.UtcNow;
        var pct = AdherenceCalculator.Calculate(logs, now);

        return new AdherenceSummary(
            PatientId: prescription.Case?.PatientProfile?.UserId ?? Guid.Empty,
            FromUtc: logs.Count > 0 ? logs.Min(l => l.ScheduledTime) : now,
            ToUtc: logs.Count > 0 ? logs.Max(l => l.ScheduledTime) : now,
            TotalDoses: logs.Count,
            TakenDoses: logs.Count(l => l.ConfirmedAt.HasValue),
            PendingDoses: logs.Count(l => !l.ConfirmedAt.HasValue),
            AdherencePercent: pct,
            AdherenceLevel: AdherenceLevel.FromPercent(pct));
    }
}