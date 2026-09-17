using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Chẩn đoán bệnh của bác sĩ cho ca khám — structured selection từ diagnosis_items.
/// </summary>
public partial class CaseDiagnosis
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public Guid DiagnosisItemId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual DiagnosisItem DiagnosisItem { get; set; } = null!;
}
