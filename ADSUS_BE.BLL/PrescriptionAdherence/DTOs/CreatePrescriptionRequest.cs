using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

/// <summary>
/// Request DTO cho POST /api/v1/prescriptions (UC-18). Bác sĩ kê đơn sau lượt khám.
/// CaseId liên kết tới bệnh nhân — kiểm tra doctor có case này ở tầng handler.
/// ScheduleSlots là runtime param (MORNING/NOON/EVENING) — KHÔNG persist ở
/// PrescriptionItem (master convention); IntakeLogGenerationService dùng chúng
/// kết hợp patient_reminder_preferences để tính scheduled_time cho từng intake log.
/// </summary>
public sealed record CreatePrescriptionRequest(
    [Required] Guid CaseId,
    [Required] Guid DoctorId,
    [MaxLength(2000)] string? GeneralNote,
    [Required][MinLength(1)] IReadOnlyList<CreatePrescriptionItemDto> Items);

/// <summary>
/// 1 dòng thuốc trong đơn kê.
/// MedicineName: tên thuốc nhập bởi bác sĩ. Backend tự tìm hoặc tạo mới trong bảng medicines.
/// (Option A: tự động quản lý danh mục thuốc).
/// </summary>
public sealed record CreatePrescriptionItemDto(
    [Required][MinLength(1)] string MedicineName,
    [Required] string Dosage,
    [Range(1, 365)] short DurationDays,
    DateOnly StartDate,
    [MaxLength(1000)] string? Instructions,
    [Required][MinLength(1)] IReadOnlyList<ScheduleSlot> ScheduleSlots);

/// <summary>Enum runtime — phải khớp Postgres enum reminder_slot MORNING/NOON/EVENING.</summary>
public enum ScheduleSlot { Morning, Noon, Evening }