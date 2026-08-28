using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Danh mục thuốc dùng chung. Bác sĩ gõ tìm tên thuốc khi kê đơn (FT-30) qua ô tìm kiếm; nếu chưa có trong danh mục, hệ thống tự thêm mới để dùng lại cho lần sau — thay cho việc nhập tự do trước đây.
/// </summary>
public partial class Medicine
{
    public Guid MedicineId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? UsageUnit { get; set; }

    public decimal? VolumePerBaseUnit { get; set; }

    public virtual ICollection<MedicineBatch> MedicineBatches { get; set; } = new List<MedicineBatch>();

    public virtual ICollection<MedicinePackaging> MedicinePackagings { get; set; } = new List<MedicinePackaging>();

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();
}
