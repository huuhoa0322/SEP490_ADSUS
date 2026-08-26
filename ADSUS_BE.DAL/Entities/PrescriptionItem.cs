using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// 1 đơn chứa NHIỀU thuốc, mỗi thuốc liều/lịch riêng (repeating group → bảng riêng). Job nhắc thuốc (JOB-01) đọc schedule_slots + start_date + duration_days để sinh liều, tra thêm patient_reminder_preferences để lấy giờ cụ thể.
/// </summary>
public partial class PrescriptionItem
{
    public Guid PrescriptionItemId { get; set; }

    public Guid PrescriptionId { get; set; }

    /// <summary>
    /// Tra danh mục medicines qua ô tìm kiếm khi kê đơn; tự thêm mới vào danh mục nếu bác sĩ gõ tên chưa có (thay cho nhập tự do trước đây).
    /// </summary>
    public Guid MedicineId { get; set; }

    public string Dosage { get; set; } = null!;

    public short DurationDays { get; set; }

    public DateOnly StartDate { get; set; }

    public string? Instructions { get; set; }

    public int QuantityBase { get; set; }

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual ICollection<MedicationIntakeLog> MedicationIntakeLogs { get; set; } = new List<MedicationIntakeLog>();

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Prescription Prescription { get; set; } = null!;
}
