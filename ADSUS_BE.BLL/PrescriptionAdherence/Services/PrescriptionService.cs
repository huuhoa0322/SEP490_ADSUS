using System.Security.Claims;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

/// <summary>
/// Module 7 — service nghiệp vụ cho Prescription & Adherence.
/// Triển khai UC-11 (đọc) + UC-18 (ghi).
/// Quy tắc:
///   - DoctorId lấy từ JWT claims (ClaimTypes.NameIdentifier), KHÔNG tin từ client (GB-04).
///   - Tuân thủ UC-18 BR-04 (Case CONFIRMED) + BR-03 (no active prescription).
///   - Adherence tính qua AdherenceCalculator (logic thuần, đã test).
/// </summary>
public sealed class PrescriptionService : IPrescriptionService
{
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IPrescriptionItemRepository _items;
    private readonly IMedicationIntakeLogRepository _intakes;
    private readonly IMedicineRepository _medicines;
    private readonly IPatientProfileRepository _patients;
    private readonly IValidator<CreatePrescriptionRequest> _validator;
    private readonly AppDbContext _db;

    public PrescriptionService(
        IPrescriptionRepository prescriptions,
        IPrescriptionItemRepository items,
        IMedicationIntakeLogRepository intakes,
        IMedicineRepository medicines,
        IPatientProfileRepository patients,
        IValidator<CreatePrescriptionRequest> validator,
        AppDbContext db)
    {
        _prescriptions = prescriptions;
        _items = items;
        _intakes = intakes;
        _medicines = medicines;
        _patients = patients;
        _validator = validator;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PrescriptionListResponse> ListByPatientAsync(
        PrescriptionListQuery query,
        CancellationToken ct = default)
    {
        var skip = (query.Page - 1) * query.PageSize;
        var entities = await _prescriptions.ListByPatientPagedAsync(
            query.PatientProfileId,
            query.FromDate,
            query.ToDate,
            query.ResolvedStatuses,
            skip,
            query.PageSize,
            ct);

        var total = await _prescriptions.CountByPatientAsync(
            query.PatientProfileId,
            query.FromDate,
            query.ToDate,
            query.ResolvedStatuses,
            ct);

        if (entities.Count == 0)
        {
            return new PrescriptionListResponse(
                Array.Empty<PrescriptionListItemResponse>(),
                total, query.Page, query.PageSize);
        }

        var prescriptionIds = entities.Select(p => p.PrescriptionId).ToList();
        var items = await _items.ListByPrescriptionIdsAsync(prescriptionIds, ct);
        var logs = await _intakes.ListByPrescriptionItemIdsAsync(
            items.Select(i => i.PrescriptionItemId).ToList(), ct);
        var now = DateTime.UtcNow;

        var itemsByPrescription = items
            .GroupBy(i => i.PrescriptionId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var logsByItem = logs
            .GroupBy(l => l.PrescriptionItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var responseItems = entities.Select(p =>
        {
            var pItems = itemsByPrescription.TryGetValue(p.PrescriptionId, out var list)
                ? list
                : new List<DAL.Entities.PrescriptionItem>();
            var allItemLogs = pItems
                .SelectMany(pi => logsByItem.TryGetValue(pi.PrescriptionItemId, out var l)
                    ? l
                    : Enumerable.Empty<DAL.Entities.MedicationIntakeLog>())
                .ToList();
            var adherence = AdherenceCalculator.Calculate(allItemLogs, now);
            return new PrescriptionListItemResponse(
                p.PrescriptionId,
                p.CaseId,
                p.DoctorId,
                p.Doctor?.FullName ?? string.Empty,
                p.PrescribedDate,
                p.Status.ToString().ToUpperInvariant(),
                pItems.Count,
                adherence,
                AdherenceLevel.FromPercent(adherence),
                p.CreatedAt);
        }).ToList();

        return new PrescriptionListResponse(responseItems, total, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<PrescriptionDetailResponse> GetDetailAsync(
        Guid prescriptionId,
        CancellationToken ct = default)
    {
        var entity = await _prescriptions.GetByIdAsync(prescriptionId, ct)
            ?? throw new PrescriptionNotFoundException(prescriptionId);

        var items = await _items.ListByPrescriptionAsync(prescriptionId, ct);
        var logs = await _intakes.ListByPrescriptionItemIdsAsync(
            items.Select(i => i.PrescriptionItemId).ToList(), ct);
        var now = DateTime.UtcNow;

        var logsByItem = logs
            .GroupBy(l => l.PrescriptionItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var itemDetails = items.Select(pi =>
        {
            var itemLogs = logsByItem.TryGetValue(pi.PrescriptionItemId, out var l)
                ? l
                : Enumerable.Empty<DAL.Entities.MedicationIntakeLog>().ToList();
            var taken = itemLogs.Count(l => l.ConfirmedAt.HasValue);
            var due = itemLogs.Count(l => l.ScheduledTime <= now);
            var adherence = AdherenceCalculator.Calculate(itemLogs, now);
            return new PrescriptionItemDetailResponse(
                pi.PrescriptionItemId,
                pi.MedicineId,
                pi.Medicine?.Name ?? string.Empty,
                pi.Dosage,
                pi.DurationDays,
                pi.StartDate,
                pi.Instructions,
                itemLogs.Count,
                taken,
                Math.Max(0, due - taken),
                adherence,
                AdherenceLevel.FromPercent(adherence));
        }).ToList();

        var overallAdherence = AdherenceCalculator.Calculate(logs.ToList(), now);

        var patientProfileId = entity.Case?.PatientProfileId ?? Guid.Empty;
        var patientName = entity.Case?.PatientProfile?.User?.FullName ?? string.Empty;

        return new PrescriptionDetailResponse(
            entity.PrescriptionId,
            entity.CaseId,
            patientProfileId,
            patientName,
            entity.DoctorId,
            entity.Doctor?.FullName ?? string.Empty,
            entity.PrescribedDate,
            entity.Status.ToString().ToUpperInvariant(),
            entity.GeneralNote,
            entity.CreatedAt,
            entity.UpdatedAt,
            itemDetails,
            overallAdherence,
            AdherenceLevel.FromPercent(overallAdherence));
    }

    /// <inheritdoc />
    public async Task<IntakeLogListResponse> GetIntakeLogsAsync(
        Guid prescriptionId,
        CancellationToken ct = default)
    {
        var prescription = await _prescriptions.GetByIdAsync(prescriptionId, ct)
            ?? throw new PrescriptionNotFoundException(prescriptionId);

        var items = await _items.ListByPrescriptionAsync(prescriptionId, ct);
        var logs = await _intakes.ListByPrescriptionItemIdsAsync(
            items.Select(i => i.PrescriptionItemId).ToList(), ct);

        var byItem = items.ToDictionary(
            i => i.PrescriptionItemId,
            i => i.Medicine?.Name ?? string.Empty);

        var list = logs
            .OrderBy(l => l.ScheduledTime)
            .Select(l => new IntakeLogListItem(
                l.IntakeId,
                l.PrescriptionItemId,
                byItem.TryGetValue(l.PrescriptionItemId, out var n) ? n : string.Empty,
                l.ScheduledTime,
                l.ConfirmedAt,
                AdherenceCalculator.StatusOf(l)))
            .ToList();

        return new IntakeLogListResponse(prescription.PrescriptionId, list);
    }

    /// <inheritdoc />
    public async Task<PrescriptionDetailResponse> CreateAsync(
        CreatePrescriptionRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        // UC-18: DoctorId từ JWT (GB-04). FindFirstValue là extension của
        // Microsoft.AspNetCore.Authentication — không có sẵn trong classlib BLL,
        // nên dùng FindFirst() trực tiếp.
        var doctorIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(doctorIdClaim) || !Guid.TryParse(doctorIdClaim, out var doctorId))
            throw new DoctorNotFoundException();

        // UC-18: validate payload
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // UC-18 BR-04: Case phải CONFIRMED
        var caseEntity = await _db.Cases
            .FirstOrDefaultAsync(c => c.CaseId == request.CaseId, ct)
            ?? throw new CaseNotFoundException(request.CaseId);

        if (caseEntity.Status != CaseStatus.Confirmed)
            throw new CaseNotConfirmedException(request.CaseId);

        // UC-18 BR-03: không có ACTIVE prescription
        if (await _prescriptions.HasActiveForCaseAsync(request.CaseId, ct))
            throw new ActivePrescriptionExistsException(request.CaseId);

        // Resolve medicines (UC-18 BR-01 — MedicineId phải tồn tại)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var resolvedItems = new List<PrescriptionItem>();
        foreach (var item in request.Items)
        {
            var medicine = await _medicines.GetByIdAsync(item.MedicineId, ct)
                ?? throw new ValidationException(
                    $"Thuốc với mã {item.MedicineId} không có trong danh mục.");

            resolvedItems.Add(new PrescriptionItem
            {
                PrescriptionItemId = Guid.NewGuid(),
                MedicineId = medicine.MedicineId,
                Dosage = item.Dosage,
                DurationDays = item.DurationDays,
                StartDate = item.StartDate == default ? today : item.StartDate,
                Instructions = item.Instructions,
            });
        }

        var prescription = new Prescription
        {
            PrescriptionId = Guid.NewGuid(),
            CaseId = request.CaseId,
            DoctorId = doctorId,
            PrescribedDate = today,
            GeneralNote = request.GeneralNote,
            Status = PrescriptionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _prescriptions.AddAsync(prescription, ct);
        foreach (var ri in resolvedItems)
            ri.PrescriptionId = prescription.PrescriptionId;
        await _items.AddRangeAsync(resolvedItems, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetDetailAsync(prescription.PrescriptionId, ct);
    }
}
