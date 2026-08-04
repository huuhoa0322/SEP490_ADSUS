using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

/// <summary>
/// UC-11 (Patient) — xem lịch uống thuốc của chính mình.
/// UC-17 — xác nhận đã uống (GB-01: one-way Pending → Taken).
/// </summary>
public interface IMedicationIntakeService
{
    /// <summary>UC-11 — Danh sách liều của 1 đơn thuốc (Patient xem đơn của mình).</summary>
    Task<IReadOnlyList<IntakeLogResponse>> ListByPrescriptionAsync(
        Guid patientId,
        Guid prescriptionId,
        CancellationToken ct = default);

    /// <summary>UC-11 — Tất cả liều sắp tới của 1 bệnh nhân (Today + upcoming).</summary>
    Task<IReadOnlyList<IntakeLogResponse>> ListUpcomingAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// UC-17 — Xác nhận đã uống. GB-01: trạng thái một chiều PENDING → TAKEN.
    /// Idempotent: nếu đã TAKEN thì trả 204 mà không cập nhật ConfirmedAt.
    /// </summary>
    Task ConfirmTakenAsync(
        Guid patientId,
        Guid intakeId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy summary tuân thủ cho 1 đơn (dùng AdherenceCalculator).
    /// </summary>
    Task<AdherenceSummary> GetAdherenceAsync(
        Guid prescriptionId,
        CancellationToken ct = default);
}