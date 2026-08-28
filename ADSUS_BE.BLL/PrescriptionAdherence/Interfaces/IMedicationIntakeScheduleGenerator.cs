using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

/// <summary>
/// Sinh danh sách MedicationIntakeLog từ 1 PrescriptionItem kèm khung giờ uống (MORNING/NOON/EVENING).
/// Mỗi khung = 1 log với scheduled_time được tính từ ngày bắt đầu + giờ nhắc của bệnh nhân.
/// </summary>
public interface IMedicationIntakeScheduleGenerator
{
    /// <summary>
    /// Sinh logs cho 1 dòng thuốc. Mỗi slot (MORNING/NOON/EVENING) tạo 1 log riêng
    /// cho mỗi ngày trong khoảng [StartDate, StartDate + DurationDays - 1].
    /// </summary>
    /// <param name="utcNow">Mốc thời gian hiện tại (UTC). Dùng cho skip logic và test deterministic.</param>
    Task<IReadOnlyList<ScheduledDose>> GenerateAsync(
        PrescriptionItemWithPatient item,
        IReadOnlyList<ScheduleSlot> slots,
        TimeOnly patientMorningTime,
        TimeOnly patientMiddayTime,
        TimeOnly patientEveningTime,
        DateTime utcNow,
        CancellationToken ct = default);
}

/// <summary>
/// Kết quả sinh từ generator — dùng để batch-insert vào DB.
/// </summary>
public sealed record ScheduledDose(
    Guid PrescriptionItemId,
    DateTime ScheduledTimeUtc);

/// <summary>
/// 1 dòng thuốc kèm patient context cần thiết để tính scheduled_time.
/// </summary>
public sealed record PrescriptionItemWithPatient(
    Guid PrescriptionItemId,
    Guid PatientProfileId,
    DateOnly StartDate,
    short DurationDays);