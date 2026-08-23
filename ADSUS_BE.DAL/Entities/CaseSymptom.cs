using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

public partial class CaseSymptom
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid? SymptomId { get; set; }

    public string? OtherNote { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Case Case { get; set; } = null!;

    public virtual SymptomCategory Category { get; set; } = null!;

    public virtual Symptom? Symptom { get; set; }
}
