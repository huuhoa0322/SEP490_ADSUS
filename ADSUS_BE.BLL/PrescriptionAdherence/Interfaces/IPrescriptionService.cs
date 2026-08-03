using System.Security.Claims;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

/// <summary>
/// Module 7 — service nghiệp vụ cho Prescription & Adherence.
/// Triển khai UC-11 (đọc) + UC-18 (ghi). Pushed DoctorId từ JWT claims,
/// không bao giờ tin từ request body (GB-04).
/// </summary>
public interface IPrescriptionService
{
    /// <summary>UC-11: danh sách đơn thuốc của 1 bệnh nhân, có filter + phân trang.</summary>
    Task<PrescriptionListResponse> ListByPatientAsync(
        PrescriptionListQuery query,
        CancellationToken ct = default);

    /// <summary>UC-11: chi tiết 1 đơn + adherence per-item.</summary>
    Task<PrescriptionDetailResponse> GetDetailAsync(Guid prescriptionId, CancellationToken ct = default);

    /// <summary>UC-11: timeline liều thuốc của 1 đơn, sort theo scheduled_time tăng dần.</summary>
    Task<IntakeLogListResponse> GetIntakeLogsAsync(Guid prescriptionId, CancellationToken ct = default);

    /// <summary>UC-18: bác sĩ kê đơn cho 1 Case đã Confirmed. DoctorId lấy từ JWT.</summary>
    Task<PrescriptionDetailResponse> CreateAsync(
        CreatePrescriptionRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default);
}
