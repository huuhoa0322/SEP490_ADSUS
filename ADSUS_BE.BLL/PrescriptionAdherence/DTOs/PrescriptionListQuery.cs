using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Module 7 UC-11 — filter cho GET /api/v1/patient-profiles/{id}/prescriptions.
/// StatusFilter "ALL" (mặc định) hoặc "ACTIVE"/"COMPLETED" — ResolvedStatuses
/// chuyển sang enum list thật để truyền vào repository.
/// Page mặc định 1, PageSize mặc định 20 (khớp UCS UC-09 BR-01).
/// </summary>
public sealed record PrescriptionListQuery(
    Guid PatientProfileId,
    string? StatusFilter,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int PageSize)
{
    public IReadOnlyCollection<PrescriptionStatus> ResolvedStatuses =>
        StatusFilter?.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => new[] { PrescriptionStatus.Active },
            "COMPLETED" => new[] { PrescriptionStatus.Completed },
            _ => Array.Empty<PrescriptionStatus>(),
        };
}
