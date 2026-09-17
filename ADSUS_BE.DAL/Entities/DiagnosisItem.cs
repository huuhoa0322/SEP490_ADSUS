using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Danh mục bệnh chẩn đoán chuyên khoa phụ khoa — curated list cho bác sĩ chọn nhanh khi khám.
/// </summary>
public partial class DiagnosisItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool RequiresNote { get; set; }

    public bool IsOther { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CaseDiagnosis> CaseDiagnoses { get; set; } = new List<CaseDiagnosis>();
}
