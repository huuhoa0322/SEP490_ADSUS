using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Snapshot dị ứng của bệnh nhân tại thời điểm diễn ra ca khám. Sửa tại ca khám chỉ lưu trên ca đó, không ảnh hưởng hồ sơ nền gốc.
/// </summary>
public partial class CaseAllergy
{
    /// <summary>
    /// Khóa chính bản ghi snapshot dị ứng ca khám
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Khóa ngoại trỏ về ca khám tương ứng
    /// </summary>
    public Guid CaseId { get; set; }

    /// <summary>
    /// Khóa ngoại trỏ về danh mục loại dị ứng (medical_allergy_types)
    /// </summary>
    public Guid AllergyTypeId { get; set; }

    /// <summary>
    /// Ghi chú lâm sàng cụ thể cho dị ứng tại ca khám (tối đa 500 ký tự)
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Thời điểm ghi nhận snapshot
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    public virtual MedicalAllergyType AllergyType { get; set; } = null!;

    public virtual Case Case { get; set; } = null!;
}
